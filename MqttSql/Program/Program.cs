/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;
using static MqttSql.Program.ThrowHelpers;
using static MqttSql.Program.LinuxHelpers;
using System.Diagnostics;
using System.IO;
using static MqttSql.CommandLineArgs;

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
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (!serviceStopped && service.State != Service.ServiceState.Exited)
            {
                Console.WriteLine("Received SIGTERM");
                service.Stop();
            }
        };

        await service.StartAsync();

        return 0;

        static (string? config, string? logfile, string? sqliteBase) GetPathsFromArgs(CommandAndArgs cliArgs) => (
            cliArgs.ArgValue("config", 'c')?.RequiredValue,
            cliArgs.ArgValue("logfile", 'l')?.RequiredValue,
            cliArgs.ArgValue("sqlite-dir", 's')?.RequiredValue
        );
    }

    private static async Task<int> Install(CommandAndArgs args)
    {
        ThrowIfCommand("install").IsOnlySuportedOnPlatforms(Linux);

        if (IsOSPlatform(Linux))
        {
            var systemdServiceUnitPath = GetSystemdServiceUnitPath(systemdServiceName);
            if (File.Exists(systemdServiceUnitPath)) return 0;

            var workingDirectory = Directory.GetCurrentDirectory();
            var executable = Process.GetCurrentProcess().MainModule?.FileName ?? $"{workingDirectory}/{nameof(MqttSql)}";

            var userArg = args.ArgValue("user", 'u') ?? "root";

            Console.WriteLine("Creating systemd service:");
            Console.WriteLine($"\t Directory: {workingDirectory}");
            Console.WriteLine($"\t Executable: {executable}");
            Console.WriteLine($"\t User: {userArg.RequiredValue}");
            if (userArg.Value == "root")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("The service was set to run with \"root\" user. "
                    + $"If this is undesirable, either modify the \"{systemdServiceUnitPath}\" service file, "
                    + "or install using the \"-u\" or \"--user\" argument.");
                Console.ResetColor();
            }

            try
            {
                await File.WriteAllTextAsync(
                    systemdServiceUnitPath,
                    GetSystemdServiceUnitContent(
                        executablePath: executable,
                        workingDirectory: workingDirectory,
                        description: "Subscribes to MQTT brokers and writes the messages to SQL databases"
                    )
                );
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return -1;
            }

            return await ExecuteSystemd(SystemdSubcommand.DaemonReload, systemdServiceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Uninstall()
    {
        ThrowIfCommand("uninstall").IsOnlySuportedOnPlatforms(Linux);

        if (IsOSPlatform(Linux))
        {
            int exitCode = await Stop();
            if (exitCode != 0) return exitCode;

            File.Delete(GetSystemdServiceUnitPath(systemdServiceName));

            return await ExecuteSystemd(SystemdSubcommand.DaemonReload, systemdServiceName);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Start()
    {
        ThrowIfCommand("start").IsOnlySuportedOnPlatforms(Linux);

        if (IsOSPlatform(Linux))
        {
            int exitCode = await ExecuteSystemd(SystemdSubcommand.Enable, systemdServiceName);
            return exitCode == 0 ? await ExecuteSystemd(SystemdSubcommand.Start, systemdServiceName) : exitCode;
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static async Task<int> Stop()
    {
        ThrowIfCommand("stop").IsOnlySuportedOnPlatforms(Linux);

        if (IsOSPlatform(Linux))
        {
            int exitCode = await ExecuteSystemd(SystemdSubcommand.Stop, systemdServiceName);
            return exitCode == 0 ? await ExecuteSystemd(SystemdSubcommand.Stop, systemdServiceName) : exitCode;
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private const string systemdServiceName = "mqtt-sql";
}
