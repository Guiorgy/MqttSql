using System;
using System.Threading.Tasks;

namespace MqttSql;

public static partial class Program
{
    public static async Task Main()
    {
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
            if (!serviceStopped)
            {
                Console.WriteLine("Received SIGTERM");
                service.Stop();
            }
        };

        await service.StartAsync();
    }
}
