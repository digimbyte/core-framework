using System;

namespace Core.Signals
{
    public class Signal : ISignal
    {
        private Action _callback;

        public void AddListener(Action action) => _callback += action;
        public void RemoveListener(Action action) => _callback -= action;
        public void Dispatch() => _callback?.Invoke();
    }

    public class Signal<T> : ISignal
    {
        private Action<T> _callback;

        public void AddListener(Action<T> action) => _callback += action;
        public void RemoveListener(Action<T> action) => _callback -= action;
        public void Dispatch(T t) => _callback?.Invoke(t);
    }

    public class Signal<T1, T2> : ISignal
    {
        private Action<T1, T2> _callback;

        public void AddListener(Action<T1, T2> action) => _callback += action;
        public void RemoveListener(Action<T1, T2> action) => _callback -= action;
        public void Dispatch(T1 t1, T2 t2) => _callback?.Invoke(t1, t2);
    }

    public class Signal<T1, T2, T3> : ISignal
    {
        private Action<T1, T2, T3> _callback;

        public void AddListener(Action<T1, T2, T3> action) => _callback += action;
        public void RemoveListener(Action<T1, T2, T3> action) => _callback -= action;
        public void Dispatch(T1 t1, T2 t2, T3 t3) => _callback?.Invoke(t1, t2, t3);
    }

    public class Signal<T1, T2, T3, T4> : ISignal
    {
        private Action<T1, T2, T3, T4> _callback;

        public void AddListener(Action<T1, T2, T3, T4> action) => _callback += action;
        public void RemoveListener(Action<T1, T2, T3, T4> action) => _callback -= action;
        public void Dispatch(T1 t1, T2 t2, T3 t3, T4 t4) => _callback?.Invoke(t1, t2, t3, t4);
    }
}
