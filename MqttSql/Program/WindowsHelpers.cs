using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace MqttSql.Program;

public static class WindowsHelpers
{
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "System component")]
    private const string serviceControlManagerPath = @"C:\Windows\System32\sc.exe";

    private static async Task<int> Execute(string executable, params IEnumerable<string> arguments)
    {
        Console.WriteLine($"Executing \"{executable} {string.Join(" ", arguments)}\"");

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

    private enum ServiceControlSubcommand
    {
        Query,
        Create,
        Delete,
        Start,
        Stop
    }

    private static async Task<int> ExecuteServiceControl(ServiceControlSubcommand subcommand, string serviceName, params IEnumerable<string> options)
    {
        string subcommandStr = subcommand switch
        {
            ServiceControlSubcommand.Query or ServiceControlSubcommand.Create or ServiceControlSubcommand.Delete
                or ServiceControlSubcommand.Start or ServiceControlSubcommand.Stop => subcommand.ToString().ToLower(),
            _ => throw new UnreachableException($"{nameof(ServiceControlSubcommand)} enum value {subcommand} not handled")
        };

        int exitCode = await Execute(serviceControlManagerPath, [subcommandStr, serviceName, .. options]);
        if (exitCode != 0)
        {
            string message = subcommand switch
            {
                ServiceControlSubcommand.Query => "query status of",
                ServiceControlSubcommand.Create or ServiceControlSubcommand.Delete
                    or ServiceControlSubcommand.Start or ServiceControlSubcommand.Stop => subcommand.ToString().ToLower(),
                _ => throw new UnreachableException($"{nameof(ServiceControlSubcommand)} enum value {subcommand} not handled")
            };

            if (serviceName.Length != 0)
                serviceName = " " + serviceName;

            await Console.Error.WriteLineAsync($"Failed to {message}{serviceName} Windows service");
        }

        return exitCode;
    }

    public static async Task<int> QueryServiceStatus(string name) => await ExecuteServiceControl(ServiceControlSubcommand.Query, name);

    public static async Task<int> InstallService(string name, string? displayName = null, string? config = null, string? logfile = null, string? sqliteDir = null)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var executable = Process.GetCurrentProcess().MainModule?.FileName ?? $"{workingDirectory}/{nameof(MqttSql)}";

        config ??= workingDirectory + "/config.json";
        logfile ??= workingDirectory + "/logs.txt";
        sqliteDir ??= workingDirectory;
        var executableArgs = $@"--config=\""{config}\"" --logfile=\""{logfile}\"" --sqlite-dir=\""{sqliteDir}\""";

        Console.WriteLine("Creating Windows service:");
        Console.WriteLine($"\t Name: {name}");
        Console.WriteLine($"\t Display Name: {displayName ?? name}");
        Console.WriteLine($"\t Binary: {executable}");
        Console.WriteLine($"\t Arguments: {executableArgs}");
        return await ExecuteServiceControl(
            ServiceControlSubcommand.Create,
            name,
            "DisplayName=", displayName ?? name,
            "binpath=", $@"""\""{executable}\"" {executableArgs}""",
            "start=", "auto"
        );
    }

    public static async Task<int> UninstallService(string name) => await ExecuteServiceControl(ServiceControlSubcommand.Delete, name);

    public static async Task<int> StartService(string name) => await ExecuteServiceControl(ServiceControlSubcommand.Start, name);

    public static async Task<int> StopService(string name) => await ExecuteServiceControl(ServiceControlSubcommand.Stop, name);
}
