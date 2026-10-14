using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FifMan
{
    /// <summary>
    /// Player rig: tracked head + hands, smooth locomotion (left stick), snap turn (right stick).
    /// Falls back to WASD + Q/E when no headset is present so the game can be tested in the editor.
    /// </summary>
    public sealed class VrRig : MonoBehaviour
    {
        const float MoveSpeed = 2.6f;
        const float BodyRadius = 0.3f;
        const float SnapAngle = 45f;
        const float StandingHeight = 1.7f;

        public Transform Head { get; private set; }
        public Transform LeftHand { get; private set; }
        public Transform RightHand { get; private set; }

        Transform cameraOffset;
        Maze maze;
        InputAction move;
        InputAction turn;
        bool snapReady = true;
        float originTimer;
        int originAttempts;

        public static VrRig Create(Material baseMaterial)
        {
            var go = new GameObject("VR Rig");
            var rig = go.AddComponent<VrRig>();
            rig.Build(baseMaterial);
            return rig;
        }

        void Build(Material baseMaterial)
        {
            cameraOffset = new GameObject("Camera Offset").transform;
            cameraOffset.SetParent(transform, false);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.SetParent(cameraOffset, false);
            var camera = cam.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
            cam.AddComponent<AudioListener>();
            PoseFollower.Attach(cam, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation",
                "<XRHMD>/devicePosition", "<XRHMD>/deviceRotation");
            Head = cam.transform;

            LeftHand = MakeHand("Left Hand", "LeftHand", new Color(0.2f, 0.9f, 1f), baseMaterial);
            RightHand = MakeHand("Right Hand", "RightHand", new Color(1f, 0.5f, 0.9f), baseMaterial);

            move = new InputAction("move", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
            turn = new InputAction("turn", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
            move.Enable();
            turn.Enable();

            ConfigureOrigin();
        }

        Transform MakeHand(string handName, string usage, Color color, Material baseMaterial)
        {
            var hand = new GameObject(handName).transform;
            hand.SetParent(cameraOffset, false);
            PoseFollower.Attach(hand.gameObject,
                $"<XRController>{{{usage}}}/devicePosition", $"<XRController>{{{usage}}}/deviceRotation",
                $"<XRController>{{{usage}}}/pointerPosition", $"<XRController>{{{usage}}}/pointerRotation");

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(hand, false);
            orb.transform.localScale = Vector3.one * 0.09f;
            orb.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            var mat = new Material(baseMaterial) { color = color };
            orb.GetComponent<Renderer>().sharedMaterial = mat;
            return hand;
        }

        public void SetMaze(Maze m) => maze = m;

        /// <summary>Shifts the rig so the player's head is above the given world position.</summary>
        public void PlaceHeadAt(Vector3 world)
        {
            var delta = world - Head.position;
            delta.y = 0f;
            transform.position += delta;
        }

        public void Haptic(int hand, float amplitude, float duration)
        {
            if (hand < 0) return;
            var device = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }

        void ConfigureOrigin()
        {
            bool floor = false;
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)
                    || (s.GetTrackingOriginMode() & TrackingOriginModeFlags.Floor) != 0)
                    floor = true;
            }
            bool headset = XRSettings.isDeviceActive;
            cameraOffset.localPosition = (floor && headset) ? Vector3.zero : Vector3.up * StandingHeight;
        }

        void Update()
        {
            // XR may finish initialising a moment after the first frame; re-check a few times.
            if (originAttempts < 4)
            {
                originTimer += Time.deltaTime;
                if (originTimer > 0.75f) { originTimer = 0f; originAttempts++; ConfigureOrigin(); }
            }

            var kb = Keyboard.current;
            Vector2 m = move.ReadValue<Vector2>();
            float turnInput = turn.ReadValue<Vector2>().x;
            if (kb != null)
            {
                if (kb.wKey.isPressed) m.y += 1f;
                if (kb.sKey.isPressed) m.y -= 1f;
                if (kb.dKey.isPressed) m.x += 1f;
                if (kb.aKey.isPressed) m.x -= 1f;
                if (kb.eKey.isPressed) turnInput += 1f;
                if (kb.qKey.isPressed) turnInput -= 1f;
            }

            HandleTurn(turnInput);
            HandleMove(m);
        }

        void HandleTurn(float x)
        {
            if (Mathf.Abs(x) < 0.3f) { snapReady = true; return; }
            if (!snapReady || Mathf.Abs(x) < 0.7f) return;
            snapReady = false;
            transform.RotateAround(Head.position, Vector3.up, Mathf.Sign(x) * SnapAngle);
        }

        void HandleMove(Vector2 input)
        {
            if (maze == null) return;
            if (input.magnitude < 0.15f) return;
            if (input.sqrMagnitude > 1f) input.Normalize();

            var forward = Head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Head.up;
            forward.y = 0f;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);

            var delta = (forward * input.y + right * input.x) * (MoveSpeed * Time.deltaTime);
            var pos = Head.position;
            pos.y = 0f;

            // Already overlapping a wall (e.g. the player physically walked into it): let them back out.
            if (maze.Collides(pos, BodyRadius))
            {
                transform.position += delta;
                return;
            }

            var dx = new Vector3(delta.x, 0f, 0f);
            if (!maze.Collides(pos + dx, BodyRadius))
            {
                transform.position += dx;
                pos += dx;
            }
            var dz = new Vector3(0f, 0f, delta.z);
            if (!maze.Collides(pos + dz, BodyRadius)) transform.position += dz;
        }
    }
}
