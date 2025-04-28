/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MqttSql.Program;

public static class LinuxHelpers
{
    private const string systemctlPath = "/usr/bin/systemctl";
    private const string systemdServicesPath = "/etc/systemd/system/";
    private static string GetSystemdServiceUnitPath(string serviceName) => $"{systemdServicesPath}/{serviceName}.service";

    private static async Task<int> ExecuteCommand(string command, bool sudo = true)
    {
        string executable = sudo ? "/usr/bin/sudo" : "/bin/sh/";
        string arguments = sudo ? command : $"-c '{command}'";

        Console.WriteLine($"Executing \"{executable} {arguments}\"");

        using Process process = new();

        process.StartInfo = new(executable, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ = process.Start();
        await process.WaitForExitAsync();

        string result = await process.StandardOutput.ReadToEndAsync();

        Console.WriteLine(result);

        return process.ExitCode;
    }

    private enum SystemdSubcommand
    {
        DaemonReload,
        Status,
        Enable,
        Disable,
        Start,
        Stop
    }

    private static async Task<int> ExecuteSystemd(SystemdSubcommand subcommand, string serviceName, bool sudo = true)
    {
        string subcommandStr = subcommand switch
        {
            SystemdSubcommand.DaemonReload => "daemon-reload",
            SystemdSubcommand.Status or SystemdSubcommand.Enable or SystemdSubcommand.Disable
                or SystemdSubcommand.Start or SystemdSubcommand.Stop => subcommand.ToString().ToLower(),
            _ => throw new UnreachableException($"{nameof(SystemdSubcommand)} enum value {subcommand} not handled")
        };

        int exitCode = await ExecuteCommand($"{systemctlPath} {subcommandStr} {serviceName}", sudo);
        if (exitCode != 0)
        {
            string message = subcommand switch
            {
                SystemdSubcommand.DaemonReload => "reload",
                SystemdSubcommand.Status => "get status of",
                SystemdSubcommand.Enable or SystemdSubcommand.Disable or SystemdSubcommand.Start or SystemdSubcommand.Stop => subcommand.ToString().ToLower(),
                _ => throw new UnreachableException($"{nameof(SystemdSubcommand)} enum value {subcommand} not handled")
            };

            await Console.Error.WriteLineAsync($"Failed to {message}{(serviceName.Length != 0 ? $" {serviceName}" : "")} systemd service");
        }

        return exitCode;
    }

    public static async Task<int> PrintServiceStatus(string name) => await ExecuteSystemd(SystemdSubcommand.Status, name);

    public static async Task<int> InstallService(string name, string description, string? user = null)
    {
        var systemdServiceUnitPath = GetSystemdServiceUnitPath(name);
        if (File.Exists(systemdServiceUnitPath)) return 0;

        var workingDirectory = Directory.GetCurrentDirectory();
        var executable = Process.GetCurrentProcess().MainModule?.FileName ?? $"{workingDirectory}/{nameof(MqttSql)}";

        user ??= "root";

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
                    description: description
                )
            );
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.ToString());
            return -1;
        }

        return await ExecuteSystemd(SystemdSubcommand.DaemonReload, name);
    }

    public static async Task<int> UninstallService(string name)
    {
        int exitCode = await StopService(name);
        if (exitCode == 0) File.Delete(GetSystemdServiceUnitPath(name));
        return exitCode;
    }

    public static async Task<int> StartService(string name)
    {
        int exitCode = await ExecuteSystemd(SystemdSubcommand.Enable, name);
        if (exitCode == 0) exitCode = await ExecuteSystemd(SystemdSubcommand.Start, name);
        return exitCode;
    }

    public static async Task<int> StopService(string name)
    {
        int exitCode = await ExecuteSystemd(SystemdSubcommand.Disable, name);
        if (exitCode == 0) exitCode = await ExecuteSystemd(SystemdSubcommand.Stop, name);
        return exitCode;
    }

    private static string GetSystemdServiceUnitContent(
        string executablePath,
        string executableArgs = "",
        string? workingDirectory = null,
        string description = "",
        string type = "exec",
        string user = "root",
        string restart = "on-failure",
        string restartDelay = "10s"
    ) =>
        $"""
        [Unit]
        Description={description}
        
        [Service]
        Type={type}
        WorkingDirectory={workingDirectory ?? Path.GetDirectoryName(executablePath)}
        ExecStart={$"{executablePath} {executableArgs}"}
        User={user}
        Restart={restart}
        RestartSec={restartDelay}
        StandardOutput=syslog
        StandardError=syslog
        SyslogIdentifier=%n
        
        [Install]
        WantedBy=default.target
        """;
}
