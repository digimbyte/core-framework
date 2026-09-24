using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RegionViewer
{
    /// <summary>
    /// Creates a read-only debug overlay, activated by holding F12 for five seconds.
    /// </summary>
    public static class FpsLimiter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Apply()
        {
            var fpsDisplay = new GameObject("Debug Overlay");
            fpsDisplay.AddComponent<FpsDisplay>();
            Object.DontDestroyOnLoad(fpsDisplay);
        }
    }

    /// <summary>
    /// Displays runtime diagnostics without changing application settings.
    /// </summary>
    public class FpsDisplay : MonoBehaviour
    {
        private float deltaTime;
        private GUIStyle style;
        private bool overlayEnabled;
        private bool holdConsumed;
        private float holdStartedAt = -1f;
        private string deviceInfo;

        void Start()
        {
            style = new GUIStyle();
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = 18;
            style.normal.textColor = Color.white;
            style.wordWrap = true;
            deviceInfo = $"Platform: {Application.platform} | Unity {Application.unityVersion}\n" +
                         $"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} logical cores)\n" +
                         $"GPU: {SystemInfo.graphicsDeviceName}\n" +
                         $"Graphics API: {SystemInfo.graphicsDeviceType}\n" +
                         $"Device memory: {SystemInfo.systemMemorySize} MB RAM / {SystemInfo.graphicsMemorySize} MB VRAM";
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            bool f12Held = Keyboard.current != null && Keyboard.current.f12Key.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool f12Held = Input.GetKey(KeyCode.F12);
#else
            bool f12Held = false;
#endif
            if (!f12Held || !Application.isFocused)
            {
                holdStartedAt = -1f;
                holdConsumed = false;
            }
            else if (!holdConsumed)
            {
                if (holdStartedAt < 0f)
                    holdStartedAt = Time.realtimeSinceStartup;

                if (Time.realtimeSinceStartup - holdStartedAt >= 5f)
                {
                    holdConsumed = true;
                    if (!overlayEnabled)
                    {
                        deltaTime = Time.unscaledDeltaTime;
                        overlayEnabled = true;
                    }
                }
            }

            if (!overlayEnabled)
                return;

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        }

        void OnGUI()
        {
            if (!overlayEnabled)
                return;

            float msec = deltaTime * 1000.0f;
            float fps = deltaTime > 0f ? 1.0f / deltaTime : 0f;
            const float megabyte = 1024f * 1024f;
            string cap = Application.targetFrameRate > 0
                ? Application.targetFrameRate.ToString()
                : $"Platform default ({Application.targetFrameRate})";
            string vsync = QualitySettings.vSyncCount == 0
                ? "Off"
                : $"Every {QualitySettings.vSyncCount} refresh(es)";
            string text = $"DEBUG\nFPS (smoothed): {fps:0.0}\n" +
                          $"Frame time (smoothed): {msec:0.00} ms\n" +
                          $"Last frame: {Time.unscaledDeltaTime * 1000f:0.00} ms\n" +
                          $"Configured FPS cap: {cap}\nVSync: {vsync}\n" +
                          $"Render size: {Screen.width} x {Screen.height} | {Screen.fullScreenMode}\n" +
                          $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}\n" +
                          $"Scene: {SceneManager.GetActiveScene().name}\n" +
                          $"Time scale: {Time.timeScale:0.##} | Fixed step: {Time.fixedDeltaTime * 1000f:0.##} ms\n" +
                          $"Runtime: {Time.realtimeSinceStartup:0.0} s | Frame: {Time.frameCount}\n" +
                          $"Unity allocated: {Profiler.GetTotalAllocatedMemoryLong() / megabyte:0.0} MB\n" +
                          $"Unity reserved: {Profiler.GetTotalReservedMemoryLong() / megabyte:0.0} MB\n" +
                          $"Managed heap used: {Profiler.GetMonoUsedSizeLong() / megabyte:0.0} MB\n" +
                          deviceInfo;

            float width = Mathf.Min(600f, Screen.width - 20f);
            float height = style.CalcHeight(new GUIContent(text), width - 20f);
            float left = Screen.width - width - 10f;
            GUI.Box(new Rect(left, 10, width, height + 55f), GUIContent.none);
            GUI.Label(new Rect(left + 10f, 20f, width - 20f, height), text, style);
            if (GUI.Button(new Rect(left + width - 105f, height + 30f, 95f, 25f), "Disable"))
                overlayEnabled = false;
        }

        void OnDisable()
        {
            overlayEnabled = false;
            holdStartedAt = -1f;
            holdConsumed = false;
        }
    }
}
