// Ответственности
// - Собирает блочную сетку и чанки участка для одной попытки.
// - Хранит блоки и их повреждения, обновляет геометрию и сообщает о разрушении.

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
        private int stoneHp, soilHp, woodHp, brickHp, pillarHp, crateHp;
        private Material material;
        private Texture2D atlas;
        private Vector2Int halfSize;
        public const int YardLevel = -1; // Feet on the yard surface, in grid cells.
        public ShopLayout Shop { get; private set; }
        public int ShopFloorLevel => Shop.FloorLevel;
        public ArenaLayout Arena { get; private set; }
        internal int Bottom { get; private set; }
        public float VoidHeight => LevelY(Bottom - 4);
        public float Size { get; private set; }
        internal Vector2Int Extent { get; private set; }
        internal event System.Action<Vector3Int> BlockRemoved;
        internal event System.Action<Vector3Int> StoneMined;
        public Vector2 SafeExtent => new Vector2(halfSize.x + .5f, halfSize.y + .5f) * Size;

        public void Build(WorldConfig config, Vector3 supplyPosition)
        {
            Routes = new WalkerRoutes(this);
            soilHp = config.soil.hp;
            stoneHp = soilHp * Mathf.Max(1, config.subsoilHpMultiplier);
            woodHp = config.fence.hp;
            brickHp = config.masonry.hp;
            pillarHp = config.arenaPillar.hp;
            crateHp = config.supplyCrate ? config.supplyCrate.hp : 0;
            Size = config.blockSize;
            int w = Mathf.Max(4, config.halfSize.x), d = Mathf.Max(4, config.halfSize.y);
            halfSize = new Vector2Int(w, d);
            int drop = Mathf.Max(1, config.plateauHeight);
            // Keep the mountain clear of the plateau even on small maps.
            int ridge = Mathf.Max(drop + 20, config.mountainDistance);
            int margin = ridge + 10;
            Arena = new ArenaLayout(w, ridge, this);
            Extent = halfSize + Vector2Int.one * margin;
            int digDepth = YardTerrain.SoftLayers + Mathf.Max(1, config.subsoilDepth);
            // Keep a roof over the four-cell room even on shallow custom maps.
            Shop = new ShopLayout(config.ShopCenter, YardLevel - Mathf.Max(digDepth, ShopLayout.Height + 1));
            Bottom = Mathf.Min(YardLevel - drop - digDepth - 1, ShopFloorLevel - 1);

            var terrain = new YardTerrain(this, halfSize, ridge, drop, digDepth);
            terrain.Populate(Cell(supplyPosition), config.supplyCrate);

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

        private static int ChunkIndex(int cell) => Mathf.FloorToInt((float)cell / YardChunk.Width);
        internal Voxel BlockAt(Vector3Int cell) => blocks.GetValueOrDefault(cell);
        internal int Top(Vector2Int column) => tops.TryGetValue(column, out int y) ? y : Bottom - 1;

        internal void Place(Voxel kind, int x, int y, int z)
        {
            if (!blocks.TryAdd(new Vector3Int(x, y, z), kind)) return;
            var column = new Vector2Int(x, z);
            if (y > Top(column)) tops[column] = y;
        }

        public Health GetOrCreateBlockHealth(Vector3Int cell)
        {
            var kind = BlockAt(cell);
            if (kind is Voxel.Air or Voxel.Bedrock or Voxel.RoseBedrock) return null;
            if (damaged.TryGetValue(cell, out var existing)) return existing;
            int hp = kind switch
            {
                Voxel.Wood or Voxel.Bark => woodHp,
                Voxel.Brick or Voxel.Metal => brickHp,
                Voxel.Pillar => pillarHp,
                Voxel.Crate => crateHp,
                Voxel.Stone or Voxel.RoseStone => stoneHp,
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
                if (kind == Voxel.Stone && health.KilledByPlayer) StoneMined?.Invoke(cell);
                Destroy(obj);
            };
            return health;
        }

        private void Remove(Vector3Int cell)
        {
            blocks.Remove(cell);
            Routes.Invalidate();
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
            BlockRemoved?.Invoke(cell);
        }

        public bool IsOutside(Vector3 position)
        {
            position -= transform.position;
            var extent = SafeExtent;
            return Mathf.Abs(position.x) > extent.x || Mathf.Abs(position.z) > extent.y;
        }

        internal Vector3 WorldFeet(Vector3Int cell) => transform.position + (Vector3)cell * Size;
        internal float LevelY(int level) => transform.position.y + level * Size;
        public Vector2Int Cell(Vector3 position)
        {
            var local = (position - transform.position) / Size;
            return new Vector2Int(Mathf.FloorToInt(local.x + .5f), Mathf.FloorToInt(local.z + .5f));
        }
        public Vector3 Center(Vector2Int cell) => new(transform.position.x + cell.x * Size, Height(cell), transform.position.z + cell.y * Size);
        public float Height(Vector2Int cell)
        {
            int y = Top(cell);
            return y < Bottom ? float.NegativeInfinity : LevelY(y + 1);
        }

        private void OnDestroy()
        {
            if (atlas) Destroy(atlas);
            if (material) Destroy(material);
        }
    }
}
