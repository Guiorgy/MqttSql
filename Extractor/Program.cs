#if !LOG && DEBUG
#define LOG
#endif

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Extractor
{
    public static class Program
    {
        private static void DebugLog(string message)
        {
#if LOG
            System.Console.WriteLine(message);
#endif
        }

        private static void DebugWait()
        {
#if LOG
            System.Console.Out.Flush();
            System.Console.ReadLine();
#endif
        }

        private static readonly string DotNetRuntime = "DotNetCore-3.1.21-win-x64.exe";
        private static readonly string ServiceConfig = "config.json";
        private static readonly string[] Dependencies = new string[] {
                "Microsoft.Win32.SystemEvents.dll",
                "MQTTnet.dll",
                "MqttSql.deps.json",
                "MqttSql.dll",
                "MqttSql.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                "SQLite.Interop.dll",
                "System.Data.SQLite.dll",
                "System.Diagnostics.EventLog.dll",
                "System.ServiceProcess.ServiceController.dll",
                "Topshelf.dll",
                "TopShelf.ServiceInstaller.dll"
            };
        private static readonly string Service = "MqttSql.exe";

        public static void Main(string[] args)
        {
            string targetDir = args.Length != 0 ? args[0] : @"C:\Program Files\MqttSql\";
            DebugLog($"Target directory set to \"{targetDir}\"");

            if (!Directory.Exists(targetDir))
            {
                DebugLog($"Creating directory \"{targetDir}\"");
                Directory.CreateDirectory(targetDir);
            }

            string dotnetinfo = GetCommandOutput("dotnet --info", 10);
            if (dotnetinfo == null || !dotnetinfo.Contains("3.1.21"))
            {
                if (!File.Exists(targetDir + (targetDir.EndsWith("\\") ? "" : "\\") + DotNetRuntime))
                {
                    DebugLog($"Extracting the .Net Runtime \"{DotNetRuntime}\"");
                    ExtractResourceFile("Extractor", "Embeeded", DotNetRuntime, targetDir);
                }
                DebugLog($"Installing \"{DotNetRuntime}\" .Net Runtime");
                RunExecutableFor(targetDir, DotNetRuntime, "/install /quiet /norestart", 30);
            }

            DebugLog($"Extracting the service configuration configuration \"{ServiceConfig}\"");
            ExtractResourceFile("Extractor", "Embeeded", ServiceConfig, targetDir);

            DebugLog($"Extracting the service dependencies");
            foreach (string dep in Dependencies)
            {
                DebugLog($"Extracting dependency \"{dep}\"");
                ExtractResourceFile("Extractor", "Embeeded", dep, targetDir);
            }

            DebugLog($"Extracting the service executable \"{Service}\"");
            ExtractResourceFile("Extractor", "Embeeded", Service, targetDir);
            DebugLog($"Installing \"{Service}\" service");
            RunExecutable(targetDir, Service, " install start");
        }

        private static void ExtractResourceFile(string resourceNamespace, string resourceFilePath, string resourceName, string targetDirectory)
        {
            string embeededResourcePath = resourceNamespace + '.' + resourceFilePath + (resourceFilePath != "" ? "." : "") + resourceName;
            string targetPath = targetDirectory + (targetDirectory.EndsWith("\\") ? "" : "\\") + resourceName;
            DebugLog($"Extracting \"{embeededResourcePath}\" embeeded resource to \"{targetPath}\"");

            Assembly assembly = Assembly.GetCallingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(embeededResourcePath))
            using (BinaryReader reader = new BinaryReader(stream))
            using (FileStream fileStream = new FileStream(targetPath, FileMode.OpenOrCreate))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                writer.Write(reader.ReadBytes((int)stream.Length));
                DebugLog($"Extracted {stream.Length} bytes");
            }
        }

        private static void ExecuteCommand(string command)
        {
            DebugLog($"Executing {command}");
            ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command)
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

#if LOG
                string result = process.StandardOutput.ReadToEnd();
                DebugLog(result);
                DebugWait();
#endif
            }
        }

        private static string GetCommandOutput(string command, int timeout)
        {
            DebugLog($"Executing {command}");
            string result = null;
            Thread thread = new Thread(new ThreadStart(() =>
            {
                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command)
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

                    result = process.StandardOutput.ReadToEnd();
                    DebugLog(result);
                }
            }));
            thread.Start();
            while (timeout > 0)
            {
                Thread.Sleep(1000);
                if (!thread.IsAlive) break;
                timeout--;
            }
            if (thread.IsAlive)
            {
                thread.Abort();
                DebugLog("Aborting!");
            }
            return result;
        }

        private static void RunExecutable(string directory, string exe, string args)
        {
            using (Process process = new Process())
            {
                process.StartInfo.WorkingDirectory = directory;
                process.StartInfo.FileName = directory + (directory.EndsWith("\\") ? "" : "\\") + exe;
                process.StartInfo.Arguments = args;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#if LOG
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
#endif

                DebugLog($"Running Executable \"{process.StartInfo.FileName}\" with \"{args}\" arguments");

                process.Start();
                process.WaitForExit();

#if LOG
                string result = process.StandardOutput.ReadToEnd();
                DebugLog(result);
#endif
            }
        }

        private static void RunExecutableFor(string directory, string exe, string args, int seconds)
        {
            Thread thread = new Thread(new ThreadStart(() => RunExecutable(directory, exe, args)));
            thread.Start();
            while (seconds > 0)
            {
                Thread.Sleep(1000);
                if (!thread.IsAlive) break;
                seconds--;
            }
            if (thread.IsAlive)
            {
                thread.Abort();
                DebugLog("Aborting!");
            }
        }
    }
}
