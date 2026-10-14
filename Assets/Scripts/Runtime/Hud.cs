using UnityEngine;
using UnityEngine.Rendering;

namespace FifMan
{
    /// <summary>World-space text that lazily follows the player's gaze (always drawn on top).</summary>
    public sealed class Hud : MonoBehaviour
    {
        const float Distance = 2.2f;

        Transform head;
        Transform root;
        TextMesh stats;
        TextMesh message;
        float messageTimer;
        bool persistent;

        public static Hud Create(Transform head)
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<Hud>();
            hud.head = head;
            hud.root = go.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.stats = MakeText(go.transform, font, "Stats", 0.012f, new Color(1f, 0.95f, 0.4f), new Vector3(0f, 0.6f, 0f));
            hud.message = MakeText(go.transform, font, "Message", 0.02f, Color.white, Vector3.zero);
            hud.message.text = "";
            return hud;
        }

        static TextMesh MakeText(Transform parent, Font font, string name, float size, Color color, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(font.material);
            mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            mat.renderQueue = 4000;
            mr.sharedMaterial = mat;
            return tm;
        }

        public void SetStats(string text) => stats.text = text;

        public void Show(string text, float seconds)
        {
            message.text = text;
            persistent = seconds <= 0f;
            messageTimer = seconds;
        }

        void LateUpdate()
        {
            if (head == null) return;
            if (!persistent && messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f) message.text = "";
            }

            var forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = head.forward;
            forward.Normalize();
            var target = head.position + forward * Distance;
            float t = 1f - Mathf.Exp(-4f * Time.deltaTime);
            root.position = Vector3.Lerp(root.position, target, t);
            var away = root.position - head.position;
            if (away.sqrMagnitude > 0.0001f)
                root.rotation = Quaternion.Slerp(root.rotation, Quaternion.LookRotation(away), t);
        }
    }
}
