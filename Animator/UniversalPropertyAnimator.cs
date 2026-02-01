using System;
using System.Reflection;
using UnityEngine;

[AddComponentMenu("Animation/Universal Property Animator")]
public class UniversalPropertyAnimator : MonoBehaviour
{
    public enum TargetKind
    {
        ComponentMember,    // any field/property on a Component by name
        MaterialProperty,   // shader property on a Renderer material
        TransformPosition,  // position of a Transform
        TransformRotation   // rotation of a Transform
    }

    public enum ValueType
    {
        Float,
        Vector3,
        Quaternion,
        Color
    }

    public enum InterpMode
    {
        Lerp,
        Slerp // only meaningful for Quaternion, ignored otherwise
    }

    public enum PlayMode
    {
        Once,
        Loop,
        PingPong
    }

    [Header("Target")]
    public TargetKind targetKind = TargetKind.ComponentMember;

    [Tooltip("Component that owns the member to animate, or the Transform to move/rotate.")]
    public Component targetComponent;

    [Tooltip("Field or property name on the component for ComponentMember mode.")]
    public string memberName;

    [Tooltip("Renderer whose material property will be animated.")]
    public Renderer targetRenderer;

    [Tooltip("Index of material on the renderer.")]
    public int materialIndex = 0;

    [Tooltip("Shader property name (e.g. _Color, _Intensity).")]
    public string shaderProperty;

    [Tooltip("How we treat the value being animated.")]
    public ValueType valueType = ValueType.Float;

    [Tooltip("Lerp or Slerp (only affects Quaternions).")]
    public InterpMode interpMode = InterpMode.Lerp;

    [Header("Transform Options")]
    [Tooltip("Use local space for Transform position/rotation targets.")]
    public bool useLocalSpace = true;

    [Header("Timing")]
    [Min(0.0001f)]
    public float duration = 1f;

    public PlayMode playMode = PlayMode.Loop;

    [Tooltip("Automatically play when enabled.")]
    public bool playOnEnable = true;

    [Tooltip("Use unscaled time (ignores Time.timeScale).")]
    public bool useUnscaledTime = false;

    [Header("Validation")]
    [Tooltip("Run HealthCheck automatically in Awake to validate bindings and log detailed errors.")]
    public bool runHealthCheckOnAwake = false;

    [Header("Curve")]
    [Tooltip("Remaps normalized time (0-1) to an eased value.")]
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("From / To Values")]
    public float fromFloat;
    public float toFloat;

    public Vector3 fromVector;
    public Vector3 toVector;

    public Quaternion fromQuaternion = Quaternion.identity;
    public Quaternion toQuaternion = Quaternion.identity;

    public Color fromColor = Color.white;
    public Color toColor = Color.black;

    [Header("Noise")]
    [Tooltip("Enable procedural noise on top of the base interpolation.")]
    public bool useNoise = false;

    [Tooltip("Max multiplicative deviation from base value, e.g. 0.1 = ±10%.")]
    [Range(0f, 1f)]
    public float noisePercent = 0.1f;

    [Tooltip("How fast the noise changes over time.")]
    public float noiseSpeed = 1f;

    [Tooltip("Seed for noise so multiple instances can be decorrelated.")]
    public int noiseSeed = 0;

    [Tooltip("For Vector3/Color: use separate noise per component.")]
    public bool noisePerComponent = true;

    // Internal state
    float _elapsed;
    bool _playing;

    // Reflection cache
    FieldInfo _fieldInfo;
    PropertyInfo _propertyInfo;

    // Shader cache
    MaterialPropertyBlock _mpb;
    int _shaderId;
    bool _shaderIdValid;

    void Awake()
    {
        CacheComponentMember();
        CacheShaderProperty();

        if (runHealthCheckOnAwake)
        {
            HealthCheck();
        }
    }

    void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Keep caches fresh when values change in the Inspector
        CacheComponentMember();
        CacheShaderProperty();
    }
#endif

    /// <summary>Start (or restart) the animation from t=0.</summary>
    public void Play()
    {
        _elapsed = 0f;
        _playing = true;
    }

    /// <summary>Stop animation at current value.</summary>
    public void Stop()
    {
        _playing = false;
    }

    /// <summary>Force apply the 'from' value immediately.</summary>
    public void ApplyFrom()
    {
        ApplyValueAt(0f);
    }

    /// <summary>Force apply the 'to' value immediately.</summary>
    public void ApplyTo()
    {
        ApplyValueAt(1f);
    }

    void Update()
    {
        if (!_playing || duration <= 0f)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed += dt;

        float rawT = _elapsed / duration;
        float loopedT;

        switch (playMode)
        {
            case PlayMode.Once:
                loopedT = Mathf.Clamp01(rawT);
                if (_elapsed >= duration)
                {
                    _playing = false;
                }
                break;

            case PlayMode.Loop:
                loopedT = Mathf.Repeat(rawT, 1f);
                break;

            case PlayMode.PingPong:
                loopedT = Mathf.PingPong(rawT, 1f);
                break;

            default:
                loopedT = Mathf.Clamp01(rawT);
                break;
        }

        float easedT = curve != null ? curve.Evaluate(loopedT) : loopedT;
        ApplyValueAt(easedT);
    }

    void ApplyValueAt(float t)
    {
        float timeForNoise = _elapsed * noiseSpeed;

        switch (valueType)
        {
            case ValueType.Float:
                float fBase = Mathf.Lerp(fromFloat, toFloat, t);
                float fFinal = useNoise ? AddNoiseFloat(fBase, timeForNoise) : fBase;
                ApplyFloat(fFinal);
                break;

            case ValueType.Vector3:
                Vector3 vBase = Vector3.Lerp(fromVector, toVector, t);
                Vector3 vFinal = useNoise ? AddNoiseVector(vBase, timeForNoise) : vBase;
                ApplyVector(vFinal);
                break;

            case ValueType.Quaternion:
                Quaternion qBase = interpMode == InterpMode.Slerp
                    ? Quaternion.Slerp(fromQuaternion, toQuaternion, t)
                    : Quaternion.Lerp(fromQuaternion, toQuaternion, t);

                Quaternion qFinal = useNoise ? AddNoiseQuaternion(qBase, timeForNoise) : qBase;
                ApplyQuaternion(qFinal);
                break;

            case ValueType.Color:
                Color cBase = Color.Lerp(fromColor, toColor, t);
                Color cFinal = useNoise ? AddNoiseColor(cBase, timeForNoise) : cBase;
                ApplyColor(cFinal);
                break;
        }
    }

    /// <summary>
    /// Validates current configuration and logs detailed errors with available options.
    /// </summary>
    [ContextMenu("Health Check / Validate Binding")]
    public void HealthCheck()
    {
        switch (targetKind)
        {
            case TargetKind.ComponentMember:
                HealthCheckComponentMember();
                break;
            case TargetKind.MaterialProperty:
                HealthCheckMaterialProperty();
                break;
            case TargetKind.TransformPosition:
            case TargetKind.TransformRotation:
                HealthCheckTransformTarget();
                break;
        }
    }

    #region Noise

    float AddNoiseFloat(float value, float time)
    {
        if (noisePercent <= 0f || noiseSpeed <= 0f) return value;

        float n = Perlin01(noiseSeed, time) * 2f - 1f; // [-1,1]
        float factor = 1f + n * noisePercent;
        return value * factor;
    }

    Vector3 AddNoiseVector(Vector3 v, float time)
    {
        if (noisePercent <= 0f || noiseSpeed <= 0f) return v;

        if (noisePerComponent)
        {
            float nx = Perlin01(noiseSeed + 17, time) * 2f - 1f;
            float ny = Perlin01(noiseSeed + 31, time) * 2f - 1f;
            float nz = Perlin01(noiseSeed + 47, time) * 2f - 1f;

            return new Vector3(
                v.x * (1f + nx * noisePercent),
                v.y * (1f + ny * noisePercent),
                v.z * (1f + nz * noisePercent)
            );
        }
        else
        {
            float n = Perlin01(noiseSeed, time) * 2f - 1f;
            float factor = 1f + n * noisePercent;
            return v * factor;
        }
    }

    Quaternion AddNoiseQuaternion(Quaternion q, float time)
    {
        if (noisePercent <= 0f || noiseSpeed <= 0f) return q;

        float n = Perlin01(noiseSeed, time) * 2f - 1f;
        float angleDeg = n * noisePercent * 10f; // 0–10 degrees-ish; tweak if desired

        Vector3 axis = new Vector3(
            Perlin01(noiseSeed + 61, time) * 2f - 1f,
            Perlin01(noiseSeed + 73, time) * 2f - 1f,
            Perlin01(noiseSeed + 89, time) * 2f - 1f
        ).normalized;

        if (axis.sqrMagnitude < 1e-4f)
            axis = Vector3.up;

        Quaternion noiseRot = Quaternion.AngleAxis(angleDeg, axis);
        return noiseRot * q;
    }

    Color AddNoiseColor(Color c, float time)
    {
        if (noisePercent <= 0f || noiseSpeed <= 0f) return c;

        if (noisePerComponent)
        {
            float nr = Perlin01(noiseSeed + 5, time) * 2f - 1f;
            float ng = Perlin01(noiseSeed + 11, time) * 2f - 1f;
            float nb = Perlin01(noiseSeed + 23, time) * 2f - 1f;
            float na = Perlin01(noiseSeed + 29, time) * 2f - 1f;

            return new Color(
                c.r * (1f + nr * noisePercent),
                c.g * (1f + ng * noisePercent),
                c.b * (1f + nb * noisePercent),
                c.a * (1f + na * noisePercent)
            );
        }
        else
        {
            float n = Perlin01(noiseSeed, time) * 2f - 1f;
            float factor = 1f + n * noisePercent;
            return c * factor;
        }
    }

    float Perlin01(float xSeed, float t)
    {
        return Mathf.PerlinNoise(xSeed + 0.1234f, t);
    }

    float Perlin01(int intSeed, float t)
    {
        return Mathf.PerlinNoise(intSeed + 0.1234f, t);
    }

    #endregion

    #region Apply to Targets

    void ApplyFloat(float value)
    {
        switch (targetKind)
        {
            case TargetKind.ComponentMember:
                SetMemberValue(value);
                break;
            case TargetKind.MaterialProperty:
                SetShaderFloat(value);
                break;
            case TargetKind.TransformPosition:
                break;
            case TargetKind.TransformRotation:
                break;
        }
    }

    void ApplyVector(Vector3 value)
    {
        switch (targetKind)
        {
            case TargetKind.ComponentMember:
                SetMemberValue(value);
                break;

            case TargetKind.MaterialProperty:
                SetShaderVector(value);
                break;

            case TargetKind.TransformPosition:
                if (targetComponent is Transform trP)
                {
                    if (useLocalSpace)
                        trP.localPosition = value;
                    else
                        trP.position = value;
                }
                break;

            case TargetKind.TransformRotation:
                if (targetComponent is Transform trR)
                {
                    if (useLocalSpace)
                        trR.localEulerAngles = value;
                    else
                        trR.eulerAngles = value;
                }
                break;
        }
    }

    void ApplyQuaternion(Quaternion value)
    {
        switch (targetKind)
        {
            case TargetKind.ComponentMember:
                SetMemberValue(value);
                break;

            case TargetKind.MaterialProperty:
                SetShaderVector(new Vector4(value.x, value.y, value.z, value.w));
                break;

            case TargetKind.TransformPosition:
                break;

            case TargetKind.TransformRotation:
                if (targetComponent is Transform tr)
                {
                    if (useLocalSpace)
                        tr.localRotation = value;
                    else
                        tr.rotation = value;
                }
                break;
        }
    }

    void ApplyColor(Color value)
    {
        switch (targetKind)
        {
            case TargetKind.ComponentMember:
                SetMemberValue(value);
                break;

            case TargetKind.MaterialProperty:
                SetShaderColor(value);
                break;

            case TargetKind.TransformPosition:
            case TargetKind.TransformRotation:
                break;
        }
    }

    #endregion

    #region Validation Helpers

    bool HealthCheckComponentMember()
    {
        if (targetComponent == null)
        {
            var all = GetComponents<Component>();
            string available = "(none)";
            if (all != null && all.Length > 0)
            {
                available = string.Join(", ", Array.ConvertAll(all, c => c != null ? c.GetType().Name : "null"));
            }

            Debug.LogError($"[UniversalPropertyAnimator][500] TargetKind=ComponentMember but targetComponent is null on '{name}'. Available components on this GameObject: {available}", this);
            return false;
        }

        CacheComponentMember();

        if (_fieldInfo == null && _propertyInfo == null)
        {
            var type = targetComponent.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fields = type.GetFields(flags);
            var props = type.GetProperties(flags);

            string fieldList = fields.Length > 0
                ? string.Join("\n  ", Array.ConvertAll(fields, f => $"{f.FieldType.Name} {f.Name}"))
                : "(no fields)";

            string propList = props.Length > 0
                ? string.Join("\n  ", Array.ConvertAll(props, p => $"{p.PropertyType.Name} {p.Name}"))
                : "(no properties)";

            Debug.LogError($"[UniversalPropertyAnimator][500] Member '{memberName}' not found on component type {type.Name} for GameObject '{name}'.\n"
                + "Available fields:\n  " + fieldList + "\n"
                + "Available properties:\n  " + propList,
                this);
            return false;
        }

        Debug.LogError($"[UniversalPropertyAnimator][200 OK] ComponentMember binding valid on '{name}'. Component={targetComponent.GetType().Name}, member='{memberName}', valueType={valueType}", this);
        return true;
    }

    bool HealthCheckMaterialProperty()
    {
        if (targetRenderer == null)
        {
            var all = GetComponentsInChildren<Renderer>(true);
            string available = all != null && all.Length > 0
                ? string.Join("\n  ", Array.ConvertAll(all, r => r != null ? $"{r.GetType().Name} on {r.gameObject.name}" : "null"))
                : "(no Renderers found on this GameObject or children)";

            Debug.LogError($"[UniversalPropertyAnimator][500] TargetKind=MaterialProperty but targetRenderer is null on '{name}'.\nAvailable Renderers below this object:\n  {available}", this);
            return false;
        }

        var mats = targetRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
        {
            Debug.LogError($"[UniversalPropertyAnimator][500] Renderer on '{name}' has no materials assigned.", this);
            return false;
        }

        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            string matNames = string.Join(", ", Array.ConvertAll(mats, m => m != null ? m.name : "null"));
            Debug.LogError($"[UniversalPropertyAnimator][500] materialIndex={materialIndex} is out of range for Renderer on '{name}'. Material count = {mats.Length}. Materials: {matNames}", this);
            return false;
        }

        var mat = mats[materialIndex];
        if (mat == null)
        {
            Debug.LogError($"[UniversalPropertyAnimator][500] Material at index {materialIndex} is null on Renderer '{targetRenderer.name}'.", this);
            return false;
        }

        if (string.IsNullOrEmpty(shaderProperty))
        {
            LogShaderPropertyOptions(mat, "Shader property name is empty.");
            return false;
        }

        var shader = mat.shader;
        if (shader == null)
        {
            Debug.LogError($"[UniversalPropertyAnimator][500] Material '{mat.name}' on '{name}' has no shader.", this);
            return false;
        }

        bool hasProp = mat.HasProperty(shaderProperty);
        if (!hasProp)
        {
            LogShaderPropertyOptions(mat, $"Shader property '{shaderProperty}' not found on shader '{shader.name}'.");
            return false;
        }

        Debug.LogError($"[UniversalPropertyAnimator][200 OK] MaterialProperty binding valid on '{name}'. Renderer='{targetRenderer.name}', materialIndex={materialIndex}, shader='{shader.name}', property='{shaderProperty}', valueType={valueType}", this);
        return true;
    }

    bool HealthCheckTransformTarget()
    {
        if (!(targetComponent is Transform))
        {
            var tr = GetComponent<Transform>();
            if (tr != null)
            {
                Debug.LogError($"[UniversalPropertyAnimator][500] TargetKind={targetKind} expects targetComponent to be a Transform on '{name}'. Suggested fix: set targetComponent = this.transform.", this);
            }
            else
            {
                Debug.LogError($"[UniversalPropertyAnimator][500] TargetKind={targetKind} expects targetComponent to be a Transform, but none is assigned and this GameObject has no Transform (unexpected).", this);
            }
            return false;
        }

        var targetTransform = (Transform)targetComponent;
        Debug.LogError($"[UniversalPropertyAnimator][200 OK] Transform binding valid on '{name}'. Mode={targetKind}, Transform={targetTransform.name}", this);
        return true;
    }

    void LogShaderPropertyOptions(Material mat, string prefix)
    {
        var shader = mat.shader;
        if (shader == null)
        {
            Debug.LogError($"[UniversalPropertyAnimator] {prefix} Material '{mat.name}' has no shader.", this);
            return;
        }

        int count = shader.GetPropertyCount();
        if (count == 0)
        {
            Debug.LogError($"[UniversalPropertyAnimator] {prefix} Shader '{shader.name}' on material '{mat.name}' has no exposed properties.", this);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[UniversalPropertyAnimator] {prefix}");
        sb.AppendLine($"Shader: {shader.name} (material '{mat.name}')");
        sb.AppendLine("Available properties:");

        for (int i = 0; i < count; i++)
        {
            string propName = shader.GetPropertyName(i);
            var propType = shader.GetPropertyType(i);
            sb.AppendLine($"  {propType} {propName}");
        }

        Debug.LogError(sb.ToString(), this);
    }

    #endregion

    #region Reflection / Shader Binding

    void CacheComponentMember()
    {
        _fieldInfo = null;
        _propertyInfo = null;

        if (targetKind != TargetKind.ComponentMember || targetComponent == null || string.IsNullOrEmpty(memberName))
            return;

        var type = targetComponent.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _fieldInfo = type.GetField(memberName, flags);
        if (_fieldInfo == null)
        {
            _propertyInfo = type.GetProperty(memberName, flags);
        }

        if (_fieldInfo == null && _propertyInfo == null)
        {
            Debug.LogWarning($"UniversalPropertyAnimator: Could not find field or property '{memberName}' on {type.Name}.", this);
        }
    }

    void SetMemberValue(object boxedValue)
    {
        if (targetComponent == null)
            return;

        if (_fieldInfo != null)
        {
            _fieldInfo.SetValue(targetComponent, boxedValue);
        }
        else if (_propertyInfo != null && _propertyInfo.CanWrite)
        {
            _propertyInfo.SetValue(targetComponent, boxedValue);
        }
        else
        {
            CacheComponentMember();
        }
    }

    void CacheShaderProperty()
    {
        _shaderIdValid = false;

        if (targetKind != TargetKind.MaterialProperty || targetRenderer == null || string.IsNullOrEmpty(shaderProperty))
            return;

        _shaderId = Shader.PropertyToID(shaderProperty);
        _shaderIdValid = true;

        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
    }

    void GetMPB()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(_mpb, materialIndex);
    }

    void ApplyMPB()
    {
        targetRenderer.SetPropertyBlock(_mpb, materialIndex);
    }

    void SetShaderFloat(float v)
    {
        if (!_shaderIdValid || targetRenderer == null)
            return;

        GetMPB();
        _mpb.SetFloat(_shaderId, v);
        ApplyMPB();
    }

    void SetShaderVector(Vector4 v)
    {
        if (!_shaderIdValid || targetRenderer == null)
            return;

        GetMPB();
        _mpb.SetVector(_shaderId, v);
        ApplyMPB();
    }

    void SetShaderVector(Vector3 v)
    {
        SetShaderVector((Vector4)v);
    }

    void SetShaderColor(Color c)
    {
        if (!_shaderIdValid || targetRenderer == null)
            return;

        GetMPB();
        _mpb.SetColor(_shaderId, c);
        ApplyMPB();
    }

    #endregion
}
