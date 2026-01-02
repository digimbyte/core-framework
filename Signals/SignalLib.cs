using UnityEngine;

namespace Core.Signals
{
    public abstract class SignalLib
    {
        // example signal
        public class OnEvt : Signal<bool> { }
    }
}
