using UnityEngine;
using UnityEngine.InputSystem;

namespace FifMan
{
    /// <summary>Copies an XR device pose (via Input System actions) onto this transform's local pose.</summary>
    public sealed class PoseFollower : MonoBehaviour
    {
        InputAction position;
        InputAction rotation;

        public static PoseFollower Attach(GameObject go, string positionPath, string rotationPath, string positionAlt = null, string rotationAlt = null)
        {
            var f = go.AddComponent<PoseFollower>();
            f.position = new InputAction("position", InputActionType.Value, positionPath, null, null, "Vector3");
            f.rotation = new InputAction("rotation", InputActionType.Value, rotationPath, null, null, "Quaternion");
            if (positionAlt != null) f.position.AddBinding(positionAlt);
            if (rotationAlt != null) f.rotation.AddBinding(rotationAlt);
            f.position.Enable();
            f.rotation.Enable();
            return f;
        }

        void OnEnable() => Application.onBeforeRender += Apply;
        void OnDisable() => Application.onBeforeRender -= Apply;

        void OnDestroy()
        {
            position?.Dispose();
            rotation?.Dispose();
        }

        void Update() => Apply();

        void Apply()
        {
            if (position == null) return;
            if (position.controls.Count > 0) transform.localPosition = position.ReadValue<Vector3>();
            if (rotation.controls.Count > 0)
            {
                var q = rotation.ReadValue<Quaternion>();
                if (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f) transform.localRotation = q;
            }
        }
    }
}
