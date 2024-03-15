using System;
using System.IO;
using Topshelf;
using Topshelf.Runtime.DotNetCore;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;

namespace MqttSql;

public static partial class Program
{
    public static void Main(string[] args)
    {
        if (Array.Exists(args, arg => arg.Equals("install")))
        {
            string home = Directory.GetCurrentDirectory();
            Console.WriteLine($"Setting home directory to \"{home}\"");
            Environment.SetEnvironmentVariable("MqttSqlHome", home, EnvironmentVariableTarget.Machine);
        }

        var exitCode = HostFactory.Run(host =>
        {
            if (IsOSPlatform(OSX) || IsOSPlatform(Linux) || IsOSPlatform(FreeBSD))
                host.UseEnvironmentBuilder(target => new DotNetCoreEnvironmentBuilder(target));

            host.Service<Service>(service =>
            {
#if DEBUG
                service.ConstructUsing(_ => new Service(Directory.GetCurrentDirectory()));
#else
                service.ConstructUsing(_ => new Service());
#endif
                service.WhenStarted(s => s.Start());
                service.WhenStopped(s => s.Stop());
            });

            host.RunAsLocalSystem();

            host.SetServiceName("MqttSql");
            host.SetDisplayName("MQTT to SQL");
            host.SetDescription("Subscribes to MQTT brokers and writes the messages to local SQLite databases");
            host.EnableServiceRecovery(src => src.RestartService(TimeSpan.FromSeconds(10)));
            host.StartAutomatically();

            host.OnException(e =>
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            });
        });

        Environment.ExitCode = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
    }
}
