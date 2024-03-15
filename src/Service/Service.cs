using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Protocol;
using MqttSql.Configuration;
using MqttSql.Database;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static MqttSql.Configuration.SubscriptionConfiguration;
using static MqttSql.Database.DatabaseMessageHandler;

namespace MqttSql;

public sealed class Service
{
    private const string logFileName = "logs.txt";
    private const string configurationFileName = "config.json";
    private static readonly TimeSpan mqttClientKeepAlive = TimeSpan.FromHours(1);
    private static readonly TimeSpan mqttClientReconnectionBackoffInitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan mqttClientReconnectionBackoffMaxDelay = TimeSpan.FromMinutes(15);
    private const double mqttClientReconnectionBackoffMultiplier = 1.7;
    private const int mqttClientReconnectionBackoffMaxRetries = 0;
    private static readonly TimeSpan mqttSameBrokerDifferentClientConnectionDelay = TimeSpan.FromSeconds(1);

    public enum ServiceState
    {
        Created,
        Starting,
        Running,
        Restarting,
        Stopping,
        Exited
    }

    public ServiceState State { get; private set; }

    public Service(string? homeDirectory = null)
    {
        string? macAddress = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .FirstOrDefault();

        mqttClientIdBase = $"{macAddress ?? Guid.NewGuid().ToString()}-{Environment.MachineName}-{Environment.UserName}".Replace(' ', '.');

#if DEBUG
        this.homeDirectory = homeDirectory ?? Directory.GetCurrentDirectory();
#else
        this.homeDirectory = homeDirectory ?? Environment.GetEnvironmentVariable("MqttSqlHome") ?? Directory.GetCurrentDirectory();
#endif
        if (!Path.EndsInDirectorySeparator(this.homeDirectory)) this.homeDirectory += Path.DirectorySeparatorChar;

        configurationFilePath = this.homeDirectory + configurationFileName;
        var logFilePath = this.homeDirectory + logFileName;

        logger = new Logger(
#if DEBUG
            logFilePath: null,
            logToConsole: true,
            logLevel: Logger.LogLevel.Trace,
            logTimestamp: false
#else
            logFilePath: logFilePath,
            logToConsole: false,
            logLevel: Logger.LogLevel.Information,
            logTimestamp: true
#endif
        );

        logger.Debug("Mqtt Client Id: \"", mqttClientIdBase, '"');
        logger.Debug("Home: \"", this.homeDirectory, '"');
        logger.Debug("Logs: \"", logFilePath, '"');
        logger.Debug("Configuration: \"", configurationFilePath, '"');

        State = ServiceState.Created;
    }

    public void Start()
    {
        Task.Run(() => StartAsync(), ServiceCancellationToken);
    }

    public async Task StartAsync()
    {
        State = ServiceState.Starting;

        try
        {
            await _StartAsync();

            State = ServiceState.Stopping;

            if (ServiceCancelled) logger.Information("Service cancelled");
            else logger.Warning("Service exited");
        }
        catch (Exception ex)
        {
            State = ServiceState.Stopping;

            if (ex is OperationCanceledException)
            {
                logger.Information("Service cancelled");
            }
            else
            {
                logger.Critical(ex, "Uncought exception");
                await logger.FlushAsync(TimeSpan.FromMinutes(1), ServiceCancellationToken);

                serviceStopped = true;
                State = ServiceState.Exited;

                return;
            }
        }

        await logger.FlushAsync(TimeSpan.FromMinutes(1));

        serviceStopped = true;
        serviceCancellationTokenSource.Dispose();

        State = ServiceState.Exited;
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Private method with the same name")]
    private async Task _StartAsync()
    {
        logger.Information("Starting service");

        var MethodsToExecute = new (Action? Synchronous, Func<Task>? Asynchronous)[]
        {
            (LoadConfiguration, null),
            (RegisterConfigurationFileChangeWatcher, null),
            (null, CreateMessageQueues),
            (SubscribeToBrokers, null)
        };

        async Task Reset()
        {
            State = ServiceState.Restarting;

            brokers = null;
            configurationFileChangeTokenSource?.Dispose();
            configurationFileChangeTokenSource = null;
            serviceCancellationOrConfigurationFileChangeTokenSource?.Dispose();
            serviceCancellationOrConfigurationFileChangeTokenSource = null;
            messageHandler = null;

            if (mqttClients != null)
            {
                if (mqttClients.Count != 0) logger.Information("Disconnecting all ", mqttClients.Count, " clients");

                var disconnectionOptions = new MqttClientDisconnectOptions()
                {
                    ReasonCode = MqttClientDisconnectReason.NormalDisconnection
                };

                foreach (var client in mqttClients)
                {
                    await client.DisconnectAsync(disconnectionOptions);
                    client.Dispose();
                }

                mqttClients = null;
            }
        }

        while (true)
        {
            logger.Information("Initializing service");

            foreach (var (synchronous, asynchronous) in MethodsToExecute)
            {
                if (synchronous != null) synchronous();
                else if (asynchronous != null) await asynchronous();
                else throw new UnreachableException($"Either the synchronous or asynchronous delegate must not be null in {nameof(MethodsToExecute)}");

                if (ServiceCancelled) return;

                if (ConfigurationFileChanged)
                {
                    await Reset();
                    break;
                }
            }

            State = ServiceState.Running;
            await messageHandler!.HandleMessagesAsync();

            if (ServiceCancelled) return;

            if (ConfigurationFileChanged) await Reset();
        }
    }

    public void Stop()
    {
        StopAsync().Wait();
    }

    public async Task StopAsync()
    {
        State = ServiceState.Stopping;

        CalncelService();

        messageHandler?.Dispose();

        logger.Information("Stopping service");

        var delay = TimeSpan.FromMilliseconds(100);
        while (!serviceStopped) await Task.Delay(delay);

        State = ServiceState.Exited;
    }

    private void LoadConfiguration()
    {
        try
        {
            brokers = ConfigurationLoader.LoadBrokersFromJson(configurationFilePath, logger, homeDirectory);

            if (brokers.Length == 0)
            {
                logger.Critical("No valid brokers found");

                CalncelService();
            }
        }
        catch (JsonException ex)
        {
            logger.Critical(ex);

            CalncelService();
        }
    }

    private void RegisterConfigurationFileChangeWatcher()
    {
        if (configFileChangeWatcher != null)
        {
            configFileChangeWatcher.Dispose();
            configFileChangeWatcher = null;
        }

        configurationFileChangeTokenSource?.Dispose();
        configurationFileChangeTokenSource = new CancellationTokenSource();
        serviceCancellationOrConfigurationFileChangeTokenSource?.Dispose();
        serviceCancellationOrConfigurationFileChangeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            ServiceCancellationToken,
            ConfigurationFileChangeToken
        );

        configFileChangeWatcher = new(homeDirectory);
        configFileChangeWatcher.IncludeSubdirectories = false;
        configFileChangeWatcher.Filter = configurationFileName;
        configFileChangeWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
        configFileChangeWatcher.Error += OnConfigFileChangeWatcherError;
        configFileChangeWatcher.Changed += OnConfigurationFileChanged;
        configFileChangeWatcher.EnableRaisingEvents = true;
    }

    private void OnConfigFileChangeWatcherError(object sender, ErrorEventArgs e)
    {
        logger.Error(e.GetException(), "FileSystemWatcher was unable to continue monitoring for configuration file changes");

        if (configFileChangeWatcher != null)
        {
            configFileChangeWatcher.Dispose();
            configFileChangeWatcher = null;
        }
    }

    private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
    {
        if (configFileChangeWatcher == null) return;

        configFileChangeWatcher.Changed -= OnConfigurationFileChanged;
        configFileChangeWatcher = null;

        logger.Information("Configuration file changed");

        configurationFileChangeTokenSource!.Cancel(false);
    }

    private async Task CreateMessageQueues()
    {
        var databases = brokers!.SelectMany(broker => broker.Clients.SelectMany(client => client.Subscriptions.SelectMany(sub => sub.Databases)));

        messageHandler = await Initialize(databases, logger, ServiceCancellationOrConfigurationFileChangeToken);

        if (messageHandler == null && !ServiceCancelledOrConfigurationFileChanged) {
            logger.Critical($"Failed to initialize the {nameof(DatabaseMessageHandler)}");

            CalncelService();
        }
    }

    private void SubscribeToBrokers()
    {
        List<Task> tasks = new(brokers!.Length);

        mqttClients = new(brokers.Sum(broker => broker.Clients.Length));

        foreach (var broker in brokers)
        {
            if (ServiceCancelledOrConfigurationFileChanged) break;

            tasks.Add(Task.Run(async () =>
            {
                foreach (var (client, index) in broker.Clients.Select((client, index) => (client, index)))
                {
                    if (ServiceCancelledOrConfigurationFileChanged) break;

                    var clientId = mqttClientIdBase + (broker.Clients.Length != 1 ? $"-{index}" : "");

                    var factory = new MqttFactory(new MqttNetLogger(clientId, logger, Logger.LogLevel.Warning, MqttNetLogLevel.Warning));
                    var mqttClient = factory.CreateMqttClient();
                    var mqttOptions = new MqttClientOptionsBuilder()
                        .WithClientId(clientId)
                        .WithTcpServer(broker.Host, broker.Port)
                        .WithCredentials(client.User, client.Password)
                        .WithKeepAlivePeriod(mqttClientKeepAlive)
                        // TODO: add support for TLS
                        .WithTls(new MqttClientOptionsBuilderTlsParameters()
                        {
                            Certificates = null
                        })
                        .Build();

                    var exponentialBackoff = new ExponentialBackoff(
                        initialDelay: mqttClientReconnectionBackoffInitialDelay,
                        maxDelay: mqttClientReconnectionBackoffMaxDelay,
                        multiplier: mqttClientReconnectionBackoffMultiplier,
                        maxRetries: mqttClientReconnectionBackoffMaxRetries
                    );

                    mqttClient.UseDisconnectedHandler(async e =>
                    {
                        if (ServiceCancelledOrConfigurationFileChanged)
                        {
                            logger.Information('"', clientId, "\" client disconnected from \"", broker.Host, ':', broker.Port, "\" host");
                            return;
                        }

                        if (exponentialBackoff.FirstTime)
                        {
                            logger.Information('"', clientId, "\" client disconnected from \"", broker.Host, ':', broker.Port, "\" host because \"", Enum.GetName(e.Reason), '"');
                        }

                        await exponentialBackoff.Delay(ServiceCancellationOrConfigurationFileChangeToken);
                        if (ServiceCancelledOrConfigurationFileChanged) return;

                        try
                        {
                            logger.Debug("Attempting to recconnect \"", clientId, "\" client to \"", broker.Host, ':', broker.Port, "\" host");

                            await mqttClient.ReconnectAsync(ServiceCancellationOrConfigurationFileChangeToken);
                            exponentialBackoff.Reset();

                            logger.Information('"', clientId, "\" client reconnected to \"", broker.Host, ':', broker.Port, "\" host");
                        }
                        catch
                        {
                            if (ServiceCancelledOrConfigurationFileChanged) logger.Debug("Reconnection cancelled");
                            else logger.Debug("Reconnection Failed!");
                        }
                    });

                    mqttClient.UseConnectedHandler(async _ =>
                    {
                        logger.Debug('"', clientId, "\" client connected to \"", broker.Host, ':', broker.Port, "\" host");

                        foreach (var subscription in client.Subscriptions)
                        {
                            logger.Information("Subscribing \"", clientId, "\" client to \"", subscription.Topic, "\" topic at \"", broker.Host, ':', broker.Port, "\" host");

                            var result = await mqttClient.SubscribeAsync(
                                factory.CreateSubscribeOptionsBuilder()
                                    .WithTopicFilter(subscription.Topic, qualityOfServiceLevel: (MqttQualityOfServiceLevel)subscription.QOS)
                                    .Build(),
                                ServiceCancellationOrConfigurationFileChangeToken
                            );

                            foreach (var resultCode in result.Items.Select(item => item.ResultCode))
                            {
                                var success = subscription.QOS switch
                                {
                                    MqttQualityOfService.AtMostOnce => resultCode == MqttClientSubscribeResultCode.GrantedQoS0,
                                    MqttQualityOfService.AtLeastOnce => resultCode == MqttClientSubscribeResultCode.GrantedQoS1,
                                    MqttQualityOfService.ExactlyOnce => resultCode == MqttClientSubscribeResultCode.GrantedQoS2,
                                    _ => throw new NotImplementedException($"{typeof(MqttQualityOfService).FullName}.{Enum.GetName(subscription.QOS)}")
                                };

                                if (!success)
                                {
                                    logger.Error('"', clientId, "\" client failed to subscribe to \"", subscription.Topic, "\" topic at \"", broker.Host, ':', broker.Port, "\" host with status code \"", Enum.GetName(resultCode), '"');
                                }
                            }
                        }
                    });

                    (DatabaseConfiguration[] databases, string message) getMessageAndDatabases(MqttApplicationMessageReceivedEventArgs e)
                    {
                        string topic = e.ApplicationMessage.Topic;
                        string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                        logger.Debug("Message from \"", topic, "\" topic recieved: \"", message, '"');

                        return (client.Subscriptions.First(sub => sub.Topic.Equals(topic)).Databases, message);
                    }

                    mqttClient.UseApplicationMessageReceivedHandler(e =>
                    {
                        (DatabaseConfiguration[] databases, string message) = getMessageAndDatabases(e);

                        var timestamp = DateTime.Now;

                        foreach (var database in databases)
                            messageHandler!.WriteMessage(new DatabaseMessage(database, timestamp, message));
                    });

                    logger.Information("Connecting \"", clientId, "\" client to \"", broker.Host, ':', broker.Port, "\" host");

                    await mqttClient.ConnectAsync(mqttOptions, ServiceCancellationOrConfigurationFileChangeToken);
                    mqttClients.Add(mqttClient);

                    await Task.Delay(mqttSameBrokerDifferentClientConnectionDelay, ServiceCancellationOrConfigurationFileChangeToken);
                }
            }, ServiceCancellationOrConfigurationFileChangeToken));
        }

        if (ServiceCancelledOrConfigurationFileChanged) return;

        Task.WhenAll(tasks);
    }

    private readonly CancellationTokenSource serviceCancellationTokenSource = new();

    private void CalncelService() => serviceCancellationTokenSource.Cancel(false);
    private bool serviceStopped;

    private bool ServiceCancelled => serviceCancellationTokenSource.IsCancellationRequested;
    private CancellationToken ServiceCancellationToken => serviceCancellationTokenSource.Token;

    private readonly Logger logger;

    private readonly string mqttClientIdBase;
    private readonly string homeDirectory;
    private readonly string configurationFilePath;

    private FileSystemWatcher? configFileChangeWatcher;
    private CancellationTokenSource? configurationFileChangeTokenSource;
    private bool ConfigurationFileChanged => configurationFileChangeTokenSource?.IsCancellationRequested ?? false;
    private CancellationToken ConfigurationFileChangeToken => configurationFileChangeTokenSource?.Token ?? default;
    private CancellationTokenSource? serviceCancellationOrConfigurationFileChangeTokenSource;
    private bool ServiceCancelledOrConfigurationFileChanged => serviceCancellationOrConfigurationFileChangeTokenSource?.IsCancellationRequested ?? false;
    private CancellationToken ServiceCancellationOrConfigurationFileChangeToken => serviceCancellationOrConfigurationFileChangeTokenSource?.Token ?? default;

    private List<IMqttClient>? mqttClients;
    private BrokerConfiguration[]? brokers;

    private DatabaseMessageHandler? messageHandler;
}
