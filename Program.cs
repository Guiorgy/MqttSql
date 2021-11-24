#if !LOG && DEBUG
#define LOG
#endif

using System;
using System.Linq;
using Topshelf;

namespace MqttSql
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Any(arg => arg.Equals("install"))) {
                string home = System.IO.Directory.GetCurrentDirectory();
#if LOG
                Console.WriteLine($"Setting home directory to \"{home}\"");
#endif
                Environment.SetEnvironmentVariable("MqttSqlHome", home, EnvironmentVariableTarget.Machine);
            }

            var exitCode = HostFactory.Run(host =>
            {
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

#if LOG
                host.OnException(e =>
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                });
#endif
            });

            Environment.ExitCode = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}
