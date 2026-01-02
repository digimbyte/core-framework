using Core.InputManager;
using Core.ItemSystem.Core;
using Core.RTEditorExtensions.ToolOverrideSystem.Models;
using UnityEngine;

namespace Core.Signals
{
    public abstract class SignalLib
    {
        // example signal
        public class OnEvt : Signal<bool> { }
    }
}
