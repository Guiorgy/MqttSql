using System.Text;

namespace MqttSql.src.Utility
{
    public interface IAppendStringBuilder
    {
        StringBuilder AppendStringBuilder(StringBuilder builder);

        string? ToString()
        {
            return AppendStringBuilder(new StringBuilder()).ToString();
        }
    }
}
