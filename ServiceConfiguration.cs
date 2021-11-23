using System;

namespace MqttSql
{
    public class ServiceConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Topic { get; set; }
        public string Table { get; set; }

        public override string ToString()
        {
            return
                $"{'{'}{Environment.NewLine}" +
                $"\tHost:{Host}{Environment.NewLine}" +
                $"\tPort:{Port}{Environment.NewLine}" +
                $"\tUser:{User}{Environment.NewLine}" +
                $"\tPassword:{Password}{Environment.NewLine}" +
                $"\tTopic:{Topic}{Environment.NewLine}" +
                $"\tTable:{Table}{Environment.NewLine}" +
                $"{'}'}{Environment.NewLine}";
        }

        public string ToSafeString()
        {
            return
                $"{'{'}{Environment.NewLine}" +
                $"\tHost:{Host}{Environment.NewLine}" +
                $"\tPort:{Port}{Environment.NewLine}" +
                $"\tUser:{User}{Environment.NewLine}" +
                $"\tPassword:{new string('*', Password.Length)}{Environment.NewLine}" +
                $"\tTopic:{Topic}{Environment.NewLine}" +
                $"\tTable:{Table}{Environment.NewLine}" +
                $"{'}'}{Environment.NewLine}";
        }
    }
}
