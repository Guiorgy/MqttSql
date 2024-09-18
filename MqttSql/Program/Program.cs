/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;
using static MqttSql.Program.ThrowHelpers;
using static MqttSql.Program.LinuxHelpers;
using System.Diagnostics;
using System.IO;

namespace MqttSql.Program;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 0)
        {
            ThrowIfCommand("uninstall").IsUsedWithCommands("install", "start").InArgs(args);
            ThrowIfCommand("stop").IsUsedWithCommands("start").InArgs(args);

            int exitCode = 0;

            if (args.Contains("install")) exitCode = await Install(args);
            if (args.Contains("start")) exitCode = await Start();
            if (args.Contains("stop")) exitCode = await Stop();
            if (args.Contains("uninstall")) exitCode = await Uninstall();

            Environment.ExitCode = exitCode;
            return;
        }

        Service service = new();

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
    }

    private static async Task<int> Install(string[] args)
    {
        ThrowIfCommand("install").IsOnlySuportedOnPlatforms(Linux);

        string home = Directory.GetCurrentDirectory();
        Console.WriteLine($"Setting home directory to \"{home}\"");
        Environment.SetEnvironmentVariable("MqttSqlHome", home, EnvironmentVariableTarget.Machine);

        if (IsOSPlatform(Linux))
        {
            var systemdServiceUnitPath = GetSystemdServiceUnitPath(systemdServiceName);
            if (File.Exists(systemdServiceUnitPath)) return 0;

            var workingDirectory = Directory.GetCurrentDirectory();
            var executable = Process.GetCurrentProcess().MainModule?.FileName ?? $"{workingDirectory}/{nameof(MqttSql)}";

            string user = "root";
            int indexOfUser = Array.IndexOf(args, "-u");
            if (indexOfUser == -1) indexOfUser = Array.IndexOf(args, "--user");
            if (indexOfUser != -1)
            {
                if (args.Length == indexOfUser + 1) throw new ArgumentException($"Value expected for the {args[indexOfUser]} argument");
                user = args[indexOfUser + 1];
            }

            Console.WriteLine("Creating systemd service:");
            Console.WriteLine($"\t Directory: {workingDirectory}");
            Console.WriteLine($"\t Executable: {executable}");
            Console.WriteLine($"\t User: {user}");
            if (user == "root")
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
