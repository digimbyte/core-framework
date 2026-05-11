using UnityEngine;

namespace Core.Animator
{
    [AddComponentMenu("Core/Animator/RotateOnDrag")]
    public class RotateOnDrag : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Pivot transform that will be rotated. If empty, this GameObject is used.")]
        public Transform pivot;
        [Tooltip("Camera used for raycasts. If empty, Camera.main will be used.")]
        public Camera raycastCamera;
        [Tooltip("Transform whose collider must be hit to start a drag. If empty, this GameObject is used.")]
        public Transform raycastTarget;
        public LayerMask raycastLayer = ~0;
        public bool requireRaycastHit = true;

        [Header("Rotation")]
        [Tooltip("Degrees applied per pixel of pointer movement.")]
        public float sensitivity = 0.2f;
        [Tooltip("Responsiveness to input changes. Larger = more immediate response.")]
        public float smoothing = 12f;
        public bool affectX = true;
        public bool affectY = true;
        public bool affectZ = false;

        [Header("Input Options")]
        [Tooltip("Invert rotation direction for vertical drag (X axis).")]
        public bool invertX = false;
        [Tooltip("Invert rotation direction for horizontal drag (Y axis).")]
        public bool invertY = false;
        [Tooltip("Invert rotation direction for combined drag (Z axis).")]
        public bool invertZ = false;

        [Header("Inertia & Friction")]
        [Tooltip("How quickly angular velocity decays after release (larger = faster stop).")]
        public float inertiaDamping = 3f;
        [Tooltip("Minimum deg/s considered as moving; below this velocity the object is considered stopped.")]
        public float minVelocityToStop = 0.1f;

        [Header("Axis Constraints")]
        public bool enableXLimits = false;
        public Vector2 xLimit = new Vector2(-90f, 90f);
        public bool enableYLimits = false;
        public Vector2 yLimit = new Vector2(-180f, 180f);
        public bool enableZLimits = false;
        public Vector2 zLimit = new Vector2(-45f, 45f);

        [Header("Auto Return")]
        [Tooltip("If true, capture the current local rotation as the default on Start.")]
        public bool useDefaultOnStart = true;
        [Tooltip("Default local euler angles used if Use Default On Start is false.")]
        public Vector3 defaultEulerAngles;
        public bool autoReturn = true;
        public float returnDelay = 3f;
        public float returnSpeed = 1.5f;

        bool dragging;
        Vector3 lastPointer;
        Vector3 angularVelocity; // deg/s
        float idleTimer;
        Quaternion defaultLocalRotation;

        void Reset()
        {
            sensitivity = 0.2f;
            smoothing = 12f;
            inertiaDamping = 3f;
            returnDelay = 3f;
            returnSpeed = 1.5f;
        }

        void Start()
        {
            if (pivot == null) pivot = transform;
            if (raycastTarget == null) raycastTarget = transform;
            if (raycastCamera == null) raycastCamera = Camera.main;

            defaultLocalRotation = useDefaultOnStart ? pivot.localRotation : Quaternion.Euler(defaultEulerAngles);
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryBeginDrag();
            }

            if (dragging && Input.GetMouseButton(0))
            {
                HandleDrag();
            }

            if (dragging && Input.GetMouseButtonUp(0))
            {
                EndDrag();
            }

            if (!dragging)
            {
                ApplyInertiaAndReturn();
            }
        }

        bool TryBeginDrag()
        {
            if (raycastCamera == null) raycastCamera = Camera.main;

            if (requireRaycastHit)
            {
                Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastLayer))
                {
                    if (raycastTarget != null)
                    {
                        if (hit.transform != raycastTarget && !hit.transform.IsChildOf(raycastTarget))
                            return false;
                    }
                    else
                    {
                        if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                            return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            dragging = true;
            lastPointer = Input.mousePosition;
            angularVelocity = Vector3.zero;
            idleTimer = 0f;
            return true;
        }

        void HandleDrag()
        {
            Vector3 pointer = Input.mousePosition;
            Vector3 delta = pointer - lastPointer;
            lastPointer = pointer;
            float dt = Mathf.Max(Time.deltaTime, 1e-6f);

            float ix = invertX ? -1f : 1f;
            float iy = invertY ? -1f : 1f;
            float iz = invertZ ? -1f : 1f;

            Vector3 targetDegPerSec = new Vector3(ix * (-delta.y * sensitivity / dt), iy * (delta.x * sensitivity / dt), 0f);
            if (affectZ) targetDegPerSec.z = iz * ((delta.x + delta.y) * 0.5f * sensitivity / dt);

            float t = 1f - Mathf.Exp(-smoothing * dt);
            angularVelocity = Vector3.Lerp(angularVelocity, targetDegPerSec, t);

            ApplyRotation(angularVelocity * dt);
            idleTimer = 0f;
        }

        void EndDrag()
        {
            dragging = false;
        }

        void ApplyInertiaAndReturn()
        {
            float dt = Time.deltaTime;

            if (angularVelocity.sqrMagnitude > minVelocityToStop * minVelocityToStop)
            {
                ApplyRotation(angularVelocity * dt);
                angularVelocity = Vector3.Lerp(angularVelocity, Vector3.zero, inertiaDamping * dt);
                idleTimer = 0f;
            }
            else
            {
                angularVelocity = Vector3.zero;
                idleTimer += dt;
                if (autoReturn && idleTimer >= returnDelay)
                {
                    pivot.localRotation = Quaternion.Slerp(pivot.localRotation, defaultLocalRotation, returnSpeed * dt);
                    ClampLocalEulerAngles();
                }
            }
        }

        void ApplyRotation(Vector3 deg)
        {
            if (pivot == null) pivot = transform;

            if (affectX && !Mathf.Approximately(deg.x, 0f)) pivot.Rotate(pivot.right, deg.x, Space.World);
            if (affectY && !Mathf.Approximately(deg.y, 0f)) pivot.Rotate(pivot.up, deg.y, Space.World);
            if (affectZ && !Mathf.Approximately(deg.z, 0f)) pivot.Rotate(pivot.forward, deg.z, Space.World);

            ClampLocalEulerAngles();
        }

        void ClampLocalEulerAngles()
        {
            if (pivot == null) return;

            Vector3 e = pivot.localEulerAngles;
            float sx = SignedAngle(e.x);
            float sy = SignedAngle(e.y);
            float sz = SignedAngle(e.z);

            if (enableXLimits) sx = Mathf.Clamp(sx, xLimit.x, xLimit.y);
            if (enableYLimits) sy = Mathf.Clamp(sy, yLimit.x, yLimit.y);
            if (enableZLimits) sz = Mathf.Clamp(sz, zLimit.x, zLimit.y);

            pivot.localEulerAngles = new Vector3(To360(sx), To360(sy), To360(sz));
        }

        static float SignedAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        static float To360(float signed)
        {
            return signed < 0f ? signed + 360f : signed;
        }

        [ContextMenu("Reset Default Rotation To Current")]
        public void ResetDefaultToCurrent()
        {
            if (pivot == null) pivot = transform;
            defaultLocalRotation = pivot.localRotation;
        }

        public void SetDefaultLocalRotation(Quaternion q)
        {
            defaultLocalRotation = q;
        }
    }
}
