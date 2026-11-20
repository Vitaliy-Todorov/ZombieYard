using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    public sealed class YardGrid : MonoBehaviour
    {
        private static readonly Vector2Int[] Neighbors = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly Dictionary<Vector3Int, Voxel> blocks = new();
        private readonly Dictionary<Vector3Int, Health> damaged = new();
        private readonly Dictionary<Vector2Int, int> tops = new();
        private readonly Dictionary<Vector2Int, YardChunk> chunks = new();
        internal WalkerRoutes Routes { get; private set; }
        private int stoneHp, soilHp, woodHp, brickHp, crateHp;
        private Material material;
        private Texture2D atlas;
        private Vector2Int halfSize;
        public const int YardLevel = -1; // Feet on the yard surface, in grid cells.
        internal int Bottom { get; private set; }
        public float VoidHeight => (Bottom - 4) * Size;
        public float Size { get; private set; }
        internal Vector2Int Extent { get; private set; }
        internal event System.Action<Vector3Int> BlockRemoved;
        public Vector2 SafeExtent => new Vector2(halfSize.x + .5f, halfSize.y + .5f) * Size;

        public void Build(WorldConfig config, Vector3 supplyPosition)
        {
            Routes = new WalkerRoutes(this);
            soilHp = config.soil.hp;
            stoneHp = soilHp * Mathf.Max(1, config.subsoilHpMultiplier);
            woodHp = config.fence.hp;
            brickHp = config.masonry.hp;
            crateHp = config.supplyCrate ? config.supplyCrate.hp : 0;
            Size = config.blockSize;
            int w = Mathf.Max(4, config.halfSize.x), d = Mathf.Max(4, config.halfSize.y);
            halfSize = new Vector2Int(w, d);
            int drop = Mathf.Max(1, config.plateauHeight);
            int ridge = Mathf.Max(drop + 10, config.mountainDistance);
            int margin = ridge + 10;
            Extent = halfSize + Vector2Int.one * margin;
            const int softLayers = 2;
            int digDepth = softLayers + Mathf.Max(1, config.subsoilDepth);
            Bottom = YardLevel - drop - digDepth - 1;

            var trees = new List<Vector3Int>();
            for (int x = -w - margin; x <= w + margin; x++)
            for (int z = -d - margin; z <= d + margin; z++)
            {
                int outside = Mathf.Max(Mathf.Abs(x) - w, Mathf.Abs(z) - d);
                int depth = Mathf.Clamp(outside - 1, 0, drop);
                float distance = new Vector2(Mathf.Max(0, Mathf.Abs(x) - w), Mathf.Max(0, Mathf.Abs(z) - d)).magnitude;
                float angle = Mathf.Atan2(z, x);
                float ridgeOffset = 5 * Mathf.Sin(3 * angle) + 2 * Mathf.Sin(5 * angle);
                float slope = Mathf.Clamp01(1 - Mathf.Abs(distance - ridge - ridgeOffset) / 8);
                float peaks = Mathf.PerlinNoise((x + 35) * .085f, (z + 79) * .085f);
                int rise = Mathf.RoundToInt(slope * (8 + 15 * peaks));
                int plain = YardLevel - depth - 1;
                int surface = plain + rise;
                bool beyondRidge = distance > ridge + ridgeOffset;
                bool outerWall = outside == margin;
                if (outerWall) surface += 4;
                for (int y = Bottom; y <= surface; y++)
                {
                    Voxel kind;
                    if (beyondRidge || outerWall || y <= plain - digDepth) kind = Voxel.Bedrock;
                    else if (y == surface) kind = Voxel.Turf;
                    else if (y > surface - softLayers) kind = Voxel.Earth;
                    else kind = Voxel.Stone;
                    Place(kind, x, y, z);
                }
                if (outside == 0) Place(Voxel.Wood, x, YardLevel, z);
                if (outside < margin - 3 && outside > drop + 4 && x % 5 == 0 && z % 5 == 0)
                {
                    float cover = Mathf.PerlinNoise((x + 130) * .18f, (z + 270) * .18f);
                    if (cover > .43f) trees.Add(new Vector3Int(x, surface + 1, z));
                }
            }
            // Generate crowns after terrain so foliage never replaces the side of a mountain.
            foreach (var root in trees)
                Tree(root.x, root.y, root.z, (root.x + root.z) % 3 == 0 ? 5 : 4);

            for (int x = -w + 2; x <= -w + 5; x++)
            for (int y = 0; y < 3; y++)
                Place(Voxel.Brick, x, YardLevel + y, d - 3);
            for (int z = d - 5; z < d - 3; z++)
            for (int y = 0; y < 2; y++)
                Place(Voxel.Brick, -w + 2, YardLevel + y, z);
            for (int x = w - 5; x <= w - 3; x++) Place(Voxel.Brick, x, YardLevel, 1);
            if (config.supplyCrate)
            {
                var cell = Cell(supplyPosition);
                Place(Voxel.Crate, cell.x, YardLevel, cell.y);
            }

            // The old water tower is a landmark on the far side of the northern slope.
            int towerZ = d + drop + 6;
            for (int x = 5; x <= 7; x++)
            for (int z = towerZ - 1; z <= towerZ + 1; z++)
            {
                if (x != 6 && z != towerZ)
                    for (int y = YardLevel - drop - 1; y < 1; y++) Place(Voxel.Wood, x, y, z);
                for (int y = 1; y <= 2; y++) Place(Voxel.Metal, x, y, z);
            }

            atlas = YardAtlas.Create(config);
            material = new Material(config.terrainMaterial) { name = "Yard voxel atlas" };
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            for (int x = ChunkIndex(-w - margin - 1); x <= ChunkIndex(w + margin + 1); x++)
            for (int z = ChunkIndex(-d - margin - 1); z <= ChunkIndex(d + margin + 1); z++)
            {
                var key = new Vector2Int(x, z);
                var obj = new GameObject($"Chunk {x}, {z}");
                obj.transform.SetParent(transform, false);
                var chunk = obj.AddComponent<YardChunk>();
                chunks.Add(key, chunk);
                chunk.Initialize(this, key * YardChunk.Width, material);
            }
        }

        private void Tree(int x, int baseY, int z, int height)
        {
            for (int y = 0; y < height; y++) Place(Voxel.Bark, x, baseY + y, z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                Place(Voxel.Leaves, x + dx, baseY + height - 1, z + dz);
                if (Mathf.Abs(dx) + Mathf.Abs(dz) < 2) Place(Voxel.Leaves, x + dx, baseY + height, z + dz);
            }
        }

        private static int ChunkIndex(int cell) => Mathf.FloorToInt((float)cell / YardChunk.Width);
        internal Voxel BlockAt(Vector3Int cell) => blocks.GetValueOrDefault(cell);
        internal int Top(Vector2Int column) => tops.TryGetValue(column, out int y) ? y : Bottom - 1;

        private void Place(Voxel kind, int x, int y, int z)
        {
            if (!blocks.TryAdd(new Vector3Int(x, y, z), kind)) return;
            var column = new Vector2Int(x, z);
            if (y > Top(column)) tops[column] = y;
        }

        public Health HealthAt(Vector3Int cell)
        {
            var kind = BlockAt(cell);
            if (kind == Voxel.Air || kind == Voxel.Bedrock) return null;
            if (damaged.TryGetValue(cell, out var existing)) return existing;
            int hp = kind switch
            {
                Voxel.Wood or Voxel.Bark => woodHp,
                Voxel.Brick or Voxel.Metal => brickHp,
                Voxel.Crate => crateHp,
                Voxel.Stone => stoneHp,
                _ => soilHp
            };
            var obj = new GameObject($"Damaged block {cell}");
            obj.transform.SetParent(transform, false);
            var health = obj.AddComponent<Health>();
            health.Initialize(hp);
            damaged.Add(cell, health);
            health.Died += () =>
            {
                damaged.Remove(cell);
                Remove(cell);
                Destroy(obj);
            };
            return health;
        }

        private void Remove(Vector3Int cell)
        {
            blocks.Remove(cell);
            Routes.Invalidate();
            BlockRemoved?.Invoke(cell);
            var column = new Vector2Int(cell.x, cell.z);
            if (Top(column) == cell.y)
            {
                int y = cell.y - 1;
                while (y >= Bottom && BlockAt(new Vector3Int(cell.x, y, cell.z)) == Voxel.Air) y--;
                tops[column] = y;
            }
            var owner = new Vector2Int(ChunkIndex(cell.x), ChunkIndex(cell.z));
            chunks[owner].Rebuild();
            foreach (var offset in Neighbors)
            {
                var other = column + offset;
                var key = new Vector2Int(ChunkIndex(other.x), ChunkIndex(other.y));
                if (key != owner && chunks.TryGetValue(key, out var adjacent)) adjacent.Rebuild();
            }
        }

        public bool IsOutside(Vector3 position)
        {
            var extent = SafeExtent;
            return Mathf.Abs(position.x) > extent.x || Mathf.Abs(position.z) > extent.y;
        }

        public Vector2Int Cell(Vector3 position) => new(Mathf.FloorToInt(position.x / Size + .5f), Mathf.FloorToInt(position.z / Size + .5f));
        public Vector3 Center(Vector2Int cell) => new(cell.x * Size, Height(cell), cell.y * Size);
        public float Height(Vector2Int cell)
        {
            int y = Top(cell);
            return y < Bottom ? float.NegativeInfinity : (y + 1) * Size;
        }

        private void OnDestroy()
        {
            if (atlas) Destroy(atlas);
            if (material) Destroy(material);
        }

        // Project feet onto the first supporting block below them, including under a roof.
        internal bool TryGround(Vector3 feet, out Vector3Int stand)
        {
            var column = Cell(feet);
            int top = Top(column);
            int y = Mathf.Min(top, Mathf.FloorToInt((feet.y + .04f) / Size) - 1);
            stand = new Vector3Int(column.x, y + 1, column.y);
            while (y >= Bottom)
            {
                if (y == top || BlockAt(new Vector3Int(column.x, y, column.y)) != Voxel.Air)
                {
                    stand.y = y + 1;
                    return true;
                }
                y--;
            }
            return false;
        }
    }
}
