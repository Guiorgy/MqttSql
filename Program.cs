/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if !LINUX
using Topshelf;
using Topshelf.Runtime.DotNetCore;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;
#endif

namespace MqttSql
{
    public static class Program
    {
#if LINUX
        [System.Runtime.InteropServices.DllImport("libc")]
        public static extern uint getuid();
        [System.Runtime.InteropServices.DllImport("libc")]
        public static extern uint geteuid();
#endif

        public static async Task Main(string[] args)
        {
#if !LINUX
            if (IsOSPlatform(Linux))
#endif
            {
                var proccess = Process.GetCurrentProcess();
                var dir = Directory.GetCurrentDirectory();
                if (args.ContainsAny("install", "uninstall", "start", "stop"))
                {
#if LINUX
                    if (getuid() != 0 && geteuid() != 0)
                        throw new Exception("Insufficient rights. Try running with sudo");
#endif
                    if (args.Contains("uninstall") && args.ContainsAny("install", "start"))
                        throw new ArgumentException("Can't use \"uninstall\" with \"install\" or \"start\"");

                    if (args.Contains("install"))
                    {
                        string user = Environment.UserName ?? "root";
                        bool userFlag = false;
                        foreach (string arg in args)
                        {
                            if (userFlag)
                            {
                                user = arg;
                                break;
                            }
                            else if (arg == "-u" || arg == "--user")
                                userFlag = true;
                        }

                        Console.WriteLine("Creating systemd service:");
                        var exe = proccess.MainModule.FileName;
                        Console.WriteLine($"\t Directory: {dir}");
                        Console.WriteLine($"\t Executable: {exe}");
                        Console.WriteLine($"\t User: {user}");
                        if (user == "root")
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"The service was set to run with \"root\" user. "
                                + $"If this is unwanted, either modify the \"{systemdServicePath}\" service file, "
                                + $"or install using the \"-u\" or \"--user\" argument.");
                            Console.ResetColor();
                        }

                        File.WriteAllText(systemdServicePath,
                            systemdServiceText
                                .Replace("{EXE}", exe)
                                .Replace("{DIR}", dir)
                                .Replace("{USER}", user));
                        ExecuteCommand(systemctlPath + " daemon-reload");
                    }
                    if (args.Contains("start"))
                    {
                        ExecuteCommand(systemctlPath + " enable " + systemdServiceName);
                        ExecuteCommand(systemctlPath + " start " + systemdServiceName);
                    }
                    if (args.ContainsAny("stop", "uninstall"))
                    {
                        if (args.Contains("start"))
                            await Task.Delay(60_000);
                        ExecuteCommand(systemctlPath + " stop " + systemdServiceName);
                        ExecuteCommand(systemctlPath + " disable " + systemdServiceName);
                    }
                    if (args.Contains("uninstall"))
                    {
                        if (File.Exists(systemdServicePath))
                            File.Delete(systemdServicePath);
                        ExecuteCommand(systemctlPath + " daemon-reload");
                    }
                    return;
                }
                Service service = new Service(dir, "/");
                await service.StartAsync();
                return;
            }

#if !LINUX
            if (args.Any(arg => arg.Equals("install")))
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
                    service.ConstructUsing(s => new Service());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.RunAsLocalSystem();

                host.SetServiceName("MqttSql");
                host.SetDisplayName("MQTT to SQL");
                host.SetDescription("Subscribes to a MQTT topic and writes the data into a local SQLite database");
                host.EnableServiceRecovery(src => src.RestartService(TimeSpan.FromSeconds(10)));
                host.StartAutomatically();

                host.OnException(e =>
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                });
            });

            Environment.ExitCode = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
#endif
        }

        private static void ExecuteCommand(string command, bool sudo = true)
        {
            Console.WriteLine($"Executing \"{command}\"{(sudo ? " as root" : "")}");
            ProcessStartInfo procStartInfo =
                new ProcessStartInfo(sudo ? "/usr/bin/sudo" : "/bin/bash/", command)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

            using (Process process = new Process())
            {
                process.StartInfo = procStartInfo;
                process.Start();
                process.WaitForExit();

                string result = process.StandardOutput.ReadToEnd();
                Console.WriteLine(result);
            }
        }

        private readonly static string systemctlPath = "/usr/bin/systemctl";
        private readonly static string systemdServiceName = "mqtt-sql";
        private readonly static string systemdServicePath = "/etc/systemd/system/" + systemdServiceName + ".service";
        private readonly static string systemdServiceText =
            @"
            [Unit]
            Description=Subscribes to a MQTT topic and writes the data into a local SQLite database

            [Service]
            Type=simple
            WorkingDirectory={DIR}
            ExecStart={EXE}
            User={USER}
            Restart=always
            Restart=on-failure
            RestartSec=10
            StandardOutput=syslog
            StandardError=syslog
            SyslogIdentifier=%n

            [Install]
            WantedBy=multi-user.target
            ".Replace("            ", "");
    }
}
