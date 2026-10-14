using System.Collections.Generic;
using UnityEngine;

namespace FifMan
{
    /// <summary>
    /// Grid maze. Cell (c, r) has its centre at world (c * CellSize, 0, r * CellSize).
    /// Grid directions map directly onto world axes: (dx, dy) -> (x, z).
    /// </summary>
    public sealed class Maze
    {
        public const float CellSize = 2f;
        public const float WallHeight = 2.8f;

        // # wall   . pellet   o power pellet   G ghost spawn   P player start
        static readonly string[] Layout =
        {
            "###############",
            "#o.....#.....o#",
            "#.###.###.###.#",
            "#......G......#",
            "#.###.#.#.###.#",
            "#.....#G#.....#",
            "#.###.#.#.###.#",
            "#......P......#",
            "#.###.#.#.###.#",
            "#.....#G#.....#",
            "#.###.#.#.###.#",
            "#......G......#",
            "#.###.###.###.#",
            "#o.....#.....o#",
            "###############",
        };

        public int Width { get; }
        public int Height { get; }
        public Vector2Int PlayerSpawn { get; private set; }
        public List<Vector2Int> GhostSpawns { get; } = new List<Vector2Int>();
        public List<Vector2Int> Pellets { get; } = new List<Vector2Int>();
        public List<Vector2Int> PowerPellets { get; } = new List<Vector2Int>();

        readonly bool[,] walls;

        public Maze()
        {
            Height = Layout.Length;
            Width = Layout[0].Length;
            walls = new bool[Width, Height];
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    var cell = new Vector2Int(c, r);
                    switch (Layout[r][c])
                    {
                        case '#': walls[c, r] = true; break;
                        case '.': Pellets.Add(cell); break;
                        case 'o': PowerPellets.Add(cell); break;
                        case 'G': GhostSpawns.Add(cell); break;
                        case 'P': PlayerSpawn = cell; break;
                    }
                }
            }
        }

        public bool IsWall(int c, int r)
        {
            if (c < 0 || r < 0 || c >= Width || r >= Height) return true;
            return walls[c, r];
        }

        public bool IsWall(Vector2Int cell) => IsWall(cell.x, cell.y);

        public Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x * CellSize, 0f, cell.y * CellSize);

        public Vector2Int WorldToCell(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / CellSize + 0.5f), Mathf.FloorToInt(p.z / CellSize + 0.5f));

        public Vector3 Center => new Vector3((Width - 1) * 0.5f * CellSize, 0f, (Height - 1) * 0.5f * CellSize);

        /// <summary>True if a circle of the given radius at p (XZ plane) overlaps any wall.</summary>
        public bool Collides(Vector3 p, float radius)
        {
            var min = WorldToCell(new Vector3(p.x - radius, 0f, p.z - radius));
            var max = WorldToCell(new Vector3(p.x + radius, 0f, p.z + radius));
            float half = CellSize * 0.5f;
            for (int c = min.x; c <= max.x; c++)
            {
                for (int r = min.y; r <= max.y; r++)
                {
                    if (!IsWall(c, r)) continue;
                    float cx = Mathf.Clamp(p.x, c * CellSize - half, c * CellSize + half);
                    float cz = Mathf.Clamp(p.z, r * CellSize - half, r * CellSize + half);
                    float dx = p.x - cx, dz = p.z - cz;
                    if (dx * dx + dz * dz < radius * radius) return true;
                }
            }
            return false;
        }

        /// <summary>Builds one merged mesh containing every wall block.</summary>
        public Mesh BuildWallMesh(Mesh cube)
        {
            var combine = new List<CombineInstance>();
            for (int c = 0; c < Width; c++)
            {
                for (int r = 0; r < Height; r++)
                {
                    if (!walls[c, r]) continue;
                    var center = new Vector3(c * CellSize, WallHeight * 0.5f, r * CellSize);
                    combine.Add(new CombineInstance
                    {
                        mesh = cube,
                        transform = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(CellSize, WallHeight, CellSize)),
                    });
                }
            }
            var mesh = new Mesh { name = "Maze Walls" };
            mesh.CombineMeshes(combine.ToArray(), true, true);
            return mesh;
        }
    }
}
