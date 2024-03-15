using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MqttSql.Program;

public static class LinuxHelpers
{
    private const string systemctlPath = "/usr/bin/systemctl";
    private const string systemdServicesPath = "/etc/systemd/system/";

    public static async Task<int> ExecuteCommand(string command, bool sudo = true)
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

        process.Start();
        await process.WaitForExitAsync();

        string result = await process.StandardOutput.ReadToEndAsync();

        Console.WriteLine(result);

        return process.ExitCode;
    }

    public enum SystemdSubcommand
    {
        DaemonReload,
        Status,
        Enable,
        Disable,
        Start,
        Stop
    }

    public static async Task<int> ExecuteSystemd(SystemdSubcommand subcommand, string serviceName = "", bool sudo = true)
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

            if (serviceName.Length != 0)
                serviceName = " " + serviceName;

            Console.WriteLine($"Failed to {message}{serviceName} systemd service");
        }

        return exitCode;
    }

    public static string GetSystemdServiceUnitPath(string serviceName) => $"{systemdServicesPath}/{serviceName}.service";

    public static string GetSystemdServiceUnitContent(string executablePath, string executableArgs = "", string? workingDirectory = null, string description = "",
        string type = "exec", string user = "root", string restart = "on-failure", string restartDelay = "10s")
    {
        return 
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
}
