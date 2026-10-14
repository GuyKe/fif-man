using UnityEngine;

namespace FifMan
{
    /// <summary>Spawns the whole game when any scene loads, so the scene itself can be empty.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;

            // The game provides its own XR camera; silence any default one in the scene.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                cam.gameObject.SetActive(false);

            var go = new GameObject("Fif-Man");
            go.AddComponent<GameManager>().Begin();
        }
    }
}
