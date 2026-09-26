
using Aura.Compat;
using Aura.Internal;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aura
{
    /// <summary>
    /// Access point for Aura settings.
    /// </summary>
    [ExcludeFromPreset]
    public sealed class AuraSettings : ScriptableObject, IAuraSettings
    {
        internal static event Action OnRenderSettingsChanged;

        [SerializeField]
        private SettingsConfig settings = SettingsConfig.Default;

        #region IAuraSettings
        event Action IAuraSettings.OnRenderSettingsChanged
        {
            add
            {
                OnRenderSettingsChanged += value;
            }
            remove
            {
                OnRenderSettingsChanged -= value;
            }
        }

        float IAuraSettings.EdgeSoftenWidth => EdgeSoftenWidth;
        int IAuraSettings.UIBlock3DEdgeDivisions => UIBlock3DEdgeDivisions;
        int IAuraSettings.UIBlock3DCornerDivisions => UIBlock3DCornerDivisions;

        bool IAuraSettings.PackedImagesEnabled => PackedImagesEnabled;
        #endregion

        #region Pass Thru
        /// <summary>
        /// Enables or disables specific warnings that may be logged by Aura.
        /// </summary>
        public static LogFlags LogFlags
        {
            get => Instance.settings.LogFlags;
            set
            {
                Instance.settings.LogFlags = value;
                Instance.MarkDirty(false);
            }
        }

        /// <summary>
        /// Can be used to globally disable <see cref="ImagePackMode.Packed">Packed</see> images.
        /// </summary>
        /// <seealso cref="ImagePackMode"/>
        public static bool PackedImagesEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.PackedImagesEnabled;
            set
            {
                if (Instance.settings.PackedImagesEnabled == value)
                {
                    return;
                }
                Instance.settings.PackedImagesEnabled = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// How to copy <see cref="ImagePackMode.Packed">Packed</see> images. See <see cref="Aura.PackedImageCopyMode"/> for more info.
        /// </summary>
        public static PackedImageCopyMode PackedImageCopyMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.PackedImageCopyMode;
            set
            {
                if (Instance.settings.PackedImageCopyMode == value)
                {
                    return;
                }
                Instance.settings.PackedImageCopyMode = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// Enables/disables super sampling of text, which can drastically reduce "shimmering" artifacts on text on certain platforms (such as VR).
        /// </summary>
        public static bool SuperSampleText
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.SuperSampleText;
            set
            {
                if (Instance.settings.SuperSampleText == value)
                {
                    return;
                }
                Instance.settings.SuperSampleText = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The width (in pixels) of the anti-aliasing (edge softening) performed at <see cref="UIBlock2D"/> and <see cref="ClipMask"/> boundaries.
        /// Edge softening can also be disabled per-<see cref="UIBlock2D"/> using <see cref="UIBlock2D.SoftenEdges"/>.
        /// </summary>
        public static float EdgeSoftenWidth
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.EdgeSoftenWidth;
            set
            {
                if (Instance.settings.EdgeSoftenWidth == value)
                {
                    return;
                }
                Instance.settings.EdgeSoftenWidth = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The number of divisions on the <see cref="UIBlock3D"/> mesh when applying the <see cref="UIBlock3D.CornerRadius"/> property.
        /// A larger value leads to a higher quality mesh, but the mesh will contain more vertices.
        /// </summary>
        public static int UIBlock3DCornerDivisions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.UIBlock3DCornerDivisions;
            set
            {
                if (Instance.settings.UIBlock3DCornerDivisions == value)
                {
                    return;
                }
                Instance.settings.UIBlock3DCornerDivisions = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The number of divisions on the <see cref="UIBlock3D"/> mesh when applying the <see cref="UIBlock3D.EdgeRadius"/> property.
        /// A larger value leads to a higher quality mesh, but the mesh will contain more vertices.
        /// </summary>
        public static int UIBlock3DEdgeDivisions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Instance.settings.UIBlock3DEdgeDivisions;
            set
            {
                if (Instance.settings.UIBlock3DEdgeDivisions == value)
                {
                    return;
                }
                Instance.settings.UIBlock3DEdgeDivisions = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The lighting models to include in builds for <see cref="UIBlock2D"/>. If a lighting model is not included in a build but a <see cref="UIBlock"/> 
        /// tries to use it, the block will fall back to <see cref="LightingModel.Unlit"/>.
        /// </summary>
        public static LightingModelBuildFlag UIBlock2DLightingModels
        {
            get => Instance.settings.UIBlock2DLightingModels;
            set
            {
                Instance.settings.UIBlock2DLightingModels = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The lighting models to include in builds for <see cref="UIBlock3D"/>. If a lighting model is not included in a build but a <see cref="UIBlock"/> 
        /// tries to use it, the block will fall back to <see cref="LightingModel.Unlit"/>.
        /// </summary>
        public static LightingModelBuildFlag UIBlock3DLightingModels
        {
            get => Instance.settings.UIBlock3DLightingModels;
            set
            {
                Instance.settings.UIBlock3DLightingModels = value;
                Instance.MarkDirty(true);
            }
        }

        /// <summary>
        /// The lighting models to include in builds for <see cref="TextBlock"/>. If a lighting model is not included in a build but a <see cref="UIBlock"/> 
        /// tries to use it, the block will fall back to <see cref="LightingModel.Unlit"/>.
        /// </summary>
        public static LightingModelBuildFlag TextBlockLightingModels
        {
            get => Instance.settings.TextBlockLightingModels;
            set
            {
                Instance.settings.TextBlockLightingModels = value;
                Instance.MarkDirty(true);
            }
        }
        #endregion

        #region Controls
        /// <summary>
        /// The Aura button control prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock ButtonPrefab = null;
        /// <summary>
        /// The Aura toggle control prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock TogglePrefab = null;
        /// <summary>
        /// The Aura slider control prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock SliderPrefab = null;
        /// <summary>
        /// The Aura dropdown control prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock DropdownPrefab = null;
        /// <summary>
        /// The Aura text field prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock TextFieldPrefab = null;
        /// <summary>
        /// The Aura scroll view prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock ScrollViewPrefab = null;
        /// <summary>
        /// The Aura UI root prefab to instantiate in editor from the GameObject menu (and right-click in hierarchy window).
        /// </summary>
        public UIBlock UIRootPrefab = null;
        #endregion

        internal void MarkDirty(bool fireEvent, bool markDirty = true)
        {
            Internal.AuraSettings.Config = UnsafeUtility.As<SettingsConfig, Internal.SettingsConfig>(ref _instance.settings);

            if (fireEvent)
            {
                OnRenderSettingsChanged?.Invoke();
            }

            if (markDirty)
            {
                AuraApplication.MarkDirty(this);
            }
        }

        #region Singleton
        internal static bool Initialized => _instance == null ? TryInitialize() : true;

        private static AuraSettings _instance = null;
        internal static AuraSettings Instance
        {
            get
            {
                if (_instance == null && !TryInitialize())
                {
                    throw new Exception("Failed to load Aura settings. Please ensure Aura was imported correctly.");
                }
                return _instance;
            }
        }

        private static bool TryInitialize()
        {
            // This only finds assets that have already been loaded
            AuraSettings[] settings = Resources.FindObjectsOfTypeAll<AuraSettings>();
            if (settings.Length != 0)
            {
                _instance = settings[0];
            }
            else
            {
                // Hasn't been loaded yet, so we need to load it
                _instance = Resources.Load<AuraSettings>("AuraSettings");
            }

            if (_instance != null)
            {
                // Fire this event in case the data stores initialize after the settings get loaded, so they can get
                // the correct initial values
                _instance.MarkDirty(true);
            }

            return _instance != null;
        }
        #endregion
    }
}

