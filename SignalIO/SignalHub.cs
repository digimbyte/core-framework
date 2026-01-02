using System;
using System.Collections.Generic;

namespace Core.Signals
{
    public class SignalHub
    {
        private readonly Dictionary<Type, ISignal> signals_ = new();

        public T Get<T>() where T : ISignal, new()
        {
            if (signals_.ContainsKey(typeof(T))) return (T)signals_[typeof(T)];
            return (T)BindNewSignal<T>();
        }

        private ISignal BindNewSignal<T>() where T : ISignal, new() => (T)BindNewSignal(typeof(T));

        private ISignal BindNewSignal(Type type)
        {
            var signal = (ISignal)Activator.CreateInstance(type);
            signals_.Add(type, signal);
            return signal;
        }
    }
}
