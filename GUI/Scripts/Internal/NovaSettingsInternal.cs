
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Aura.Internal
{
    internal interface IAuraSettings
    {
        event Action OnRenderSettingsChanged;

        float EdgeSoftenWidth { get; }
        int UIBlock3DCornerDivisions { get; }
        int UIBlock3DEdgeDivisions { get; }
        bool PackedImagesEnabled { get; }
    }

    internal class AuraSettings
    {
        public static event Action OnInitRequested = null;

        private static IAuraSettings instance = null;

        private static readonly SharedStatic<SettingsConfig> config = SharedStatic<SettingsConfig>.GetOrCreate<AuraSettings>();

        public static void Dispose()
        {
            instance = null;
            OnInitRequested = null;
        }

        public static void Init(IAuraSettings settings)
        {
            instance = settings;
            instance.OnRenderSettingsChanged += _onRenderSettingsChanged;
            _onRenderSettingsChanged = null;
        }

        private static event Action _onRenderSettingsChanged = null;
        public static event Action OnRenderSettingsChanged
        {
            add
            {
                if (Instance == null)
                {
                    _onRenderSettingsChanged += value;
                    return;
                }

                Instance.OnRenderSettingsChanged += value;
            }
            remove
            {
                if (Instance == null)
                {
                    return;
                }

                Instance.OnRenderSettingsChanged -= value;
            }
        }

        public static ref SettingsConfig Config
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref config.Data;
            }
        }

        public static bool Initialized => Instance != null;

        private static IAuraSettings Instance
        {
            get
            {
                if (instance as UnityEngine.Object == null)
                {
                    OnInitRequested?.Invoke();
                }

                return instance;
            }
        }

        public static float EdgeSoftenWidth => Instance.EdgeSoftenWidth;
        public static int UIBlock3DCornerDivisions => Instance.UIBlock3DCornerDivisions;
        public static int UIBlock3DEdgeDivisions => Instance.UIBlock3DEdgeDivisions;
        public static bool PackedImagesEnabled => Instance.PackedImagesEnabled;
    }
}
