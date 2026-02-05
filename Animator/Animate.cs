using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Nova;
using Sirenix.OdinInspector;

namespace Animator
{
    /// <summary>
    /// General purpose animator/tween component. Supports animating arbitrary values
    /// via getter/setter delegates and provides convenience helpers for common
    /// Unity types (float, Vector3, Quaternion, Color).
    /// - Can use the current value as the start or force a provided start value.
    /// - Uses an <see cref="AnimationCurve"/> for easing.
    /// </summary>
    public class Animate : MonoBehaviour
    {
        // Track started coroutines so we can stop them later if requested
        private readonly List<Coroutine> activeTweens = new List<Coroutine>();
        
#if UNITY_EDITOR
        // Editor preview tween state
        private class PreviewTweenState
        {
            public TweenEntry entry;
            public float startTime;
            public object initialState;
        }
        private PreviewTweenState currentPreviewTween = null;
#endif

        [BoxGroup("Global", ShowLabel = true)]
        [PropertyOrder(-100)]
        [LabelText("Play On Start")]
        public bool playAllOnStart = true;

        [FoldoutGroup("Tweens", Expanded = true)]
        [PropertyOrder(10)]
        [SerializeField]
        [LabelText("Configured Tweens")]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowFoldout = false)]
        private List<TweenEntry> configuredTweens = new List<TweenEntry>();

        [Serializable]
        public enum TweenType
        {
            Position,
            LocalPosition,
            RotationEuler,
            LocalRotationEuler,
            Scale,
            CanvasGroupAlpha,
            RendererColor,
            MaterialFloat,
            Float,
            CustomProperty
        }

        void Start()
        {
            if (playAllOnStart)
            {
                PlayAllConfigured();
            }
        }

        [Serializable]
        [InlineProperty]
        [HideReferenceObjectPicker]
        public class TweenEntry
        {
            public TweenEntry Clone()
            {
                var c = new TweenEntry();

                c.chainAfterPrevious = chainAfterPrevious;
                c.name = name;

                c.targetObject = targetObject;
                c.targetComponent = targetComponent;
                c.type = type;

                c.startSource = startSource;
                c.local = local;

                c.fromVec3 = fromVec3;
                c.toVec3 = toVec3;

                c.fromColor = fromColor;
                c.toColor = toColor;

                c.fromFloat = fromFloat;
                c.toFloat = toFloat;

                c.materialProperty = materialProperty;
                c.materialIndex = materialIndex;
                c.materialColorProperties = materialColorProperties != null ? materialColorProperties.ToArray() : Array.Empty<string>();

                c.fromBool = fromBool;
                c.toBool = toBool;

                c.propertyName = propertyName;
                c.propertyMode = propertyMode;
                c.methodInvokeTiming = methodInvokeTiming;
                c.detectedPropertyType = detectedPropertyType;

                c.vectorMask = vectorMask;
                c.enumFieldMask = enumFieldMask;

                c.delayMode = delayMode;
                c.delayValue = delayValue;
                c.duration = duration;
                c.curve = curve;

                return c;
            }
            // ----------------
            // Header
            // ----------------
            [HorizontalGroup("Header", Width = 18)]
            [HideLabel]
            [Tooltip("If enabled, this tween waits for the previous tween to finish. Ignored on the first item.")]
            [PropertyOrder(-100)]
            public bool chainAfterPrevious = false;

            [HorizontalGroup("Header")]
            [HideLabel]
            [PropertyOrder(-99)]
            public string name;

            // ----------------
            // Target / Type
            // ----------------
            [BoxGroup("A Target", ShowLabel = true)]
            [PropertyOrder(0)]
            public TweenType type = TweenType.Position;

            [BoxGroup("A Target")]
            [PropertyOrder(1)]
            public GameObject targetObject;

            [BoxGroup("A Target")]
            [ShowIf("@IsCustomProperty")]
            [PropertyOrder(2)]
            public Component targetComponent;


            // ----------------
            // Custom property selection
            // ----------------
            [BoxGroup("B Custom Property", ShowLabel = true)]
            [ShowIf("@IsCustomProperty")]
            [PropertyOrder(10)]
            [LabelText("Property")]
            public string propertyName;

            [BoxGroup("B Custom Property")]
            [ShowIf("@IsCustomProperty")]
            [PropertyOrder(11)]
            public CustomPropertyMode propertyMode = CustomPropertyMode.AutoTween;

            [BoxGroup("B Custom Property")]
            [ShowIf("@IsCustomProperty")]
            [PropertyOrder(12)]
            public MethodInvokeTiming methodInvokeTiming = MethodInvokeTiming.OnEnd;

            [BoxGroup("B Custom Property")]
            [ShowIf("@IsCustomProperty")]
            [PropertyOrder(13)]
            [ReadOnly]
            [LabelText("Detected Type")]
            public string detectedPropertyType;

            // ----------------
            // Values
            // ----------------
            [BoxGroup("C Values", ShowLabel = true)]
            [ShowIf("@UsesStartSource")]
            [PropertyOrder(20)]
            [LabelText("Initial Value")]
            public StartSource startSource = StartSource.Ignore;

            [BoxGroup("C Values")]
            [ShowIf("@UsesLocalSpace")]
            [PropertyOrder(21)]
            public bool local = true;

            [BoxGroup("C Values")]
            [ShowIf("@UsesVec3")]
            [PropertyOrder(22)]
            [LabelText("Start Value")]
            public Vector3 fromVec3;

            [BoxGroup("C Values")]
            [ShowIf("@UsesVec3")]
            [PropertyOrder(23)]
            [LabelText("End Value")]
            public Vector3 toVec3;

            [BoxGroup("C Values")]
            [ShowIf("@UsesFloat")]
            [PropertyOrder(24)]
            [LabelText("Start Value")]
            public float fromFloat = 0f;

            [BoxGroup("C Values")]
            [ShowIf("@UsesFloat")]
            [PropertyOrder(25)]
            [LabelText("End Value")]
            public float toFloat = 1f;

            [BoxGroup("C Values")]
            [ShowIf("@UsesColor")]
            [PropertyOrder(26)]
            [LabelText("Start Color")]
            public Color fromColor = Color.white;

            [BoxGroup("C Values")]
            [ShowIf("@UsesColor")]
            [PropertyOrder(27)]
            [LabelText("End Color")]
            public Color toColor = Color.white;

            [BoxGroup("C Values")]
            [ShowIf("@UsesBool")]
            [PropertyOrder(28)]
            [LabelText("Start")]
            public bool fromBool = false;

            [BoxGroup("C Values")]
            [ShowIf("@UsesBool")]
            [PropertyOrder(29)]
            [LabelText("End")]
            public bool toBool = true;

            // ----------------
            // Masks / Material
            // ----------------
            [BoxGroup("D Options", ShowLabel = true)]
            [ShowIf("@UsesVectorMask")]
            [PropertyOrder(30)]
            [LabelText("Component Mask")]
            public ComponentMask vectorMask = ComponentMask.All;

            [BoxGroup("D Options")]
            [ShowIf("@UsesEnumFieldMask")]
            [PropertyOrder(31)]
            [LabelText("Enum Field Mask")]
            public ComponentMask enumFieldMask = ComponentMask.None;

            [BoxGroup("D Options")]
            [ShowIf("@IsRendererColor")]
            [PropertyOrder(32)]
            [LabelText("Material Index")]
            public int materialIndex = 0;

            [BoxGroup("D Options")]
            [ShowIf("@UsesMaterialProperty")]
            [PropertyOrder(33)]
            [LabelText("Material Property")]
            [Tooltip("RendererColor defaults to _Color when empty.")]
            public string materialProperty = "_Glossiness";

            [BoxGroup("D Options")]
            [ShowIf("@IsRendererColor")]
            [PropertyOrder(34)]
            [Tooltip("Optional additional material color properties to set alongside materialProperty (e.g. _EmissionColor, _SpecColor).")]
            public string[] materialColorProperties = Array.Empty<string>();

            // ----------------
            // Timing
            // ----------------
            [BoxGroup("E Timing", ShowLabel = true)]
            [PropertyOrder(40)]
            public DelayMode delayMode = DelayMode.None;

            [BoxGroup("E Timing")]
            [ShowIf("@UsesDelayValue")]
            [PropertyOrder(41)]
            [LabelText("Delay")]
            public float delayValue = 0f;

            [BoxGroup("E Timing")]
            [PropertyOrder(42)]
            public float duration = 1f;

            [BoxGroup("E Timing")]
            [ShowIf("@UsesCurve")]
            [PropertyOrder(43)]
            public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            // ----------------
            // Odin helpers
            // ----------------
            private bool IsCustomProperty => type == TweenType.CustomProperty;
            private bool IsRendererColor => type == TweenType.RendererColor;
            private bool IsMaterialFloat => type == TweenType.MaterialFloat;
            private bool UsesMaterialProperty => type == TweenType.RendererColor || type == TweenType.MaterialFloat;

            private string Det => string.IsNullOrEmpty(detectedPropertyType) ? string.Empty : detectedPropertyType;

            private bool UsesLocalSpace => type == TweenType.Position || type == TweenType.LocalPosition || type == TweenType.RotationEuler || type == TweenType.LocalRotationEuler;
            private bool UsesStartSource => type != TweenType.Float;
            private bool UsesDelayValue => delayMode == DelayMode.Frames || delayMode == DelayMode.Seconds;

            private bool UsesVec3
            {
                get
                {
                    if (type == TweenType.Position || type == TweenType.LocalPosition) return true;
                    if (type == TweenType.RotationEuler || type == TweenType.LocalRotationEuler) return true;
                    if (type == TweenType.Scale) return true;

                    if (type == TweenType.CustomProperty)
                    {
                        // Vector3 / Vector4 / Quaternion / enum-struct helpers are all vec3-backed in this inspector.
                        return Det == "Vector3" || Det == "Vector4" || Det == "Quaternion" || (!string.IsNullOrEmpty(Det) && Det != "Single" && Det != "Double" && Det != "Int32" && Det != "Color" && Det != "Boolean" && Det != "Void");
                    }

                    return false;
                }
            }

            private bool UsesColor => type == TweenType.RendererColor || (type == TweenType.CustomProperty && Det == "Color");

            private bool UsesFloat
            {
                get
                {
                    if (type == TweenType.CanvasGroupAlpha) return true;
                    if (type == TweenType.MaterialFloat) return true;
                    if (type == TweenType.CustomProperty) return Det == "Single" || Det == "Double" || Det == "Int32";
                    return false;
                }
            }

            private bool UsesBool => type == TweenType.CustomProperty && Det == "Boolean";

            private bool UsesVectorMask => type == TweenType.CustomProperty && (Det == "Vector3" || Det == "Vector4" || Det == "Quaternion" || (!string.IsNullOrEmpty(Det) && Det != "Single" && Det != "Double" && Det != "Int32" && Det != "Color" && Det != "Boolean" && Det != "Void"));

            private bool UsesEnumFieldMask => type == TweenType.CustomProperty && (!string.IsNullOrEmpty(Det) && Det != "Single" && Det != "Double" && Det != "Int32" && Det != "Vector3" && Det != "Vector4" && Det != "Quaternion" && Det != "Color" && Det != "Boolean" && Det != "Void");

            private bool UsesCurve
            {
                get
                {
                    if (type == TweenType.CustomProperty)
                    {
                        // SetAtEnd/ToggleAtHalf don't evaluate the curve.
                        if (propertyMode != CustomPropertyMode.AutoTween) return false;

                        // Methods only use the curve when invoke timing is OnCurve.
                        if (Det == "Void" && methodInvokeTiming != MethodInvokeTiming.OnCurve) return false;
                    }

                    return true;
                }
            }
        }

        public enum CustomPropertyMode
        {
            AutoTween,
            SetAtEnd,
            ToggleAtHalf
        }
        
        [Serializable]
        public enum MethodInvokeTiming
        {
            OnCurve,        // Use curve threshold (0.9+) with retrigger support
            OnEnd,          // Invoke once at end of duration
            OnStart,        // Invoke once at start of duration
            StartAndEnd     // Invoke at both start and end
        }
        
        [Serializable]
        public enum DelayMode
        {
            None,           // No delay
            Frames,         // Delay by N frames (waits for next LateUpdate cycle)
            Seconds         // Delay by N seconds (real time)
        }

        [System.Flags]
        public enum ComponentMask
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
            W = 1 << 3,
            All = X | Y | Z | W
        }

        [Serializable]
        public enum StartSource
        {
            Ignore,     // Use provided from/fromVec3/fromColor/fromFloat values
            Start,      // Use current value as the start point
            End         // Use provided to/toVec3/toColor/toFloat as the start point (swap start/end)
        }

        // ----------------
        // Generic Tween API
        // ----------------

        /// <summary>
        /// Tween from an explicit <paramref name="from"/> to <paramref name="to"/> using the provided lerp function.
        /// </summary>
        public Coroutine Tween<T>(Func<T> getter, Action<T> setter, T from, T to, float duration, Func<T, T, float, T> lerpFunc, AnimationCurve curve = null, Action onComplete = null)
        {
            if (duration <= 0f)
            {
                setter(to);
                onComplete?.Invoke();
                return null;
            }

            curve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            IEnumerator routine = TweenCoroutine(getter, setter, from, to, duration, curve, lerpFunc, onComplete);
            Coroutine c = StartCoroutine(routine);
            activeTweens.Add(c);
            return c;
        }

        private const float BoolHighThreshold = 0.6f;
        private const float BoolLowThreshold = 0.4f;
        private const float MethodInvokeThreshold = 0.9f;

        private IEnumerator DriveBoolWithCurve(object owner, MemberInfo member, bool startValue, bool endValue, float duration, AnimationCurve curve)
        {
            // Execute bool state changes just before render each frame
            bool state = startValue;
            SetMemberValue(owner, member, state);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float v = curve.Evaluate(t);

                if (!state && v > BoolHighThreshold)
                {
                    state = true;
                    SetMemberValue(owner, member, state);
                }
                else if (state && v < BoolLowThreshold)
                {
                    state = false;
                    SetMemberValue(owner, member, state);
                }
                // Execute state change right before render
                yield return new WaitForEndOfFrame();
            }
            SetMemberValue(owner, member, endValue);
            yield return new WaitForEndOfFrame();
        }

        private IEnumerator InvokeMethodWithTiming(MemberInfo member, object owner, float duration, AnimationCurve curve, MethodInvokeTiming timing)
        {
            if (member is not MethodInfo mi) yield break;
            
            switch (timing)
            {
                case MethodInvokeTiming.OnStart:
                    mi.Invoke(owner, null);
                    yield return new WaitForEndOfFrame();
                    break;
                    
                case MethodInvokeTiming.OnEnd:
                    {
                        float elapsed = 0f;
                        while (elapsed < duration)
                        {
                            elapsed += Time.deltaTime;
                            yield return new WaitForEndOfFrame();
                        }
                        mi.Invoke(owner, null);
                        yield return new WaitForEndOfFrame();
                    }
                    break;
                    
                case MethodInvokeTiming.StartAndEnd:
                    {
                        mi.Invoke(owner, null);
                        yield return new WaitForEndOfFrame();
                        
                        float elapsed = 0f;
                        while (elapsed < duration)
                        {
                            elapsed += Time.deltaTime;
                            yield return new WaitForEndOfFrame();
                        }
                        mi.Invoke(owner, null);
                        yield return new WaitForEndOfFrame();
                    }
                    break;
                    
                case MethodInvokeTiming.OnCurve:
                    {
                        // Execute method invocations based on curve threshold with retrigger support
                        float elapsed = 0f;
                        bool fired = false;
                        while (elapsed < duration)
                        {
                            elapsed += Time.deltaTime;
                            float t = Mathf.Clamp01(elapsed / duration);
                            float v = curve.Evaluate(t);
                            if (!fired && v >= MethodInvokeThreshold)
                            {
                                mi.Invoke(owner, null);
                                fired = true;
                            }
                            if (fired && v < MethodInvokeThreshold)
                            {
                                fired = false; // allow retrigger if curve goes down and up again
                            }
                            yield return new WaitForEndOfFrame();
                        }
                    }
                    break;
            }
        }

        // Resolve a dot-separated member path starting from a root object (usually a Component).
        // Returns the owner object that contains the final member and the MemberInfo for that member.
        private bool TryResolveMember(object root, string path, out object owner, out MemberInfo member, out Type memberType)
        {
            owner = root;
            member = null;
            memberType = null;
            if (string.IsNullOrEmpty(path) || owner == null) return false;

            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (owner == null && !(owner is RefStructMarker)) return false;
                
                Type t;
                if (owner is RefStructMarker marker)
                {
                    // We're already traversing into a ref struct
                    t = marker.refStructType;
                }
                else
                {
                    t = owner.GetType();
                }
                
                var pi = t.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                var fi = t.GetField(part, BindingFlags.Public | BindingFlags.Instance);

                if (pi != null)
                {
                    if (i == parts.Length - 1)
                    {
                        // This is the final segment - return it
                        member = pi;
                        memberType = pi.PropertyType;
                        
                        // If it's a ref-return type, resolve the actual type
                        if (memberType.Name.EndsWith("&"))
                        {
                            string refTypeName = memberType.Name.TrimEnd('&');
                            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                            var resolvedType = ResolveType(memberType.Namespace + "." + refTypeName, assemblies);
                            if (resolvedType != null)
                            {
                                memberType = resolvedType;
                            }
                        }
                        
                        return true;
                    }
                    
                    // Not the final segment - continue traversal
                    bool isRefReturn = pi.PropertyType.Name.EndsWith("&");
                    
                    if (isRefReturn)
                    {
                        // Resolve ref-return type and wrap in marker
                        string refTypeName = pi.PropertyType.Name.TrimEnd('&');
                        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                        var refStructType = ResolveType(pi.PropertyType.Namespace + "." + refTypeName, assemblies);
                        
                        if (refStructType != null)
                        {
                            // Create a marker - the actual traversal will happen in GetMemberValue/SetMemberValue
                            owner = new RefStructMarker(owner, pi, refStructType);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        try { owner = pi.GetValue(owner); }
                        catch { return false; }
                    }
                    continue;
                }

                if (fi != null)
                {
                    if (i == parts.Length - 1)
                    {
                        member = fi;
                        memberType = fi.FieldType;
                        return true;
                    }
                    try { owner = fi.GetValue(owner); }
                    catch { return false; }
                    continue;
                }

                var mi = t.GetMethod(part, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (mi != null && mi.GetParameters().Length == 0 && mi.ReturnType == typeof(void))
                {
                    if (i == parts.Length - 1)
                    {
                        member = mi;
                        memberType = typeof(void);
                        return true;
                    }
                    owner = mi.Invoke(owner, null);
                    continue;
                }

                // not found
                return false;
            }

            return false;
        }
        
        private Type ResolveType(string fullTypeName, System.Reflection.Assembly[] assemblies)
        {
            foreach (var asm in assemblies)
            {
                if (asm == null) continue;
                var type = asm.GetType(fullTypeName);
                if (type != null) return type;
            }
            return null;
        }
        
        // Marker class for tracking ref struct traversal
        private class RefStructMarker
        {
            public object originalOwner;
            public PropertyInfo refProperty;
            public Type refStructType;
            
            public RefStructMarker(object owner, PropertyInfo prop, Type structType)
            {
                originalOwner = owner;
                refProperty = prop;
                refStructType = structType;
            }
            
            public override string ToString() => $"Ref<{refStructType.Name}>";
        }

        // Delegate types for ref-returning UIBlock.Size
        private delegate ref Length3 SizeGetter(UIBlock target);
        private delegate ref Length3 SizeGetter2D(Nova.UIBlock2D target);
        private delegate ref Length3 SizeGetter3D(Nova.UIBlock3D target);

        private object GetMemberValue(object owner, MemberInfo member)
        {
            // Handle ref struct marker
            if (owner is RefStructMarker marker)
            {
                // Get the actual ref struct instance by recursively calling the ref-return chain
                object refStructInstance = GetRefStructInstance(marker);
                
                if (member is PropertyInfo pi)
                {
                    try { return pi.GetValue(refStructInstance); }
                    catch { return null; }
                }
                if (member is FieldInfo fi)
                {
                    try { return fi.GetValue(refStructInstance); }
                    catch { return null; }
                }
                return null;
            }
            
            if (member is PropertyInfo pi2) return pi2.GetValue(owner);
            if (member is FieldInfo fi2) return fi2.GetValue(owner);
            return null;
        }
        
        private object GetRefStructInstance(RefStructMarker marker)
        {
            // If the original owner is also a marker, recursively resolve it first
            object currentOwner = marker.originalOwner;
            if (currentOwner is RefStructMarker parentMarker)
            {
                currentOwner = GetRefStructInstance(parentMarker);
            }
            
            // Now call the ref-return property on the resolved owner
            try { return marker.refProperty.GetValue(currentOwner); }
            catch { return null; }
        }

        private bool SetMemberValue(object owner, MemberInfo member, object value)
        {
            // Handle ref struct marker - must reconstruct and set back through ref-return
            if (owner is RefStructMarker marker)
            {
                return SetRefStructMember(marker, member, value);
            }
            
            if (member is PropertyInfo pi)
            {
                if (!pi.CanWrite) return false;
                pi.SetValue(owner, value);
                return true;
            }
            if (member is FieldInfo fi)
            {
                fi.SetValue(owner, value);
                return true;
            }
            return false;
        }
        
        private bool SetRefStructMember(RefStructMarker marker, MemberInfo member, object value)
        {
            try
            {
                // Get current state of the ref struct
                object refStructInstance = GetRefStructInstance(marker);
                if (refStructInstance == null) return false;
                
                // Modify it
                if (member is PropertyInfo pi)
                {
                    if (!pi.CanWrite) return false;
                    pi.SetValue(refStructInstance, value);
                }
                else if (member is FieldInfo fi)
                {
                    fi.SetValue(refStructInstance, value);
                }
                else
                {
                    return false;
                }
                
                // Now we need to set the modified struct back through the ref-return property
                // Get the parent owner (could be another marker)
                object parentOwner = marker.originalOwner;
                if (parentOwner is RefStructMarker parentMarker)
                {
                    // Recursively set through parent
                    return SetRefStructMember(parentMarker, marker.refProperty, refStructInstance);
                }
                else
                {
                    // Parent is the actual component - set the struct back
                    if (!marker.refProperty.CanWrite) return false;
                    marker.refProperty.SetValue(parentOwner, refStructInstance);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private bool TryHandleUIBlockAlignment(TweenEntry e, Component comp)
        {
            // Special handler for ref-return Alignment property on UIBlock/UIBlock2D/UIBlock3D
            // Enums are discrete states, not continuous values - snap to target at end
            
            return StartCoroutine(DriveEnumAlignment(e, comp)) != null;
        }
        
        private IEnumerator DriveEnumAlignment(TweenEntry e, Component comp)
        {
            // Get current alignment
            Func<Nova.Alignment> getCurrentAlignment = () =>
            {
                if (comp is Nova.UIBlock3D ub3) return ub3.Alignment;
                if (comp is Nova.UIBlock2D ub2) return ub2.Alignment;
                if (comp is Nova.UIBlock ub) return ub.Alignment;
                return Nova.Alignment.Center;
            };
            
            // Parse target alignment from toVec3
            var targetAlignment = new Nova.Alignment(
                (Nova.HorizontalAlignment)Mathf.RoundToInt(e.toVec3.x),
                (Nova.VerticalAlignment)Mathf.RoundToInt(e.toVec3.y),
                (Nova.DepthAlignment)Mathf.RoundToInt(e.toVec3.z)
            );
            
            // Apply start source logic
            Nova.Alignment startAlignment;
            if (e.startSource == StartSource.Start)
            {
                startAlignment = getCurrentAlignment();
            }
            else if (e.startSource == StartSource.End)
            {
                startAlignment = targetAlignment;
                targetAlignment = new Nova.Alignment(
                    (Nova.HorizontalAlignment)Mathf.RoundToInt(e.fromVec3.x),
                    (Nova.VerticalAlignment)Mathf.RoundToInt(e.fromVec3.y),
                    (Nova.DepthAlignment)Mathf.RoundToInt(e.fromVec3.z)
                );
            }
            else
            {
                startAlignment = new Nova.Alignment(
                    (Nova.HorizontalAlignment)Mathf.RoundToInt(e.fromVec3.x),
                    (Nova.VerticalAlignment)Mathf.RoundToInt(e.fromVec3.y),
                    (Nova.DepthAlignment)Mathf.RoundToInt(e.fromVec3.z)
                );
            }
            
            // Set start value
            ComponentMask mask = e.enumFieldMask;
            if (mask == ComponentMask.None)
                mask = ComponentMask.All;
            
            var current = startAlignment;
            if (mask.HasFlag(ComponentMask.X)) current.X = startAlignment.X;
            if (mask.HasFlag(ComponentMask.Y)) current.Y = startAlignment.Y;
            if (mask.HasFlag(ComponentMask.Z)) current.Z = startAlignment.Z;
            
            if (comp is Nova.UIBlock3D ub3) ub3.Alignment = current;
            else if (comp is Nova.UIBlock2D ub2) ub2.Alignment = current;
            else if (comp is Nova.UIBlock ub) ub.Alignment = current;
            
            // Wait for duration
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < e.duration)
            {
                yield return new WaitForEndOfFrame();
            }
            
            // Snap to target at end
            current = getCurrentAlignment();
            if (mask.HasFlag(ComponentMask.X)) current.X = targetAlignment.X;
            if (mask.HasFlag(ComponentMask.Y)) current.Y = targetAlignment.Y;
            if (mask.HasFlag(ComponentMask.Z)) current.Z = targetAlignment.Z;
            
            if (comp is Nova.UIBlock3D ub3b) ub3b.Alignment = current;
            else if (comp is Nova.UIBlock2D ub2b) ub2b.Alignment = current;
            else if (comp is Nova.UIBlock ub) ub.Alignment = current;
            
            yield return new WaitForEndOfFrame();
        }
        
        /// <summary>
        /// Adaptively handle any struct with enum fields (1-4 fields).
        /// Extracts enum values to the appropriate vector type, tweens, and applies back.
        /// Returns true if handled, false otherwise.
        /// </summary>
        private bool TryHandleEnumStruct(Component comp, TweenEntry e, object owner, MemberInfo memberInfo, Type memberType)
        {
            // Check if it's a struct with enum fields
            if (!memberType.IsValueType || memberType.IsPrimitive || memberType.IsEnum)
                return false;
            
            var enumFields = memberType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType.IsEnum)
                .OrderBy(f => f.Name) // Deterministic order for extraction
                .ToArray();
            
            if (enumFields.Length == 0 || enumFields.Length > 4)
                return false;
            
            // Dynamically adapt to field count: 1 field = float, 2-4 fields = Vector2/3/4
            switch (enumFields.Length)
            {
                case 1:
                    return TweenEnumStructAsFloat(e, owner, memberInfo, enumFields);
                case 2:
                    return TweenEnumStructAsVector2(e, owner, memberInfo, enumFields);
                case 3:
                    return TweenEnumStructAsVector3(e, owner, memberInfo, enumFields);
                case 4:
                    return TweenEnumStructAsVector4(e, owner, memberInfo, enumFields);
            }
            
            return false;
        }
        
        private bool TweenEnumStructAsFloat(TweenEntry e, object owner, MemberInfo memberInfo, FieldInfo[] enumFields)
        {
            Func<float> getter = () =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                return structInstance != null ? Convert.ToSingle(enumFields[0].GetValue(structInstance)) : 0f;
            };
            
            Action<float> setter = v =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance != null)
                {
                    enumFields[0].SetValue(structInstance, Enum.ToObject(enumFields[0].FieldType, Mathf.RoundToInt(v)));
                    SetMemberValue(owner, memberInfo, structInstance);
                }
            };
            
            var coro = TweenFloatWithSource(getter, setter, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
            return coro != null;
        }
        
        private bool TweenEnumStructAsVector2(TweenEntry e, object owner, MemberInfo memberInfo, FieldInfo[] enumFields)
        {
            Func<Vector3> getter = () =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance == null) return Vector3.zero;
                return new Vector3(
                    Convert.ToSingle(enumFields[0].GetValue(structInstance)),
                    Convert.ToSingle(enumFields[1].GetValue(structInstance)),
                    0f
                );
            };
            
            Action<Vector3> setter = v =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance != null)
                {
                    enumFields[0].SetValue(structInstance, Enum.ToObject(enumFields[0].FieldType, Mathf.RoundToInt(v.x)));
                    enumFields[1].SetValue(structInstance, Enum.ToObject(enumFields[1].FieldType, Mathf.RoundToInt(v.y)));
                    SetMemberValue(owner, memberInfo, structInstance);
                }
            };
            
            ComponentMask mask = e.vectorMask;
            Action<Vector3> maskedSetter = v =>
            {
                Vector3 current = getter();
                if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                setter(v);
            };
            
            var coro = TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
            return coro != null;
        }
        
        private bool TweenEnumStructAsVector3(TweenEntry e, object owner, MemberInfo memberInfo, FieldInfo[] enumFields)
        {
            Func<Vector3> getter = () =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance == null) return Vector3.zero;
                return new Vector3(
                    Convert.ToSingle(enumFields[0].GetValue(structInstance)),
                    Convert.ToSingle(enumFields[1].GetValue(structInstance)),
                    Convert.ToSingle(enumFields[2].GetValue(structInstance))
                );
            };
            
            Action<Vector3> setter = v =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance != null)
                {
                    // Only tween fields that are in the mask
                    ComponentMask mask = e.enumFieldMask;
                    if (mask.HasFlag(ComponentMask.X))
                        enumFields[0].SetValue(structInstance, Enum.ToObject(enumFields[0].FieldType, Mathf.RoundToInt(v.x)));
                    if (mask.HasFlag(ComponentMask.Y))
                        enumFields[1].SetValue(structInstance, Enum.ToObject(enumFields[1].FieldType, Mathf.RoundToInt(v.y)));
                    if (mask.HasFlag(ComponentMask.Z))
                        enumFields[2].SetValue(structInstance, Enum.ToObject(enumFields[2].FieldType, Mathf.RoundToInt(v.z)));
                    SetMemberValue(owner, memberInfo, structInstance);
                }
            };
            
            // If no enum fields are masked, default to all
            ComponentMask enumMask = e.enumFieldMask;
            if (enumMask == ComponentMask.None)
                enumMask = ComponentMask.All;
            
            Action<Vector3> maskedSetter = v =>
            {
                Vector3 current = getter();
                if (!enumMask.HasFlag(ComponentMask.X)) v.x = current.x;
                if (!enumMask.HasFlag(ComponentMask.Y)) v.y = current.y;
                if (!enumMask.HasFlag(ComponentMask.Z)) v.z = current.z;
                setter(v);
            };
            
            var coro = TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
            return coro != null;
        }
        
        private bool TweenEnumStructAsVector4(TweenEntry e, object owner, MemberInfo memberInfo, FieldInfo[] enumFields)
        {
            Func<Vector3> getter = () =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance == null) return Vector3.zero;
                return new Vector3(
                    Convert.ToSingle(enumFields[0].GetValue(structInstance)),
                    Convert.ToSingle(enumFields[1].GetValue(structInstance)),
                    Convert.ToSingle(enumFields[2].GetValue(structInstance))
                    // Note: Vector4.w would need separate handling if needed
                );
            };
            
            Action<Vector3> setter = v =>
            {
                object structInstance = GetMemberValue(owner, memberInfo);
                if (structInstance != null)
                {
                    enumFields[0].SetValue(structInstance, Enum.ToObject(enumFields[0].FieldType, Mathf.RoundToInt(v.x)));
                    enumFields[1].SetValue(structInstance, Enum.ToObject(enumFields[1].FieldType, Mathf.RoundToInt(v.y)));
                    enumFields[2].SetValue(structInstance, Enum.ToObject(enumFields[2].FieldType, Mathf.RoundToInt(v.z)));
                    if (enumFields.Length > 3)
                    {
                        // For 4th field, would need to pass as separate value
                        // For now, keep from current state
                        var current = GetMemberValue(owner, memberInfo);
                        if (current != null)
                        {
                            enumFields[3].SetValue(structInstance, enumFields[3].GetValue(current));
                        }
                    }
                    SetMemberValue(owner, memberInfo, structInstance);
                }
            };
            
            ComponentMask mask = e.vectorMask;
            Action<Vector3> maskedSetter = v =>
            {
                Vector3 current = getter();
                if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                setter(v);
            };
            
            var coro = TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
            return coro != null;
        }

        /// <summary>
        /// Tween using the current getter value as the start.
        /// </summary>
        public Coroutine Tween<T>(Func<T> getter, Action<T> setter, T to, float duration, Func<T, T, float, T> lerpFunc, AnimationCurve curve = null, Action onComplete = null)
        {
            T from = getter();
            return Tween(getter, setter, from, to, duration, lerpFunc, curve, onComplete);
        }

        private IEnumerator TweenCoroutine<T>(Func<T> getter, Action<T> setter, T from, T to, float duration, AnimationCurve curve, Func<T, T, float, T> lerpFunc, Action onComplete)
        {
            // Tweens execute just before render each frame using WaitForEndOfFrame
            // This ensures: Layout calculations → Tween applies value → Render with updated value
            float startTime = Time.realtimeSinceStartup;
            setter(from);
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= duration)
                {
                    setter(to);
                    // Final update at render time
                    yield return new WaitForEndOfFrame();
                    break;
                }
                float t = Mathf.Clamp01(elapsed / duration);
                float e = curve.Evaluate(t);
                setter(lerpFunc(from, to, e));
                // Execute update right before render
                yield return new WaitForEndOfFrame();
            }

            onComplete?.Invoke();
        }

        private IEnumerator ApplyActionAfterSeconds(Action action, float seconds)
        {
            if (action == null) yield break;
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < seconds)
            {
                yield return new WaitForEndOfFrame();
            }
            action();
            yield return new WaitForEndOfFrame();
        }

        // ----------------
        // Tween management
        // ----------------
        public void StopTween(Coroutine c)
        {
            if (c == null) return;
            try { StopCoroutine(c); } catch { }
            activeTweens.Remove(c);
        }

        public void StopAllTweens()
        {
            foreach (var c in activeTweens)
            {
                if (c != null)
                {
                    try { StopCoroutine(c); } catch { }
                }
            }

            activeTweens.Clear();
        }

        // ----------------
        // Inspector-play helpers
        // ----------------

        /// <summary>
        /// Play all configured tweens.
        /// If <see cref="TweenEntry.chainAfterPrevious"/> is enabled, will wait for the previous tween to finish.
        /// </summary>
        public void PlayAllConfigured()
        {
            // In edit mode, PlayEntry() runs preview behavior. Chaining is not guaranteed there,
            // but we still run the same scheduling logic.
            StartCoroutine(PlayAllConfiguredCoroutine());
        }

        private IEnumerator PlayAllConfiguredCoroutine()
        {
            // Track the coroutine handle per entry so we can wait on the previous one when chaining.
            Coroutine prev = null;

            for (int i = 0; i < configuredTweens.Count; i++)
            {
                var e = configuredTweens[i];
                if (e == null) continue;

                if (e.chainAfterPrevious && prev != null)
                {
                    yield return prev;
                }

                prev = PlayEntry(e);

                // If PlayEntry returned null (e.g. editor preview), don't block the chain.
                if (prev == null)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Play a configured entry by index.
        /// </summary>
        public void PlayByIndex(int index)
        {
            if (index < 0 || index >= configuredTweens.Count) return;
            PlayEntry(configuredTweens[index]);
        }

        public void CloneConfiguredTween(int index)
        {
            if (index < 0 || index >= configuredTweens.Count) return;

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Clone Tween");
#endif

            var src = configuredTweens[index];
            if (src == null)
            {
                configuredTweens.Insert(index + 1, null);
            }
            else
            {
                var clone = src.Clone();
                clone.name = string.IsNullOrEmpty(clone.name) ? "(Clone)" : (clone.name + " (Clone)");
                configuredTweens.Insert(index + 1, clone);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void RemoveConfiguredTween(int index)
        {
            if (index < 0 || index >= configuredTweens.Count) return;

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Remove Tween");
#endif

            configuredTweens.RemoveAt(index);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Play the first configured entry with a matching name.
        /// </summary>
        public void PlayByName(string name)
        {
            var e = configuredTweens.Find(x => x != null && x.name == name);
            if (e != null) PlayEntry(e);
        }

        private Coroutine PlayEntry(TweenEntry e)
        {
            if (e == null || e.targetObject == null) return null;
            
            // For editor preview (when not playing), auto-reset after duration + 1 second
            bool isPreview = !Application.isPlaying;
            if (isPreview)
            {
                StartCoroutine(PreviewTweenWithReset(e));
                return null;
            }
            
            return ExecuteEntry(e);
        }

        private Coroutine ExecuteEntry(TweenEntry e)
        {
            if (e == null || e.targetObject == null) return null;
            
            // If there's a delay, wrap the execution in a delayed coroutine
            if (e.delayMode != DelayMode.None && e.delayValue > 0f)
            {
                return StartCoroutine(ExecuteEntryWithDelay(e));
            }
            
            return ExecuteEntryImmediate(e);
        }
        
        private IEnumerator ExecuteEntryWithDelay(TweenEntry e)
        {
            if (e.delayMode == DelayMode.Frames)
            {
                // Wait N frames - each WaitForEndOfFrame is one frame
                int framesToWait = Mathf.Max(1, Mathf.RoundToInt(e.delayValue));
                for (int i = 0; i < framesToWait; i++)
                {
                    yield return new WaitForEndOfFrame();
                }
            }
            else if (e.delayMode == DelayMode.Seconds)
            {
                // Wait by real time
                yield return new WaitForSeconds(e.delayValue);
            }
            
            var c = ExecuteEntryImmediate(e);
            if (c != null)
            {
                yield return c;
            }
        }
        
        private Coroutine ExecuteEntryImmediate(TweenEntry e)
        {
            if (e == null || e.targetObject == null) return null;
            var go = e.targetObject;
            switch (e.type)
            {
                case TweenType.Position:
                case TweenType.LocalPosition:
                    return TweenPositionWithSource(go.transform, e.toVec3, e.duration, e.curve, e.local, e.startSource, e.fromVec3);

                case TweenType.RotationEuler:
                case TweenType.LocalRotationEuler:
                {
                    Quaternion toQ = Quaternion.Euler(e.toVec3);
                    Quaternion fromQ = Quaternion.Euler(e.fromVec3);
                    bool localRot = (e.type == TweenType.LocalRotationEuler) || e.local;
                    return TweenRotationWithSource(go.transform, toQ, e.duration, e.curve, localRot, e.startSource, fromQ);
                }

                case TweenType.Scale:
                    return TweenScaleWithSource(go.transform, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);

                case TweenType.CanvasGroupAlpha:
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg == null) return null;
                    return TweenCanvasAlphaWithSource(cg, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
                }

                case TweenType.RendererColor:
                {
                    var r = go.GetComponent<Renderer>();
                    if (r == null) return null;
                    string prop = string.IsNullOrEmpty(e.materialProperty) ? "_Color" : e.materialProperty;
                    int mIndex = Mathf.Clamp(e.materialIndex, 0, r.materials.Length - 1);
                    return TweenMaterialColorWithSource(r, mIndex, prop, e.materialColorProperties, e.toColor, e.duration, e.curve, e.startSource, e.fromColor);
                }

                case TweenType.MaterialFloat:
                {
                    var r = go.GetComponent<Renderer>();
                    if (r == null) return null;
                    string prop = string.IsNullOrEmpty(e.materialProperty) ? "_Glossiness" : e.materialProperty;
                    Func<float> getter = () => r.material.GetFloat(prop);
                    Action<float> setter = v => r.material.SetFloat(prop, v);
                    return TweenFloatWithSource(getter, setter, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
                }

                case TweenType.CustomProperty:
                {
                    Component comp = e.targetComponent ?? go.GetComponent<Component>();
                    if (comp == null)
                    {
                        Debug.LogWarning($"Animate: CustomProperty entry '{e.name}' has no component assigned on {go.name}.");
                        return null;
                    }

                    // support nested member path via dot notation; strip enum backing-field suffix if user picked it
                    string resolvedPath = e.propertyName;
                    const string backingSuffix = ".value__";
                    if (!string.IsNullOrEmpty(resolvedPath) && resolvedPath.EndsWith(backingSuffix, StringComparison.Ordinal))
                        resolvedPath = resolvedPath.Substring(0, resolvedPath.Length - backingSuffix.Length);

                    // Fast-path: direct UIBlock Size handling (avoids ref-return reflection issues)
                    if ((comp is Nova.UIBlock || comp is Nova.UIBlock2D || comp is Nova.UIBlock3D) && (resolvedPath == "Size.Percent" || resolvedPath == "Size.Raw"))
                    {
                        bool isPercent = resolvedPath.EndsWith("Percent");
                        
                        // Generic handler works for all UIBlock types since Size property is the same
                        // Store references to get both values for masking to work with mixed Raw/Percent
                        Func<Vector3> getter = () => 
                        {
                            Nova.Length3 size;
                            if (comp is Nova.UIBlock3D ub3d)
                                size = ub3d.Size;
                            else if (comp is Nova.UIBlock2D ub2d)
                                size = ub2d.Size;
                            else
                                size = ((Nova.UIBlock)comp).Size;
                            
                            var block = comp as Nova.UIBlock;
                            if (block == null) return Vector3.zero;
                            return isPercent ? block.GetSizePercentUI() : block.GetSizeValueUnits();
                        };
                        
                        Action<Vector3> setter = v =>
                        {
                            // Only set axes that are in the mask; null means "do not change"
                            float? setX = e.vectorMask.HasFlag(ComponentMask.X) ? (float?)v.x : null;
                            float? setY = e.vectorMask.HasFlag(ComponentMask.Y) ? (float?)v.y : null;
                            float? setZ = e.vectorMask.HasFlag(ComponentMask.Z) ? (float?)v.z : null;

                            var block = comp as Nova.UIBlock;
                            if (block == null) return;

                            block.SetSizeAxes(setX, setY, setZ,
                                isPercent ? Nova.Length3Extensions.LengthInputSpace.PercentUI_0_100 : Nova.Length3Extensions.LengthInputSpace.ValueUnits);
                        };
                        
                        ComponentMask mask = e.vectorMask;
                        if (mask == ComponentMask.None)
                            mask = ComponentMask.All;
                        
                        Action<Vector3> maskedSetter = v =>
                        {
                            Vector3 current = getter();
                            if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                            if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                            if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                            setter(v);
                        };
                        return TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
                    }

                    // Fast-path: Position ref-return handling  
                    if ((comp is Nova.UIBlock || comp is Nova.UIBlock2D || comp is Nova.UIBlock3D) && (resolvedPath == "Position.Percent" || resolvedPath == "Position.Raw"))
                    {
                        bool isPercent = resolvedPath.EndsWith("Percent");
                        
                        // Generic handler works for all UIBlock types since Position property is the same
                        // Store references to get both values for masking to work with mixed Raw/Percent
                        Func<Vector3> getter = () => 
                        {
                            Nova.Length3 pos;
                            if (comp is Nova.UIBlock3D ub3d)
                                pos = ub3d.Position;
                            else if (comp is Nova.UIBlock2D ub2d)
                                pos = ub2d.Position;
                            else
                                pos = ((Nova.UIBlock)comp).Position;
                            
                            var block = comp as Nova.UIBlock;
                            if (block == null) return Vector3.zero;
                            return isPercent ? block.GetPositionPercentUI() : block.GetPositionValueUnits();
                        };
                        
                        Action<Vector3> setter = v =>
                        {
                            // Only set axes that are in the mask; null means "do not change"
                            float? setX = e.vectorMask.HasFlag(ComponentMask.X) ? (float?)v.x : null;
                            float? setY = e.vectorMask.HasFlag(ComponentMask.Y) ? (float?)v.y : null;
                            float? setZ = e.vectorMask.HasFlag(ComponentMask.Z) ? (float?)v.z : null;

                            var block = comp as Nova.UIBlock;
                            if (block == null) return;

                            block.SetPositionAxes(setX, setY, setZ,
                                isPercent ? Nova.Length3Extensions.LengthInputSpace.PercentUI_0_100 : Nova.Length3Extensions.LengthInputSpace.ValueUnits);
                        };
                        
                        ComponentMask mask = e.vectorMask;
                        if (mask == ComponentMask.None)
                            mask = ComponentMask.All;
                        
                        Action<Vector3> maskedSetter = v =>
                        {
                            Vector3 current = getter();
                            if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                            if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                            if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                            setter(v);
                        };
                        return TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
                    }

                    // Fast-path: Alignment ref-return handling
                    if ((comp is Nova.UIBlock || comp is Nova.UIBlock2D || comp is Nova.UIBlock3D) && resolvedPath == "Alignment")
                    {
                        TryHandleUIBlockAlignment(e, comp);
                        return null;
                    }

                    if (TryResolveMember(comp, resolvedPath, out var owner, out var memberInfo, out var memberType))
                    {
                        // handle numeric types
                        if (memberType == typeof(float) || memberType == typeof(double) || memberType == typeof(int))
                        {
                            Func<float> getter;
                            Action<float> setter;
                            
                            if (owner is RefStructMarker marker && marker.refProperty.Name == "Size" && (marker.originalOwner is Nova.UIBlock uiBlock || marker.originalOwner is Nova.UIBlock2D uiBlock2D || marker.originalOwner is Nova.UIBlock3D uiBlock3D))
                            {
                                // Create closures that properly handle ref struct get/set
                                getter = () =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return 0f;
                                    if (memberInfo is PropertyInfo pi) return Convert.ToSingle(pi.GetValue(refStructInstance));
                                    if (memberInfo is FieldInfo fi) return Convert.ToSingle(fi.GetValue(refStructInstance));
                                    return 0f;
                                };
                                
                                setter = v =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return;
                                    
                                    if (memberInfo is PropertyInfo pi && pi.CanWrite)
                                    {
                                        pi.SetValue(refStructInstance, Convert.ChangeType(v, memberType));
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                    else if (memberInfo is FieldInfo fi)
                                    {
                                        fi.SetValue(refStructInstance, Convert.ChangeType(v, memberType));
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                };
                            }
                            else
                            {
                                getter = () => Convert.ToSingle(GetMemberValue(owner, memberInfo));
                                setter = v => SetMemberValue(owner, memberInfo, Convert.ChangeType(v, memberType));
                            }

                            bool isPercent = memberInfo.Name == "Percent";
                            if (isPercent)
                            {
                                var baseGetter = getter;
                                var baseSetter = setter;
                                getter = () => baseGetter() * 100f;          // engine -> UI
                                setter = v => baseSetter(v * 0.01f);         // UI -> engine
                            }
                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => setter(e.toFloat), e.duration));
                                return null;
                            }
                            return TweenFloatWithSource(getter, setter, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
                        }

                        // handle enums by tweening over their underlying numeric value
                        if (memberType.IsEnum)
                        {
                            Type underlying = Enum.GetUnderlyingType(memberType);
                            Func<float> getter = () => Convert.ToSingle(Convert.ChangeType(GetMemberValue(owner, memberInfo), underlying));
                            Action<float> setter = v => SetMemberValue(owner, memberInfo, Enum.ToObject(memberType, Convert.ChangeType(v, underlying)));

                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => setter(e.toFloat), e.duration));
                                return null;
                            }

                            return TweenFloatWithSource(getter, setter, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
                        }

                        // Handle any struct with enum fields (e.g., Alignment, any custom enum struct)
                        // Dynamically adapts to the number of enum fields (1-4) and tweens as float/vector
                        if (TryHandleEnumStruct(comp, e, owner, memberInfo, memberType))
                        {
                            return null; // Handled by TryHandleEnumStruct, which starts its own coroutine
                        }

                        if (memberType == typeof(Vector3))
                        {
                            Func<Vector3> getter;
                            Action<Vector3> setter;
                            bool configured = false;

                            if (owner is RefStructMarker marker && marker.refProperty.Name == "Size")
                            {
                                var ui = marker.originalOwner as Nova.UIBlock;
                                var ui2 = marker.originalOwner as Nova.UIBlock2D;
                                var ui3 = marker.originalOwner as Nova.UIBlock3D;

                                if (ui != null || ui2 != null || ui3 != null)
                                {
                                    Delegate sizeGetterDel;
                                    if (ui != null)
                                        sizeGetterDel = marker.refProperty.GetMethod.CreateDelegate(typeof(SizeGetter));
                                    else if (ui2 != null)
                                        sizeGetterDel = marker.refProperty.GetMethod.CreateDelegate(typeof(SizeGetter2D));
                                    else
                                        sizeGetterDel = marker.refProperty.GetMethod.CreateDelegate(typeof(SizeGetter3D));

                                    getter = () =>
                                    {
                                        if (ui != null)
                                        {
                                            ref Length3 size = ref ((SizeGetter)sizeGetterDel)(ui);
                                            if (memberInfo.Name == "Raw") return size.Raw;
                                            if (memberInfo.Name == "Percent") return size.Percent;
                                            if (memberInfo is PropertyInfo pi) return (Vector3)pi.GetValue(size);
                                            if (memberInfo is FieldInfo fi) return (Vector3)fi.GetValue(size);
                                            return Vector3.zero;
                                        }
                                        else if (ui2 != null)
                                        {
                                            ref Length3 size = ref ((SizeGetter2D)sizeGetterDel)(ui2);
                                            if (memberInfo.Name == "Raw") return size.Raw;
                                            if (memberInfo.Name == "Percent") return size.Percent;
                                            if (memberInfo is PropertyInfo pi) return (Vector3)pi.GetValue(size);
                                            if (memberInfo is FieldInfo fi) return (Vector3)fi.GetValue(size);
                                            return Vector3.zero;
                                        }
                                        else
                                        {
                                            ref Length3 size = ref ((SizeGetter3D)sizeGetterDel)(ui3);
                                            if (memberInfo.Name == "Raw") return size.Raw;
                                            if (memberInfo.Name == "Percent") return size.Percent;
                                            if (memberInfo is PropertyInfo pi) return (Vector3)pi.GetValue(size);
                                            if (memberInfo is FieldInfo fi) return (Vector3)fi.GetValue(size);
                                            return Vector3.zero;
                                        }
                                    };

                                    setter = v =>
                                    {
                                        if (ui != null)
                                        {
                                            ref Length3 size = ref ((SizeGetter)sizeGetterDel)(ui);
                                            if (memberInfo.Name == "Raw") size.Raw = v;
                                            else if (memberInfo.Name == "Percent") size.Percent = v;
                                            else if (memberInfo is PropertyInfo pi && pi.CanWrite) pi.SetValue(size, v);
                                            else if (memberInfo is FieldInfo fi) fi.SetValue(size, v);
                                        }
                                        else if (ui2 != null)
                                        {
                                            ref Length3 size = ref ((SizeGetter2D)sizeGetterDel)(ui2);
                                            if (memberInfo.Name == "Raw") size.Raw = v;
                                            else if (memberInfo.Name == "Percent") size.Percent = v;
                                            else if (memberInfo is PropertyInfo pi && pi.CanWrite) pi.SetValue(size, v);
                                            else if (memberInfo is FieldInfo fi) fi.SetValue(size, v);
                                        }
                                        else
                                        {
                                            ref Length3 size = ref ((SizeGetter3D)sizeGetterDel)(ui3);
                                            if (memberInfo.Name == "Raw") size.Raw = v;
                                            else if (memberInfo.Name == "Percent") size.Percent = v;
                                            else if (memberInfo is PropertyInfo pi && pi.CanWrite) pi.SetValue(size, v);
                                            else if (memberInfo is FieldInfo fi) fi.SetValue(size, v);
                                        }
                                    };
                                    configured = true;
                                }
                            }

                            if (owner is RefStructMarker marker2)
                            {
                                // generic ref struct path
                                getter = () =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker2);
                                    if (refStructInstance == null) return Vector3.zero;
                                    if (memberInfo is PropertyInfo pi) return (Vector3)pi.GetValue(refStructInstance);
                                    if (memberInfo is FieldInfo fi) return (Vector3)fi.GetValue(refStructInstance);
                                    return Vector3.zero;
                                };

                                setter = v =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker2);
                                    if (refStructInstance == null) return;

                                    if (memberInfo is PropertyInfo pi && pi.CanWrite)
                                    {
                                        pi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker2, marker2.refProperty, refStructInstance);
                                    }
                                    else if (memberInfo is FieldInfo fi)
                                    {
                                        fi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker2, marker2.refProperty, refStructInstance);
                                    }
                                };
                                configured = true;
                            }
                            else
                            {
                                getter = () => (Vector3)GetMemberValue(owner, memberInfo);
                                setter = v => SetMemberValue(owner, memberInfo, v);
                                configured = true;
                            }

                            if (!configured) return null;

                            bool isPercent = memberInfo.Name == "Percent";
                            if (isPercent)
                            {
                                var baseGetter = getter;
                                var baseSetter = setter;
                                getter = () => baseGetter() * 100f;                // engine -> UI
                                setter = v => baseSetter(v * 0.01f);               // UI -> engine
                            }

                            ComponentMask mask = e.vectorMask;
                            Action<Vector3> maskedSetter = v =>
                            {
                                Vector3 current = getter();
                                if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                                if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                                if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                                setter(v);
                            };

                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => maskedSetter(e.toVec3), e.duration));
                                return null;
                            }
                            return TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
                        }

                        if (memberType == typeof(Color))
                        {
                            Func<Color> getter;
                            Action<Color> setter;
                            
                            if (owner is RefStructMarker marker)
                            {
                                getter = () =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return Color.white;
                                    if (memberInfo is PropertyInfo pi) return (Color)pi.GetValue(refStructInstance);
                                    if (memberInfo is FieldInfo fi) return (Color)fi.GetValue(refStructInstance);
                                    return Color.white;
                                };
                                
                                setter = v =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return;
                                    
                                    if (memberInfo is PropertyInfo pi && pi.CanWrite)
                                    {
                                        pi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                    else if (memberInfo is FieldInfo fi)
                                    {
                                        fi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                };
                            }
                            else
                            {
                                getter = () => (Color)GetMemberValue(owner, memberInfo);
                                setter = v => SetMemberValue(owner, memberInfo, v);
                            }
                            
                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => setter(e.toColor), e.duration));
                                return null;
                            }
                            return TweenColorWithSource(getter, setter, e.toColor, e.duration, e.curve, e.startSource, e.fromColor);
                        }

                        if (memberType == typeof(Quaternion))
                        {
                            Func<Quaternion> getter;
                            Action<Quaternion> setter;
                            
                            if (owner is RefStructMarker marker)
                            {
                                getter = () =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return Quaternion.identity;
                                    if (memberInfo is PropertyInfo pi) return (Quaternion)pi.GetValue(refStructInstance);
                                    if (memberInfo is FieldInfo fi) return (Quaternion)fi.GetValue(refStructInstance);
                                    return Quaternion.identity;
                                };
                                
                                setter = v =>
                                {
                                    object refStructInstance = GetRefStructInstance(marker);
                                    if (refStructInstance == null) return;
                                    
                                    if (memberInfo is PropertyInfo pi && pi.CanWrite)
                                    {
                                        pi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                    else if (memberInfo is FieldInfo fi)
                                    {
                                        fi.SetValue(refStructInstance, v);
                                        SetRefStructMember(marker, marker.refProperty, refStructInstance);
                                    }
                                };
                            }
                            else
                            {
                                getter = () => (Quaternion)GetMemberValue(owner, memberInfo);
                                setter = v => SetMemberValue(owner, memberInfo, v);
                            }
                            
                            Func<Quaternion, Quaternion, float, Quaternion> slerp = (a, b, t) => Quaternion.SlerpUnclamped(a, b, t);
                            Quaternion toQ = Quaternion.Euler(e.toVec3);
                            Quaternion fromQ = Quaternion.Euler(e.fromVec3);
                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => setter(toQ), e.duration));
                                return null;
                            }
                            return TweenQuatWithSource(getter, setter, toQ, e.duration, slerp, e.curve, e.startSource, fromQ);
                        }

                        if (memberType == typeof(bool))
                        {
                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => SetMemberValue(owner, memberInfo, e.toBool), e.duration));
                                return null;
                            }
                            if (e.propertyMode == CustomPropertyMode.ToggleAtHalf)
                            {
                                SetMemberValue(owner, memberInfo, e.fromBool);
                                StartCoroutine(ApplyActionAfterSeconds(() => SetMemberValue(owner, memberInfo, e.toBool), e.duration * 0.5f));
                                return null;
                            }

                            StartCoroutine(DriveBoolWithCurve(owner, memberInfo, e.fromBool, e.toBool, e.duration, e.curve));
                            return null;
                        }
                        if (memberType == typeof(void) && memberInfo is MethodInfo method)
                        {
                            // Invoke method based on configured timing
                            StartCoroutine(InvokeMethodWithTiming(method, owner, e.duration, e.curve, e.methodInvokeTiming));
                            return null;
                        }

                        Debug.LogWarning($"Animate: Unsupported property type '{memberType.Name}' for CustomProperty on {comp.GetType().Name} (path '{e.propertyName}').");
                        return null;
                    }

                    Debug.LogWarning($"Animate: Property/Field path '{e.propertyName}' not found on component {comp.GetType().Name}.");
                    return null;
                }

                case TweenType.Float:
                default:
                    // For generic float, user must wire getter/setter from code.
                    return null;
            }
        }

        // ----------------
        // Convenience helpers
        // ----------------

        // Position (with legacy signature)
        public Coroutine TweenPosition(Transform tgt, Vector3 to, float duration, AnimationCurve curve = null, bool local = true, bool useExplicitFrom = false, Vector3 explicitFrom = default, Action onComplete = null)
        {
            Func<Vector3> getter = local ? (Func<Vector3>)(() => tgt.localPosition) : () => tgt.position;
            Action<Vector3> setter = local ? (Action<Vector3>)(v => tgt.localPosition = v) : v => tgt.position = v;
            if (useExplicitFrom)
                return Tween(getter, setter, explicitFrom, to, duration, Vector3.LerpUnclamped, curve, onComplete);
            return Tween(getter, setter, to, duration, Vector3.LerpUnclamped, curve, onComplete);
        }

        private Coroutine TweenPositionWithSource(Transform tgt, Vector3 to, float duration, AnimationCurve curve, bool local, StartSource source, Vector3 explicitFrom)
        {
            Func<Vector3> getter = local ? (Func<Vector3>)(() => tgt.localPosition) : () => tgt.position;
            Action<Vector3> setter = local ? (Action<Vector3>)(v => tgt.localPosition = v) : v => tgt.position = v;
            return ApplyStartSource(getter, setter, to, duration, Vector3.LerpUnclamped, curve, source, explicitFrom);
        }

        // Rotation (slerp) - legacy
        public Coroutine TweenRotation(Transform tgt, Quaternion to, float duration, AnimationCurve curve = null, bool local = true, bool useExplicitFrom = false, Quaternion explicitFrom = default, Action onComplete = null)
        {
            Func<Quaternion> getter = local ? (Func<Quaternion>)(() => tgt.localRotation) : () => tgt.rotation;
            Action<Quaternion> setter = local ? (Action<Quaternion>)(q => tgt.localRotation = q) : q => tgt.rotation = q;
            Func<Quaternion, Quaternion, float, Quaternion> slerp = (a, b, t) => Quaternion.SlerpUnclamped(a, b, t);
            if (useExplicitFrom)
                return Tween(getter, setter, explicitFrom, to, duration, slerp, curve, onComplete);
            return Tween(getter, setter, to, duration, slerp, curve, onComplete);
        }

        private Coroutine TweenRotationWithSource(Transform tgt, Quaternion to, float duration, AnimationCurve curve, bool local, StartSource source, Quaternion explicitFrom)
        {
            Func<Quaternion> getter = local ? (Func<Quaternion>)(() => tgt.localRotation) : () => tgt.rotation;
            Action<Quaternion> setter = local ? (Action<Quaternion>)(q => tgt.localRotation = q) : q => tgt.rotation = q;
            Func<Quaternion, Quaternion, float, Quaternion> slerp = (a, b, t) => Quaternion.SlerpUnclamped(a, b, t);
            return ApplyStartSource(getter, setter, to, duration, slerp, curve, source, explicitFrom);
        }

        // Scale - legacy
        public Coroutine TweenScale(Transform tgt, Vector3 to, float duration, AnimationCurve curve = null, bool useExplicitFrom = false, Vector3 explicitFrom = default, Action onComplete = null)
        {
            Func<Vector3> getter = () => tgt.localScale;
            Action<Vector3> setter = v => tgt.localScale = v;
            if (useExplicitFrom)
                return Tween(getter, setter, explicitFrom, to, duration, Vector3.LerpUnclamped, curve, onComplete);
            return Tween(getter, setter, to, duration, Vector3.LerpUnclamped, curve, onComplete);
        }

        private Coroutine TweenScaleWithSource(Transform tgt, Vector3 to, float duration, AnimationCurve curve, StartSource source, Vector3 explicitFrom)
        {
            Func<Vector3> getter = () => tgt.localScale;
            Action<Vector3> setter = v => tgt.localScale = v;
            return ApplyStartSource(getter, setter, to, duration, Vector3.LerpUnclamped, curve, source, explicitFrom);
        }

        // Float (useful for CanvasGroup alpha, material floats, etc.) - legacy
        public Coroutine TweenFloat(Func<float> getter, Action<float> setter, float to, float duration, AnimationCurve curve = null, bool useExplicitFrom = false, float explicitFrom = 0f, Action onComplete = null)
        {
            Func<float, float, float, float> lerp = Mathf.LerpUnclamped;
            if (useExplicitFrom)
                return Tween(getter, setter, explicitFrom, to, duration, lerp, curve, onComplete);
            return Tween(getter, setter, to, duration, lerp, curve, onComplete);
        }

        private Coroutine TweenFloatWithSource(Func<float> getter, Action<float> setter, float to, float duration, AnimationCurve curve, StartSource source, float explicitFrom)
        {
            Func<float, float, float, float> lerp = Mathf.LerpUnclamped;
            return ApplyStartSource(getter, setter, to, duration, lerp, curve, source, explicitFrom);
        }

        // Color (Renderer material color) - legacy
        public Coroutine TweenColor(Renderer renderer, Color to, float duration, AnimationCurve curve = null, bool useExplicitFrom = false, Color explicitFrom = default, Action onComplete = null)
        {
            if (renderer == null) return null;
            Func<Color> getter = () => renderer.material.color;
            Action<Color> setter = c => renderer.material.color = c;
            if (useExplicitFrom)
                return Tween(getter, setter, explicitFrom, to, duration, Color.LerpUnclamped, curve, onComplete);
            return Tween(getter, setter, to, duration, Color.LerpUnclamped, curve, onComplete);
        }

        private Coroutine TweenColorWithSource(Func<Color> getter, Action<Color> setter, Color to, float duration, AnimationCurve curve, StartSource source, Color explicitFrom)
        {
            return ApplyStartSource(getter, setter, to, duration, Color.LerpUnclamped, curve, source, explicitFrom);
        }

        // CanvasGroup alpha helper - legacy
        public Coroutine TweenCanvasAlpha(CanvasGroup cg, float to, float duration, AnimationCurve curve = null, bool useExplicitFrom = false, float explicitFrom = 0f, Action onComplete = null)
        {
            if (cg == null) return null;
            return TweenFloat(() => cg.alpha, v => cg.alpha = v, to, duration, curve, useExplicitFrom, explicitFrom, onComplete);
        }

        private Coroutine TweenCanvasAlphaWithSource(CanvasGroup cg, float to, float duration, AnimationCurve curve, StartSource source, float explicitFrom)
        {
            if (cg == null) return null;
            return TweenFloatWithSource(() => cg.alpha, v => cg.alpha = v, to, duration, curve, source, explicitFrom);
        }

        // Renderer material color with property name and material index - legacy
        public Coroutine TweenMaterialColor(Renderer renderer, int materialIndex, string primaryProperty, string[] extraProperties, Color to, float duration, AnimationCurve curve = null, bool useExplicitFrom = false, Color explicitFrom = default, bool useCurrentAsFrom = false, Action onComplete = null)
        {
            if (renderer == null || renderer.materials == null || renderer.materials.Length == 0) return null;
            var mats = renderer.sharedMaterials;
            materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
            Material mat = mats[materialIndex];
            // build property list
            var props = new List<string>();
            if (!string.IsNullOrEmpty(primaryProperty)) props.Add(primaryProperty);
            if (extraProperties != null && extraProperties.Length > 0)
                props.AddRange(extraProperties.Where(p => !string.IsNullOrEmpty(p)));
            if (props.Count == 0) props.Add("_Color");
            string sampleProp = props[0];
            Func<Color> getter = () =>
            {
                if (mat != null && mat.HasProperty(sampleProp))
                {
                    // Prefer the currently rendered value (PropertyBlock), fallback to material value.
                    var mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb, materialIndex);
                    if (mpb.HasProperty(sampleProp))
                        return mpb.GetColor(sampleProp);
                    return mat.GetColor(sampleProp);
                }
                return Color.white;
            };
            Action<Color> setter = c =>
            {
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb, materialIndex);
                foreach (var p in props)
                {
                    if (mat != null && mat.HasProperty(p))
                        mpb.SetColor(p, c);
                }
                renderer.SetPropertyBlock(mpb, materialIndex);
            };

            if (useExplicitFrom && !useCurrentAsFrom)
                return Tween(getter, setter, explicitFrom, to, duration, Color.LerpUnclamped, curve, onComplete);

            // use current as from
            Color currentFrom = getter();
            return Tween(getter, setter, currentFrom, to, duration, Color.LerpUnclamped, curve, onComplete);
        }

        private Coroutine TweenMaterialColorWithSource(Renderer renderer, int materialIndex, string primaryProperty, string[] extraProperties, Color to, float duration, AnimationCurve curve, StartSource source, Color explicitFrom)
        {
            if (renderer == null || renderer.materials == null || renderer.materials.Length == 0) return null;
            var mats = renderer.sharedMaterials;
            materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
            Material mat = mats[materialIndex];
            // build property list
            var props = new List<string>();
            if (!string.IsNullOrEmpty(primaryProperty)) props.Add(primaryProperty);
            if (extraProperties != null && extraProperties.Length > 0)
                props.AddRange(extraProperties.Where(p => !string.IsNullOrEmpty(p)));
            if (props.Count == 0) props.Add("_Color");
            string sampleProp = props[0];
            Func<Color> getter = () =>
            {
                if (mat != null && mat.HasProperty(sampleProp))
                {
                    var mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb, materialIndex);
                    if (mpb.HasProperty(sampleProp))
                        return mpb.GetColor(sampleProp);
                    return mat.GetColor(sampleProp);
                }
                return Color.white;
            };
            Action<Color> setter = c =>
            {
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb, materialIndex);
                foreach (var p in props)
                {
                    if (mat != null && mat.HasProperty(p))
                        mpb.SetColor(p, c);
                }
                renderer.SetPropertyBlock(mpb, materialIndex);
            };
            return ApplyStartSource(getter, setter, to, duration, Color.LerpUnclamped, curve, source, explicitFrom);
        }

        // Generic start source handler
        private Coroutine ApplyStartSource<T>(Func<T> getter, Action<T> setter, T to, float duration, Func<T, T, float, T> lerpFunc, AnimationCurve curve, StartSource source, T explicitFrom)
        {
            switch (source)
            {
                case StartSource.Ignore:
                    // Use explicit start/end values provided in the inspector
                    return Tween(getter, setter, explicitFrom, to, duration, lerpFunc, curve);
                case StartSource.Start:
                    // Override start with current value, keep inspector end
                    {
                        T current = getter();
                        return Tween(getter, setter, current, to, duration, lerpFunc, curve);
                    }
                case StartSource.End:
                    // Override end with current value, keep inspector start.
                    {
                        T current = getter();
                        return Tween(getter, setter, explicitFrom, current, duration, lerpFunc, curve);
                    }
                default:
                    return null;
            }
        }

        private Coroutine TweenVec3WithSource(Func<Vector3> getter, Action<Vector3> setter, Vector3 to, float duration, AnimationCurve curve, StartSource source, Vector3 explicitFrom)
        {
            return ApplyStartSource(getter, setter, to, duration, Vector3.LerpUnclamped, curve, source, explicitFrom);
        }

        private Coroutine TweenQuatWithSource(Func<Quaternion> getter, Action<Quaternion> setter, Quaternion to, float duration, Func<Quaternion, Quaternion, float, Quaternion> slerp, AnimationCurve curve, StartSource source, Quaternion explicitFrom)
        {
            return ApplyStartSource(getter, setter, to, duration, slerp, curve, source, explicitFrom);
        }

        /// <summary>
        /// Preview tween in editor that auto-resets after completion.
        /// Captures current state before tweening and restores it after a delay.
        /// </summary>
        private IEnumerator PreviewTweenWithReset(TweenEntry e)
        {
#if UNITY_EDITOR
            var go = e.targetObject;
            if (go == null) yield break;
            
            var initialState = CaptureGameObjectState(go, e.type);
            currentPreviewTween = new PreviewTweenState
            {
                entry = e,
                startTime = Time.realtimeSinceStartup,
                initialState = initialState
            };
            
            // Register update hook
            UnityEditor.EditorApplication.update += UpdatePreviewTweenFrame;
#endif
            
            yield return null;
        }
        
        private void UpdatePreviewTweenFrame()
        {
#if UNITY_EDITOR
            if (currentPreviewTween == null) return;
            
            var e = currentPreviewTween.entry;
            if (e?.targetObject == null)
            {
                UnityEditor.EditorApplication.update -= UpdatePreviewTweenFrame;
                currentPreviewTween = null;
                return;
            }
            
            float elapsed = Time.realtimeSinceStartup - currentPreviewTween.startTime;
            
            // Tween is complete, wait 1 second then restore
            if (elapsed >= e.duration + 1f)
            {
                RestoreGameObjectState(e.targetObject, e.type, currentPreviewTween.initialState);
                UnityEditor.EditorApplication.update -= UpdatePreviewTweenFrame;
                currentPreviewTween = null;
            }
#endif
        }

        private object CaptureGameObjectState(GameObject go, TweenType type)
        {
            if (go == null) return null;
            switch (type)
            {
                case TweenType.Position:
                    return go.transform.position;
                case TweenType.LocalPosition:
                    return go.transform.localPosition;
                case TweenType.RotationEuler:
                    return go.transform.rotation;
                case TweenType.LocalRotationEuler:
                    return go.transform.localRotation;
                case TweenType.Scale:
                    return go.transform.localScale;
                case TweenType.CanvasGroupAlpha:
                    var cg = go.GetComponent<CanvasGroup>();
                    return cg != null ? cg.alpha : 1f;
                case TweenType.RendererColor:
                case TweenType.MaterialFloat:
                    var r = go.GetComponent<Renderer>();
                    return r != null ? r.material.color : Color.white;
                default:
                    return null;
            }
        }

        private void RestoreGameObjectState(GameObject go, TweenType type, object state)
        {
            if (go == null || state == null) return;
            switch (type)
            {
                case TweenType.Position:
                    go.transform.position = (Vector3)state;
                    break;
                case TweenType.LocalPosition:
                    go.transform.localPosition = (Vector3)state;
                    break;
                case TweenType.RotationEuler:
                    go.transform.rotation = (Quaternion)state;
                    break;
                case TweenType.LocalRotationEuler:
                    go.transform.localRotation = (Quaternion)state;
                    break;
                case TweenType.Scale:
                    go.transform.localScale = (Vector3)state;
                    break;
                case TweenType.CanvasGroupAlpha:
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = (float)state;
                    break;
                case TweenType.RendererColor:
                case TweenType.MaterialFloat:
                    var r = go.GetComponent<Renderer>();
                    if (r != null) r.material.color = (Color)state;
                    break;
            }
        }
    }
}
