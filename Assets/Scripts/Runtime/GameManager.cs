using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FifMan
{
    /// <summary>Builds the maze at runtime and runs the game rules.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        enum State { Playing, Won, Lost }

        sealed class Pellet
        {
            public GameObject Object;
            public bool Power;
        }

        const int StartLives = 3;
        const float InvulnerableSeconds = 3f;
        const float PowerSeconds = 8f;
        const float HeadPickupRadius = 0.9f;
        const float HandPickupRadius = 0.45f;
        const float GhostTouchRadius = 0.8f;

        static readonly Color[] GhostColors =
        {
            new Color(1f, 0.15f, 0.15f), new Color(1f, 0.55f, 0.8f),
            new Color(0.2f, 0.9f, 0.95f), new Color(1f, 0.6f, 0.1f),
        };

        VrRig rig;
        Hud hud;
        Sfx sfx;
        Maze maze;
        Transform worldRoot;
        Material baseMaterial;
        Mesh cubeMesh;
        InputAction confirm;

        readonly Dictionary<Vector2Int, Pellet> pellets = new Dictionary<Vector2Int, Pellet>();
        readonly List<Ghost> ghosts = new List<Ghost>();

        State state;
        int score, lives, level = 1, ghostCombo;
        float powerTimer, invulnerableTimer;

        public void Begin()
        {
            Application.targetFrameRate = -1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.antiAliasing = 4;

            // Borrow the engine's default lit material (and cube mesh) from a primitive so no shader lookup is needed.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
            cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);

            rig = VrRig.Create(baseMaterial);
            hud = Hud.Create(rig.Head);
            sfx = Sfx.Create(rig.gameObject);

            confirm = new InputAction("confirm", InputActionType.Button);
            confirm.AddBinding("<XRController>{RightHand}/triggerPressed");
            confirm.AddBinding("<XRController>{RightHand}/primaryButton");
            confirm.AddBinding("<XRController>{LeftHand}/triggerPressed");
            confirm.AddBinding("<Keyboard>/space");
            confirm.Enable();

            NewGame();
        }

        void NewGame()
        {
            level = 1;
            score = 0;
            lives = StartLives;
            Build();
        }

        void NextLevel()
        {
            level++;
            Build();
        }

        // ---------------------------------------------------------------- world building

        Material MakeMaterial(Color color, float smoothness = 0.2f)
        {
            var m = new Material(baseMaterial) { color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        void Build()
        {
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("World").transform;
            pellets.Clear();
            ghosts.Clear();
            maze = new Maze();
            rig.SetMaze(maze);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.06f);
            RenderSettings.fogStartDistance = 10f;
            RenderSettings.fogEndDistance = 32f;

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(worldRoot, false);
            lightGo.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;

            // Walls (one merged mesh) and floor.
            var walls = new GameObject("Walls");
            walls.transform.SetParent(worldRoot, false);
            walls.AddComponent<MeshFilter>().sharedMesh = maze.BuildWallMesh(cubeMesh);
            walls.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(new Color(0.12f, 0.2f, 0.85f), 0.45f);

            float floorW = maze.Width * Maze.CellSize, floorH = maze.Height * Maze.CellSize;
            var floor = Prim(PrimitiveType.Cube, worldRoot, maze.Center + Vector3.down * 0.05f,
                new Vector3(floorW, 0.1f, floorH), MakeMaterial(new Color(0.05f, 0.05f, 0.1f)));
            floor.name = "Floor";

            // Pellets sit at waist height so they can be grabbed with a hand.
            var pelletMat = MakeMaterial(new Color(1f, 0.92f, 0.45f), 0.6f);
            var powerMat = MakeMaterial(new Color(1f, 0.6f, 0.9f), 0.6f);
            foreach (var cell in maze.Pellets) AddPellet(cell, false, pelletMat);
            foreach (var cell in maze.PowerPellets) AddPellet(cell, true, powerMat);

            // Ghosts.
            for (int i = 0; i < maze.GhostSpawns.Count; i++) AddGhost(i);

            // Player.
            rig.PlaceHeadAt(maze.CellToWorld(maze.PlayerSpawn));
            rig.transform.rotation = Quaternion.identity;
            // Face the long open corridor: along +X from the centre.
            rig.transform.RotateAround(rig.Head.position, Vector3.up, 90f);

            state = State.Playing;
            powerTimer = 0f;
            ghostCombo = 0;
            invulnerableTimer = InvulnerableSeconds;
            hud.Show($"LEVEL {level}\nEat every pellet - grab them with your hands!", 4f);
        }

        void AddPellet(Vector2Int cell, bool power, Material mat)
        {
            float size = power ? 0.35f : 0.16f;
            var pos = maze.CellToWorld(cell) + Vector3.up * 1.05f;
            var go = Prim(PrimitiveType.Sphere, worldRoot, pos, Vector3.one * size, mat);
            go.name = power ? "Power Pellet" : "Pellet";
            pellets[cell] = new Pellet { Object = go, Power = power };
        }

        void AddGhost(int index)
        {
            var spawn = maze.GhostSpawns[index];
            var color = GhostColors[index % GhostColors.Length];
            var root = new GameObject($"Ghost {index}");
            root.transform.SetParent(worldRoot, false);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var bodyMat = MakeMaterial(color, 0.4f);
            Prim(PrimitiveType.Capsule, visual, new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 0.6f, 0.8f), bodyMat);
            var white = MakeMaterial(Color.white);
            var black = MakeMaterial(Color.black);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, visual, new Vector3(0.17f * side, 0.95f, 0.3f), Vector3.one * 0.2f, white);
                Prim(PrimitiveType.Sphere, visual, new Vector3(0.17f * side, 0.95f, 0.4f), Vector3.one * 0.1f, black);
            }

            var ghost = root.AddComponent<Ghost>();
            ghost.Speed = Mathf.Min(2.2f + 0.25f * level, 4f);
            ghost.Init(maze, spawn, visual, bodyMat, color, () => rig.Head.position);
            ghost.Respawn(2f + index * 2.5f);
            ghosts.Add(ghost);
        }

        // ---------------------------------------------------------------- game loop

        void Update()
        {
            hud.SetStats($"SCORE {score}    LIVES {lives}    LEFT {pellets.Count}");

            if (state != State.Playing)
            {
                if (confirm.WasPressedThisFrame())
                {
                    if (state == State.Won) NextLevel(); else NewGame();
                }
                return;
            }

            float dt = Time.deltaTime;
            PickUp(rig.Head.position, HeadPickupRadius, true, -1);
            PickUp(rig.LeftHand.position, HandPickupRadius, false, 0);
            PickUp(rig.RightHand.position, HandPickupRadius, false, 1);
            AnimatePowerPellets();

            if (invulnerableTimer > 0f) invulnerableTimer -= dt;

            if (powerTimer > 0f)
            {
                powerTimer -= dt;
                bool flash = powerTimer < 2f;
                foreach (var g in ghosts) if (g.Frightened) g.SetFrightened(true, flash);
                if (powerTimer <= 0f) foreach (var g in ghosts) g.SetFrightened(false);
            }

            if (pellets.Count == 0)
            {
                state = State.Won;
                sfx.Play(sfx.Win);
                hud.Show($"LEVEL {level} CLEARED!\nScore {score}\nPull a trigger for the next level", 0f);
                return;
            }

            CheckGhostContact();
        }

        void PickUp(Vector3 p, float radius, bool flat, int hand)
        {
            var center = maze.WorldToCell(p);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var key = new Vector2Int(center.x + dx, center.y + dz);
                    if (!pellets.TryGetValue(key, out var pellet)) continue;
                    var pos = pellet.Object.transform.position;
                    if (flat) { p.y = pos.y; }
                    if ((pos - p).sqrMagnitude > radius * radius) continue;
                    Collect(key, pellet, hand);
                }
            }
        }

        void Collect(Vector2Int key, Pellet pellet, int hand)
        {
            Destroy(pellet.Object);
            pellets.Remove(key);
            if (pellet.Power)
            {
                score += 50;
                powerTimer = PowerSeconds;
                ghostCombo = 0;
                foreach (var g in ghosts) g.SetFrightened(true);
                sfx.Play(sfx.Power);
                rig.Haptic(hand, 0.6f, 0.2f);
            }
            else
            {
                score += 10;
                sfx.Play(sfx.Pellet, 0.35f);
                rig.Haptic(hand, 0.25f, 0.04f);
            }
        }

        void AnimatePowerPellets()
        {
            float s = 0.35f + 0.06f * Mathf.Sin(Time.time * 6f);
            foreach (var pellet in pellets.Values)
                if (pellet.Power) pellet.Object.transform.localScale = Vector3.one * s;
        }

        void CheckGhostContact()
        {
            var head = rig.Head.position;
            head.y = 0f;
            foreach (var g in ghosts)
            {
                if (!g.Released) continue;
                var gp = g.transform.position;
                gp.y = 0f;
                if ((gp - head).sqrMagnitude > GhostTouchRadius * GhostTouchRadius) continue;

                if (g.Frightened)
                {
                    ghostCombo++;
                    score += 100 * (1 << Mathf.Min(ghostCombo, 4));
                    g.Respawn(4f);
                    sfx.Play(sfx.EatGhost);
                    rig.Haptic(0, 0.8f, 0.15f);
                    rig.Haptic(1, 0.8f, 0.15f);
                }
                else if (invulnerableTimer <= 0f)
                {
                    LoseLife();
                    return;
                }
            }
        }

        void LoseLife()
        {
            lives--;
            rig.Haptic(0, 1f, 0.4f);
            rig.Haptic(1, 1f, 0.4f);
            if (lives <= 0)
            {
                state = State.Lost;
                sfx.Play(sfx.Lose);
                hud.Show($"GAME OVER\nScore {score}\nPull a trigger to try again", 0f);
                return;
            }

            sfx.Play(sfx.Hit);
            hud.Show("OUCH!", 1.5f);
            invulnerableTimer = InvulnerableSeconds;
            rig.PlaceHeadAt(maze.CellToWorld(maze.PlayerSpawn));
            for (int i = 0; i < ghosts.Count; i++) ghosts[i].Respawn(2f + i * 1.5f);
        }
    }
}
