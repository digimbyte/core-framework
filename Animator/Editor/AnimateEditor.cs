using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Animator;

namespace Animator
{
    public struct NestedEntry { public string path; public string display; public string typeName; }

    [CustomEditor(typeof(Animate))]
    public partial class AnimateEditor : Editor
    {
        private SerializedProperty configuredTweensProp;
        private SerializedProperty playAllOnStartProp;
        private List<bool> foldouts = new List<bool>();

        void OnEnable()
        {
            configuredTweensProp = serializedObject.FindProperty("configuredTweens");
            playAllOnStartProp = serializedObject.FindProperty("playAllOnStart");
            SyncFoldouts();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (configuredTweensProp == null)
            {
                EditorGUILayout.HelpBox("No configured tweens found.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.LabelField("Configured Tweens", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playAllOnStartProp, new GUIContent("Play All On Start"));
            SyncFoldouts();

            for (int i = 0; i < configuredTweensProp.arraySize; ++i)
            {
                var e = configuredTweensProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");

                // Header with foldout and quick actions
                EditorGUILayout.BeginHorizontal();
                string headerName = e.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(headerName)) headerName = $"Tween {i + 1}";
                if (i >= foldouts.Count) foldouts.Add(LoadFoldout(i, true));
                bool newFoldout = EditorGUILayout.Foldout(foldouts[i], headerName, true);
                if (newFoldout != foldouts[i])
                {
                    foldouts[i] = newFoldout;
                    SaveFoldout(i, newFoldout);
                }
                if (GUILayout.Button("Play", GUILayout.Width(60))) ((Animate)target).PlayByIndex(i);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    configuredTweensProp.DeleteArrayElementAtIndex(i);
                    foldouts.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (!foldouts[i])
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }

                EditorGUILayout.PropertyField(e.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(e.FindPropertyRelative("targetObject"));
                EditorGUILayout.PropertyField(e.FindPropertyRelative("type"));

                var typeProp = e.FindPropertyRelative("type");
                var typeIndex = typeProp.enumValueIndex;

                // Common fields
                EditorGUILayout.PropertyField(e.FindPropertyRelative("delayMode"), new GUIContent("Delay Mode"));
                var delayModeProp = e.FindPropertyRelative("delayMode");
                var delayMode = (Animate.DelayMode)delayModeProp.enumValueIndex;
                
                if (delayMode == Animate.DelayMode.Frames)
                {
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("delayValue"), new GUIContent("Delay (frames)"));
                }
                else if (delayMode == Animate.DelayMode.Seconds)
                {
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("delayValue"), new GUIContent("Delay (s)"));
                }
                
                EditorGUILayout.PropertyField(e.FindPropertyRelative("duration"), new GUIContent("Duration (s)"));
                EditorGUILayout.PropertyField(e.FindPropertyRelative("curve"));

                switch ((Animate.TweenType)typeIndex)
                {
                    case Animate.TweenType.Position:
                    case Animate.TweenType.LocalPosition:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("local"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"), new GUIContent("Initial Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromVec3"), new GUIContent("Start Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toVec3"), new GUIContent("End Value"));
                        break;

                    case Animate.TweenType.RotationEuler:
                    case Animate.TweenType.LocalRotationEuler:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("local"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"), new GUIContent("Initial Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromVec3"), new GUIContent("Start Euler"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toVec3"), new GUIContent("End Euler"));
                        break;

                    case Animate.TweenType.Scale:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromVec3"), new GUIContent("Start Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toVec3"), new GUIContent("End Value"));
                        break;

                    case Animate.TweenType.CanvasGroupAlpha:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("Start Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("End Value"));
                        break;

                    case Animate.TweenType.RendererColor:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromColor"), new GUIContent("Start Color"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toColor"), new GUIContent("End Color"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("materialIndex"), new GUIContent("Material Index"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("materialProperty"), new GUIContent("Material Property", "Defaults to _Color when empty"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("materialColorProperties"), new GUIContent("Extra Color Properties"));
                        if (GUILayout.Button("Print Material Color Props"))
                        {
                            var go = e.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;
                            if (go == null)
                            {
                                Debug.LogWarning("Animate: No Target Object assigned for RendererColor.");
                            }
                            else
                            {
                                LogMaterialColorProps(go, e.FindPropertyRelative("materialIndex").intValue);
                            }
                        }
                        break;

                    case Animate.TweenType.MaterialFloat:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("materialProperty"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("Start Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("End Value"));
                        break;

                    case Animate.TweenType.Float:
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("Start Value"));
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("End Value"));
                        break;

                    case Animate.TweenType.CustomProperty:
                        {
                            EditorGUILayout.PropertyField(e.FindPropertyRelative("targetComponent"));
                            EditorGUILayout.PropertyField(e.FindPropertyRelative("propertyName"));
                            EditorGUILayout.PropertyField(e.FindPropertyRelative("propertyMode"));
                            EditorGUILayout.PropertyField(e.FindPropertyRelative("startSource"), new GUIContent("Initial Value"));

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Refresh Type"))
                            {
                                var comp = e.FindPropertyRelative("targetComponent").objectReferenceValue as Component;
                                string propPath = e.FindPropertyRelative("propertyName").stringValue;
                                string detected = string.Empty;
                                if (comp != null && !string.IsNullOrEmpty(propPath))
                                {
                                    var t = ResolveMemberType(comp, propPath);
                                    if (t != null) detected = t.Name;
                                }
                                e.FindPropertyRelative("detectedPropertyType").stringValue = detected;
                            }
                            EditorGUILayout.EndHorizontal();

                            // Hierarchical nested property dropdowns (depth 1-3)
                            // NOTE: Reflection is deferred until Browse button is clicked to avoid inspector lag
                            var compProp = e.FindPropertyRelative("targetComponent");
                            var compObj = compProp.objectReferenceValue as Component;
                            if (compObj != null)
                            {
                                string currentPath = e.FindPropertyRelative("propertyName").stringValue;
                                
                                EditorGUILayout.LabelField("Select Property");
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.TextField(currentPath);
                                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                                {
                                    // ONLY collect nested members when button is clicked, not every frame
                                    var nested = CollectNestedMembers(compObj, 3);
                                    if (nested.Count > 0)
                                    {
                                        PropertySearchWindow.ShowWindow(nested, selectedEntry =>
                                        {
                                            e.FindPropertyRelative("propertyName").stringValue = selectedEntry.path;
                                            e.FindPropertyRelative("detectedPropertyType").stringValue = selectedEntry.typeName;
                                            serializedObject.ApplyModifiedProperties();
                                        });
                                    }
                                    else
                                    {
                                        EditorUtility.DisplayDialog("No Properties Found", "No nested public fields/properties found on this component.", "OK");
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }

                            // Type-specific inputs based on detectedPropertyType
                            string det = e.FindPropertyRelative("detectedPropertyType").stringValue;
                            EditorGUILayout.LabelField("Detected Type", det);
                            EditorGUILayout.Space();

                            if (!string.IsNullOrEmpty(det))
                            {
                                // Check if it's an alpha/opacity property
                                string propName = e.FindPropertyRelative("propertyName").stringValue;
                                bool isAlpha = propName.EndsWith(".a") || propName.EndsWith(".alpha");

                                switch (det)
                                {
                                    case "Single":
                                    case "Double":
                                        if (isAlpha)
                                        {
                                            EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("Start Alpha"));
                                            EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("End Alpha"));
                                        }
                                        else
                                        {
                                            EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("Start Value"));
                                            EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("End Value"));
                                        }
                                        break;
                                    case "Int32":
                                        {
                                            var fromF = e.FindPropertyRelative("fromFloat");
                                            var toF = e.FindPropertyRelative("toFloat");
                                            int fromInt = Mathf.RoundToInt(fromF.floatValue);
                                            int toInt = Mathf.RoundToInt(toF.floatValue);
                                            fromInt = EditorGUILayout.IntField("Start Value", fromInt);
                                            toInt = EditorGUILayout.IntField("End Value", toInt);
                                            fromF.floatValue = fromInt;
                                            toF.floatValue = toInt;
                                        }
                                        break;
                                    case "Vector3":
                                    case "Vector4":
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromVec3"), new GUIContent("Start Value"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toVec3"), new GUIContent("End Value"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("vectorMask"), new GUIContent("Component Mask"));
                                        break;
                                    case "Color":
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromColor"), new GUIContent("Start Color"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toColor"), new GUIContent("End Color"));
                                        break;
                                    case "Boolean":
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromBool"), new GUIContent("Start"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toBool"), new GUIContent("End"));
                                        break;
                                    case "Quaternion":
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromVec3"), new GUIContent("Start Euler"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toVec3"), new GUIContent("End Euler"));
                                        break;
                                    case "Void":
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("methodInvokeTiming"), new GUIContent("Invoke Timing"));
                                        var timingProp = e.FindPropertyRelative("methodInvokeTiming");
                                        var timing = (Animate.MethodInvokeTiming)timingProp.enumValueIndex;
                                        
                                        if (timing == Animate.MethodInvokeTiming.OnCurve)
                                        {
                                            EditorGUILayout.HelpBox("Method will be invoked when the animation curve value reaches or exceeds 0.9. Curve can retrigger the method if it drops below 0.9 and rises again.", MessageType.Info);
                                        }
                                        else if (timing == Animate.MethodInvokeTiming.OnStart)
                                        {
                                            EditorGUILayout.HelpBox("Method will be invoked once at the start of the animation.", MessageType.Info);
                                        }
                                        else if (timing == Animate.MethodInvokeTiming.OnEnd)
                                        {
                                            EditorGUILayout.HelpBox("Method will be invoked once at the end of the animation (after duration).", MessageType.Info);
                                        }
                                        else if (timing == Animate.MethodInvokeTiming.StartAndEnd)
                                        {
                                            EditorGUILayout.HelpBox("Method will be invoked once at the start and once at the end of the animation.", MessageType.Info);
                                        }
                                        break;
                                    default:
                                        EditorGUILayout.LabelField($"Type '{det}' not directly supported in inspector.");
                                        EditorGUILayout.HelpBox("Use from/to float values or define custom handling in code.", MessageType.Info);
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("fromFloat"), new GUIContent("From (as float)"));
                                        EditorGUILayout.PropertyField(e.FindPropertyRelative("toFloat"), new GUIContent("To (as float)"));
                                        break;
                                }
                            }
                            else
                            {
                                EditorGUILayout.HelpBox("Click 'Refresh Type' to detect the property type.", MessageType.Info);
                            }

                            break;
                        }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Tween"))
            {
                int newIndex = configuredTweensProp.arraySize;
                configuredTweensProp.arraySize++;
                var e = configuredTweensProp.GetArrayElementAtIndex(newIndex);

                e.FindPropertyRelative("name").stringValue = string.Empty;
                e.FindPropertyRelative("targetObject").objectReferenceValue = null;
                e.FindPropertyRelative("targetComponent").objectReferenceValue = null;
                e.FindPropertyRelative("type").enumValueIndex = (int)Animate.TweenType.Position;
                e.FindPropertyRelative("playOnStart").boolValue = false;
                e.FindPropertyRelative("startSource").enumValueIndex = (int)Animate.StartSource.Ignore;
                e.FindPropertyRelative("local").boolValue = true;

                e.FindPropertyRelative("fromVec3").vector3Value = Vector3.zero;
                e.FindPropertyRelative("toVec3").vector3Value = Vector3.zero;
                e.FindPropertyRelative("fromColor").colorValue = Color.white;
                e.FindPropertyRelative("toColor").colorValue = Color.white;
                e.FindPropertyRelative("fromFloat").floatValue = 0f;
                e.FindPropertyRelative("toFloat").floatValue = 0f;
                e.FindPropertyRelative("materialProperty").stringValue = "_Glossiness";
                e.FindPropertyRelative("materialIndex").intValue = 0;
                e.FindPropertyRelative("materialColorProperties").ClearArray();
                e.FindPropertyRelative("fromBool").boolValue = false;
                e.FindPropertyRelative("toBool").boolValue = true;
                e.FindPropertyRelative("propertyName").stringValue = string.Empty;
                e.FindPropertyRelative("detectedPropertyType").stringValue = string.Empty;
                e.FindPropertyRelative("propertyMode").enumValueIndex = (int)Animate.CustomPropertyMode.AutoTween;
                e.FindPropertyRelative("methodInvokeTiming").enumValueIndex = (int)Animate.MethodInvokeTiming.OnEnd;
                e.FindPropertyRelative("delayMode").enumValueIndex = (int)Animate.DelayMode.None;
                e.FindPropertyRelative("delayValue").floatValue = 0f;
                e.FindPropertyRelative("duration").floatValue = 1f;
                e.FindPropertyRelative("curve").animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

                foldouts.Add(true);
            }
            if (GUILayout.Button("Play All")) ((Animate)target).PlayAllConfigured();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private List<NestedEntry> CollectNestedMembers(Component root, int maxDepth)
        {
            var results = new List<NestedEntry>();
            if (root == null) return results;

            void Recurse(object owner, string prefix, int depth, HashSet<object> seen)
            {
                if (owner == null || depth > maxDepth) return;
                Type t = owner.GetType();

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead
                                && p.PropertyType != typeof(Matrix4x4)
                                && !p.Name.Contains("Matrix"));
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType != typeof(Matrix4x4) && !f.Name.Contains("Matrix"));
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void));

                // Add all properties
                foreach (var p in props)
                {
                    string path = string.IsNullOrEmpty(prefix) ? p.Name : prefix + "." + p.Name;
                    object val = null;
                    Type propType = p.PropertyType;
                    Type refStructType = null;
                    
                    try 
                    { 
                        val = p.GetValue(owner, null);
                    } 
                    catch { val = "<err>"; }
                    
                    // Check if return type is a ref struct or managed ref (ends with &)
                    bool isRefReturn = propType.Name.EndsWith("&");
                    if (isRefReturn)
                    {
                        // For ref returns, try to get the base type
                        string baseTypeName = propType.Name.TrimEnd('&');
                        // Look in current assembly and Nova assemblies (by name)
                        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                        var targetAssemblies = new[] 
                        { 
                            propType.Assembly, 
                            assemblies.FirstOrDefault(a => a.GetName().Name == "Nova") 
                        }.Where(a => a != null).ToArray();
                        foreach (var asm in targetAssemblies)
                        {
                            refStructType = asm.GetType(propType.Namespace + "." + baseTypeName);
                            if (refStructType != null) break;
                        }
                    }
                    else if (!propType.IsPrimitive && propType != typeof(string))
                    {
                        refStructType = propType;
                    }
                    
                    string valStr = val?.ToString() ?? "null";
                    string typeName_display = propType.Name;
                    results.Add(new NestedEntry { path = path, display = $"{path} : {valStr} ({typeName_display})", typeName = typeName_display });

                    // For ref structs and complex types, enumerate their public properties
                    if (refStructType != null && depth < maxDepth)
                    {
                        try
                        {
                            var nestedProps = refStructType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(rp => rp.GetIndexParameters().Length == 0 && rp.CanRead && !rp.PropertyType.IsGenericType);
                            
                            foreach (var nestedProp in nestedProps)
                            {
                                string nestedPath = path + "." + nestedProp.Name;
                                object nestedVal = null;
                                try
                                {
                                    // For ref returns, we can't actually call GetValue, so just add the property
                                    if (!isRefReturn && val != null)
                                        nestedVal = nestedProp.GetValue(val);
                                }
                                catch { }
                                
                                string nestedValStr = nestedVal?.ToString() ?? (isRefReturn ? "(ref)" : "null");
                                string nestedTypeName = nestedProp.PropertyType.Name;
                                results.Add(new NestedEntry { path = nestedPath, display = $"{nestedPath} : {nestedValStr} ({nestedTypeName})", typeName = nestedTypeName });
                            }
                        }
                        catch { }
                    }
                    // Regular struct/class recursion
                    else if (depth < maxDepth && !p.PropertyType.IsPrimitive && p.PropertyType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                    {
                        if (val != null && !seen.Contains(val))
                        {
                            try
                            {
                                seen.Add(val);
                                Recurse(val, path, depth + 1, seen);
                            }
                            catch { }
                        }
                    }
                }

                // Add all fields
                foreach (var f in fields)
                {
                    string path = string.IsNullOrEmpty(prefix) ? f.Name : prefix + "." + f.Name;
                    object val = null;
                    try { val = f.GetValue(owner); } catch { val = "<err>"; }
                    string valStr = val?.ToString() ?? "null";
                    string typeName = f.FieldType.Name;
                    results.Add(new NestedEntry { path = path, display = $"{path} : {valStr} ({typeName})", typeName = typeName });

                    // Recurse into non-primitive fields (structs, classes)
                    if (val != null && depth < maxDepth && !f.FieldType.IsPrimitive && f.FieldType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                    {
                        if (!seen.Contains(val))
                        {
                            seen.Add(val);
                            Recurse(val, path, depth + 1, seen);
                        }
                    }
                }
                
                // Add methods (only once, not per-property)
                foreach (var m in methods)
                {
                    string methodPath = string.IsNullOrEmpty(prefix) ? m.Name : prefix + "." + m.Name;
                    string display = $"{methodPath} () (Method)";
                    results.Add(new NestedEntry { path = methodPath, display = display, typeName = "Void" });
                }
            }

            Recurse(root, string.Empty, 0, new HashSet<object>());
            return results;
        }

        private void LogMaterialColorProps(GameObject go, int materialIndex)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
            {
                Debug.LogWarning($"Animate: TargetObject '{go.name}' has no Renderer.");
                return;
            }

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                Debug.LogWarning($"Animate: Renderer on '{go.name}' has no materials.");
                return;
            }

            materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
            var mat = mats[materialIndex];
            if (mat == null || mat.shader == null)
            {
                Debug.LogWarning($"Animate: Material at index {materialIndex} is null on '{go.name}'.");
                return;
            }

#if UNITY_EDITOR
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                var type = ShaderUtil.GetPropertyType(mat.shader, i);
                if (type == ShaderUtil.ShaderPropertyType.Color || type == ShaderUtil.ShaderPropertyType.Vector)
                {
                    string propName = ShaderUtil.GetPropertyName(mat.shader, i);
                    Color c = mat.HasProperty(propName) ? mat.GetColor(propName) : Color.magenta;
                    Debug.Log($"[Animate] Material '{mat.name}' prop '{propName}' ({type}) = {c} (material idx {materialIndex})", go);
                }
            }
#else
            Debug.Log($"[Animate] Shader property logging only available in editor for '{go.name}'.");
#endif
        }

        private void SyncFoldouts()
        {
            int size = configuredTweensProp != null ? configuredTweensProp.arraySize : 0;
            while (foldouts.Count < size) foldouts.Add(LoadFoldout(foldouts.Count, true));
            while (foldouts.Count > size) foldouts.RemoveAt(foldouts.Count - 1);
        }

        private string FoldoutKey(int index) => $"AnimateEditor_Foldout_{target.GetInstanceID()}_{index}";
        private bool LoadFoldout(int index, bool defaultValue) => SessionState.GetBool(FoldoutKey(index), defaultValue);
        private void SaveFoldout(int index, bool value) => SessionState.SetBool(FoldoutKey(index), value);

        /// <summary>
        /// Resolve the System.Type of a (possibly nested) field/property path like "foo.bar.value".
        /// </summary>
        private Type ResolveMemberType(object root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;

            object current = root;
            Type currentType = root.GetType();

            foreach (var segment in path.Split('.'))
            {
                if (string.IsNullOrEmpty(segment)) return null;

                var pi = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null)
                {
                    currentType = pi.PropertyType;
                    
                    // Handle ref-return properties (Type ends with &)
                    if (currentType.Name.EndsWith("&"))
                    {
                        string baseTypeName = currentType.Name.TrimEnd('&');
                        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                        var searchAssemblies = new[] 
                        { 
                            currentType.Assembly, 
                            assemblies.FirstOrDefault(a => a.GetName().Name == "Nova") 
                        }.Where(a => a != null).ToArray();
                        
                        foreach (var asm in searchAssemblies)
                        {
                            var refType = asm.GetType(currentType.Namespace + "." + baseTypeName);
                            if (refType != null)
                            {
                                currentType = refType;
                                break;
                            }
                        }
                        current = null; // Can't get value of ref return
                    }
                    else
                    {
                        try { current = current != null ? pi.GetValue(current, null) : null; } catch { current = null; }
                    }
                    continue;
                }

                var fi = currentType.GetField(segment, BindingFlags.Public | BindingFlags.Instance);
                if (fi != null)
                {
                    currentType = fi.FieldType;
                    try { current = current != null ? fi.GetValue(current) : null; } catch { current = null; }
                    continue;
                }
                
                // Check for methods (parameterless, void return)
                var mi = currentType.GetMethod(segment, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (mi != null && mi.GetParameters().Length == 0 && mi.ReturnType == typeof(void))
                {
                    return typeof(void);
                }

                // segment not found
                return null;
            }

            return currentType;
        }
    }

    public class PropertySearchWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchText = "";
        private string lastSearchText = null;
        private List<NestedEntry> allProperties = new List<NestedEntry>();
        private List<NestedEntry> cachedFiltered = new List<NestedEntry>();
        private System.Action<NestedEntry> onSelected;

        public static void ShowWindow(List<NestedEntry> properties, System.Action<NestedEntry> onSelect)
        {
            var window = GetWindow<PropertySearchWindow>("Select Property");
            window.allProperties = properties;
            window.cachedFiltered = new List<NestedEntry>(properties);
            window.onSelected = onSelect;
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Search Properties", EditorStyles.boldLabel);
            searchText = EditorGUILayout.TextField("Search:", searchText);
            EditorGUILayout.Space();

            // Only refilter if search text changed
            if (searchText != lastSearchText)
            {
                lastSearchText = searchText;
                cachedFiltered.Clear();
                foreach (var p in allProperties)
                {
                    if (string.IsNullOrEmpty(searchText) || p.path.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        cachedFiltered.Add(p);
                }
                cachedFiltered.Sort((a, b) => a.path.CompareTo(b.path));
            }

            EditorGUILayout.LabelField($"Found: {cachedFiltered.Count} properties");
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var entry in cachedFiltered)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.display, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    onSelected?.Invoke(entry);
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
