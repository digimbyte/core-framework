namespace Core.Signals
{
    public static class Signals
    {
        private static readonly SignalHub Hub = new SignalHub();

        public static T Get<T>() where T : ISignal, new() => Hub.Get<T>();
    }
}
