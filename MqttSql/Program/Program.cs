/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System.Diagnostics;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using static MqttSql.CommandLineArgs;
using static MqttSql.Program.ThrowHelpers;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;

namespace MqttSql.Program;

public static class Program
{
    public static async Task Main(string[] args)
    {
        CommandLineArgs cliArgs = new(args);

        ThrowIfCommand("uninstall").IsUsedWithCommands("install", "start").InArgs(cliArgs);
        ThrowIfCommand("stop").IsUsedWithCommands("start").InArgs(cliArgs);

        int exitCode = 0;

        if (cliArgs.ContainsSubcommand("run") || cliArgs.SubcommandsAndArgs.Length == 0) exitCode = await Run(cliArgs.TopLevelArgs);
        if (exitCode == 0 && cliArgs.ContainsSubcommand("status")) exitCode = await Status();
        if (exitCode == 0 && cliArgs.ContainsSubcommand("install")) exitCode = await Install(cliArgs["install"]);
        if (exitCode == 0 && cliArgs.ContainsSubcommand("start")) exitCode = await Start();
        if (exitCode == 0 && cliArgs.ContainsSubcommand("stop")) exitCode = await Stop();
        if (exitCode == 0 && cliArgs.ContainsSubcommand("uninstall")) exitCode = await Uninstall();

        Environment.ExitCode = exitCode;
    }

    private static async Task<int> Run(CommandAndArgs cliArgs)
    {
        var (config, logfile, sqliteBase) = GetPathsFromArgs(cliArgs);
        Service service = new(configFilePath: config, logFilePath: logfile, sqliteBasePath: sqliteBase);

        bool serviceStopped = false;

        // Handle SIGINT (Ctrl+C)
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Tell .NET to not terminate the process
            eventArgs.Cancel = true;

            service.Stop();
            serviceStopped = true;
        };

        // Handle SIGTERM
        AssemblyLoadContext.Default.Unloading += (_) => stopService();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => stopService();
        void stopService()
        {
            if (!serviceStopped && service.State != Service.ServiceState.Exited)
            {
                Console.WriteLine("Received SIGTERM");
                service.Stop();
                serviceStopped = true;
            }
        }

        await service.StartAsync();

        return 0;

        static (string? config, string? logfile, string? sqliteBase) GetPathsFromArgs(CommandAndArgs cliArgs) => (
            cliArgs.ArgValue("config", 'c')?.RequiredValue,
            cliArgs.ArgValue("logfile", 'l')?.RequiredValue,
            cliArgs.ArgValue("sqlite-dir", 's')?.RequiredValue
        );
    }

#pragma warning disable IDE0046 // Convert to conditional expression
    private static async Task<int> Status()
    {
        ThrowIfCommand("stop").IsOnlySuportedOnPlatforms(Linux, Windows);

        if (IsOSPlatform(Linux))
        {
            return await LinuxHelpers.PrintServiceStatus(serviceName);
        }
        else if (IsOSPlatform(Windows))
        {
            return await WindowsHelpers.QueryServiceStatus(serviceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Install(CommandAndArgs args)
    {
        ThrowIfCommand("install").IsOnlySuportedOnPlatforms(Linux, Windows);

        if (IsOSPlatform(Linux))
        {
            return await LinuxHelpers.InstallService(
                serviceName,
                serviceDescription,
                args.ArgValue("user", 'u')?.Value
            );
        }
        else if (IsOSPlatform(Windows))
        {
            return await WindowsHelpers.InstallService(serviceName, serviceDisplayName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Uninstall()
    {
        ThrowIfCommand("uninstall").IsOnlySuportedOnPlatforms(Linux, Windows);

        if (IsOSPlatform(Linux))
        {
            return await LinuxHelpers.UninstallService(serviceName);
        }
        else if (IsOSPlatform(Windows))
        {
            return await WindowsHelpers.UninstallService(serviceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Start()
    {
        ThrowIfCommand("start").IsOnlySuportedOnPlatforms(Linux, Windows);

        if (IsOSPlatform(Linux))
        {
            return await LinuxHelpers.StartService(serviceName);
        }
        else if (IsOSPlatform(Windows))
        {
            return await WindowsHelpers.StartService(serviceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Stop()
    {
        ThrowIfCommand("stop").IsOnlySuportedOnPlatforms(Linux, Windows);

        if (IsOSPlatform(Linux))
        {
            return await LinuxHelpers.StopService(serviceName);
        }
        else if (IsOSPlatform(Windows))
        {
            return await WindowsHelpers.StopService(serviceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }
#pragma warning restore IDE0046 // Convert to conditional expression

    private const string serviceName = "mqtt-sql";
    private const string serviceDisplayName = "MqttSql";
    private const string serviceDescription = "Subscribes to MQTT brokers and writes the messages to SQL databases";
}
