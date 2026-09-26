#if UNITY_EDITOR
using System;
using System.Reflection;
using Aura;
using UnityEditor;
using UnityEngine;

namespace Core.Animator
{
    public static class AnimatePositionAxisChecks
    {
        [MenuItem("Tools/Core/Animator/Check Aura Position Axes")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
            CheckBlock<UIBlock2D>();
            CheckBlock<UIBlock3D>();
            Debug.Log("CORE POSITION AXIS CHECKS PASSED: raw aliases, explicit raw paths, picker types, units, and untouched axes.");
        }

        static void CheckBlock<T>() where T : UIBlock
        {
            var go = new GameObject("PositionAxisCheck", typeof(T), typeof(Animate));
            try
            {
                var block = go.GetComponent<T>();
                var animate = go.GetComponent<Animate>();
                var execute = typeof(Animate).GetMethod("ExecuteEntryImmediate", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (LengthType unit in new[] { LengthType.Value, LengthType.Percent })
                foreach (string axis in new[] { "X", "Y", "Z" })
                foreach (string path in new[] { "Position." + axis, "Position." + axis + ".Raw", "Layout.Position." + axis })
                {
                    block.Position.X.Type = block.Position.Y.Type = block.Position.Z.Type = unit;
                    block.Position.Raw = new Vector3(11, 22, 33);
                    var entry = new Animate.TweenEntry
                    {
                        type = Animate.TweenType.CustomProperty, targetObject = go, targetComponent = block,
                        propertyName = path, startSource = Animate.StartSource.Ignore,
                        fromFloat = 0, toFloat = -520, duration = 0
                    };
                    execute.Invoke(animate, new object[] { entry });
                    var expected = new Vector3(11, 22, 33);
                    expected[axis == "X" ? 0 : axis == "Y" ? 1 : 2] = -520;
                    Require(block.Position.Raw == expected, path + " did not write only the selected raw axis");
                    Require(block.Position.X.Type == unit && block.Position.Y.Type == unit && block.Position.Z.Type == unit, "Length types changed");
                    Require(MemberPathBrowser.ResolveMemberType(block, path) == typeof(float), "Picker type mismatch: " + path);
                }
                var entries = MemberPathBrowser.CollectNestedMembers(block, 3);
                foreach (string axis in new[] { "X", "Y", "Z" })
                    Require(entries.Exists(e => e.path == "Position." + axis && e.typeName == "Single"), "Missing scalar picker entry: " + axis);
                Require(Animate.NormalizeAuraPositionAxisPath(go.transform, "Position.X") == "Position.X", "Alias leaked to Transform");
                Require(Animate.NormalizeAuraPositionAxisPath(block, "Position.X.Percent") == "Position.X.Percent", "Explicit percent path changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
