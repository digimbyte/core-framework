
using Aura.Editor.Utilities;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Aura.Editor
{
    internal static class AddObjectMenus
    {
        [MenuItem("GameObject/Aura/UIBlock 2D", false, 8)]
        private static void AddUIBlock2D(MenuCommand menuCommand)
        {
            Add<UIBlock2D, Tools.UIBlockTool>(menuCommand, "UIBlock2D").CopyToDataStore();
        }

        [MenuItem("GameObject/Aura/TextBlock", false, 9)]
        private static void AddTextBlock(MenuCommand menuCommand)
        {
            Add<TextBlock, Tools.UIBlockTool>(menuCommand, "TextBlock").CopyToDataStore();
        }

        [MenuItem("GameObject/Aura/UIBlock 3D", false, 10)]
        private static void AddUIBlock3D(MenuCommand menuCommand)
        {
            Add<UIBlock3D, Tools.UIBlockTool>(menuCommand, "UIBlock3D").CopyToDataStore();
        }

        [MenuItem("GameObject/Aura/UIBlock", false, 11)]
        private static void AddUIBlock(MenuCommand menuCommand)
        {
            Add<UIBlock, Tools.UIBlockTool>(menuCommand, "UIBlock").CopyToDataStore();
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Button", false, 112)]
#else
        [MenuItem("GameObject/Aura/Controls/Button", false, 12)]
#endif
        private static void AddButton(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.ButtonPrefab == null)
            {
                Debug.LogError("Button prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.ButtonPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Toggle", false, 113)]
#else
        [MenuItem("GameObject/Aura/Controls/Toggle", false, 13)]
#endif
        private static void AddToggle(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.TogglePrefab == null)
            {
                Debug.LogError("Toggle prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.TogglePrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Slider", false, 114)]
#else
        [MenuItem("GameObject/Aura/Controls/Slider", false, 14)]
#endif
        private static void AddSlider(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.SliderPrefab == null)
            {
                Debug.LogError("Slider prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.SliderPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Dropdown", false, 115)]
#else
        [MenuItem("GameObject/Aura/Controls/Dropdown", false, 15)]
#endif
        private static void AddDropdown(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.DropdownPrefab == null)
            {
                Debug.LogError("Dropdown prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.DropdownPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Text Field", false, 116)]
#else
        [MenuItem("GameObject/Aura/Controls/Text Field", false, 16)]
#endif
        private static void AddTextField(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.TextFieldPrefab == null)
            {
                Debug.LogError("Text Field prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.TextFieldPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/Scroll View", false, 117)]
#else
        [MenuItem("GameObject/Aura/Controls/Scroll View", false, 17)]
#endif
        private static void AddScrollView(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.ScrollViewPrefab == null)
            {
                Debug.LogError("Scroll View prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.ScrollViewPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Aura/UI Root", false, 118)]
#else
        [MenuItem("GameObject/Aura/Controls/UI Root", false, 18)]
#endif
        private static void AddUIRoot(MenuCommand menuCommand)
        {
            if (AuraSettings.Instance.UIRootPrefab == null)
            {
                Debug.LogError("UI Root prefab source unassigned. A prefab can be assigned under Project Settings > Aura.");
                return;
            }

            InstantiatePrefab(AuraSettings.Instance.UIRootPrefab, menuCommand);
        }

        private static void InstantiatePrefab(UIBlock uiBlock, MenuCommand menuCommand)
        {
            // Just clone in play mode, since that's more consistent with
            // Unity behavior of dragging a prefab into the scene while playing
            bool createPrefab = !Application.IsPlaying(UnityEditor.SceneManagement.StageUtility.GetCurrentStage());

            GameObject go =  createPrefab ? (PrefabUtility.InstantiatePrefab(uiBlock) as UIBlock).gameObject : Object.Instantiate(uiBlock).gameObject;

            if (menuCommand.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else
            {
                UnityEditor.SceneManagement.StageUtility.PlaceGameObjectInCurrentStage(go);
            }

            if (go != null)
            {
                go.transform.SetAsLastSibling();
            }

            Selection.activeObject = go;
        }

        private static T Add<T, TTool>(MenuCommand menuCommand, string name = "GameObject") where T : Component where TTool : UnityEditor.EditorTools.EditorTool
        {
            // Create a custom game object
            GameObject go = new GameObject(name);

            if (menuCommand.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else
            {
                UnityEditor.SceneManagement.StageUtility.PlaceGameObjectInCurrentStage(go);
            }

            T addedComponent = go.AddComponent<T>();

            Preset[] presets = Preset.GetDefaultPresetsForObject(addedComponent);

            if (presets != null && presets.Length > 0 && presets[0] != null)
            {
                presets[0].ApplyTo(addedComponent);
            }

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeGameObject = go;

            EditorApplication.delayCall += () =>
            {
                if (!ActiveEditorUtils.TryGetActiveEditorTargetType<T>(out _))
                {
                    // editor window not active, which can happen if the inspector
                    // is locked to another object or if the single-frame delay
                    // is insufficient (thanks Unity), so the tool can't be set active.
                    return;
                }

                UnityEditor.EditorTools.ToolManager.SetActiveTool<TTool>();
            };

            return addedComponent;
        }
    }
}

