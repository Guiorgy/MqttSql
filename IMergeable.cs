namespace MqttSql
{
    public interface IMergeable<in T>
    {
        public void Merge(T other);
    }

    public interface IMergeable<in T1, in T2>
    {
        public void Merge(T1 arg1, T2 arg2);
    }

    public interface IMergeable<in T1, in T2, in T3>
    {
        public void Merge(T1 arg1, T2 arg2, T3 arg3);
    }

    public interface IMergeable<in T1, in T2, in T3, in T4>
    {
        public void Merge(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }

    public interface IMergeable<in T1, in T2, in T3, in T4, in T5>
    {
        public void Merge(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    }

    public interface IMergeable<in T1, in T2, in T3, in T4, in T5, in T6>
    {
        public void Merge(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    }
}
