using UnityEditor;
using UnityEngine;

namespace Core.Registry.Editor
{
    public static class RegistryEditorMenu
    {
        [MenuItem("GameObject/Core/Registries/Registry Manager", false, 10)]
        private static void CreateRegistryManager(MenuCommand menuCommand)
        {
            // Create a new GameObject with RegistryManager component
            GameObject go = new GameObject("RegistryManager");
            go.AddComponent<RegistryManager>();
            
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create Registry Manager");
            
            // Select the newly created GameObject
            Selection.activeGameObject = go;
            
            Debug.Log("[Editor] Created RegistryManager GameObject");
        }
    }
}
