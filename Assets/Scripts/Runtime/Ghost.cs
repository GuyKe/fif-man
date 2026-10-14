using System;
using UnityEngine;

namespace FifMan
{
    /// <summary>Grid-walking ghost: chases the player, flees while frightened.</summary>
    public sealed class Ghost : MonoBehaviour
    {
        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        static readonly Color FrightenedColor = new Color(0.55f, 0.7f, 1f);

        Maze maze;
        Func<Vector3> playerPosition;
        Transform visual;
        Material bodyMaterial;
        Color baseColor;
        Vector2Int spawn;
        Vector2Int cell;
        Vector2Int destCell;
        Vector2Int dir;
        float releaseTimer;
        bool frightened;
        bool flashing;
        float bobSeed;

        public float Speed = 2.4f;
        public bool Released => releaseTimer <= 0f;
        public bool Frightened => frightened;

        public void Init(Maze m, Vector2Int spawnCell, Transform visualRoot, Material body, Color color, Func<Vector3> player)
        {
            maze = m;
            spawn = spawnCell;
            visual = visualRoot;
            bodyMaterial = body;
            baseColor = color;
            playerPosition = player;
            bobSeed = UnityEngine.Random.value * 10f;
        }

        public void Respawn(float delay)
        {
            cell = spawn;
            destCell = spawn;
            dir = Vector2Int.zero;
            releaseTimer = delay;
            frightened = false;
            flashing = false;
            transform.position = maze.CellToWorld(spawn);
        }

        public void SetFrightened(bool value, bool flash = false)
        {
            frightened = value;
            flashing = value && flash;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            visual.localPosition = new Vector3(0f, 0.05f + 0.08f * Mathf.Sin(Time.time * 6f + bobSeed), 0f);
            bodyMaterial.color = frightened
                ? (flashing && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? Color.white : FrightenedColor)
                : baseColor;

            if (releaseTimer > 0f)
            {
                releaseTimer -= dt;
                return;
            }

            float step = (frightened ? Speed * 0.6f : Speed) * dt;
            var p = transform.position;
            p.y = 0f;
            for (int guard = 0; step > 0f && guard < 4; guard++)
            {
                var dest = maze.CellToWorld(destCell);
                float d = Vector3.Distance(p, dest);
                if (d <= step)
                {
                    p = dest;
                    step -= d;
                    cell = destCell;
                    if (!ChooseNext()) break;
                }
                else
                {
                    p = Vector3.MoveTowards(p, dest, step);
                    step = 0f;
                }
            }
            transform.position = p;

            if (dir != Vector2Int.zero)
            {
                var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                visual.rotation = Quaternion.Slerp(visual.rotation, look, 1f - Mathf.Exp(-12f * dt));
            }
        }

        bool ChooseNext()
        {
            Span<Vector2Int> options = stackalloc Vector2Int[4];
            int count = 0;
            for (int i = 0; i < Dirs.Length; i++)
            {
                if (maze.IsWall(cell + Dirs[i])) continue;
                options[count++] = Dirs[i];
            }
            if (count == 0) { dir = Vector2Int.zero; return false; }

            // Don't reverse unless it's a dead end.
            if (count > 1 && dir != Vector2Int.zero)
            {
                for (int i = 0; i < count; i++)
                {
                    if (options[i] == -dir)
                    {
                        options[i] = options[count - 1];
                        count--;
                        break;
                    }
                }
            }

            Vector2Int pick;
            if (UnityEngine.Random.value < (frightened ? 0.35f : 0.22f))
            {
                pick = options[UnityEngine.Random.Range(0, count)];
            }
            else
            {
                var target = playerPosition();
                pick = options[0];
                float best = frightened ? float.MinValue : float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var next = maze.CellToWorld(cell + options[i]);
                    float dist = (next - target).sqrMagnitude;
                    if (frightened ? dist > best : dist < best)
                    {
                        best = dist;
                        pick = options[i];
                    }
                }
            }

            dir = pick;
            destCell = cell + dir;
            return true;
        }
    }
}
