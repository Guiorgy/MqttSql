namespace MqttSql
{
    public interface IMergeable<in T>
    {
        public void Merge(T other);
    }
}
