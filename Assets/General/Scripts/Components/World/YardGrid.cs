using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    public sealed class YardGrid : MonoBehaviour
    {
        private static readonly Vector2Int[] Neighbors = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly HashSet<Vector3Int> blocks = new();
        private readonly Dictionary<Vector2Int, Vector2Int> next = new();
        private readonly Queue<Vector2Int> frontier = new();
        private Vector2Int lastTarget;
        private bool dirty = true;
        private MaterialPropertyBlock tint;
        private float lastClimb;
        private Vector2Int groundExtent;
        public float Size { get; private set; }

        public void Build(WorldConfig config, Vector3 supplyPosition)
        {
            tint = new MaterialPropertyBlock();
            Size = config.blockSize;
            int w = Mathf.Max(4, config.halfSize.x), d = Mathf.Max(4, config.halfSize.y);
            groundExtent = new Vector2Int(w + 4, d + 4);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Packed subsoil";
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(0, -1.5f * Size, 0);
            floor.transform.localScale = new Vector3(2 * groundExtent.x + 1, 1, 2 * groundExtent.y + 1) * Size;
            floor.GetComponent<MeshRenderer>().sharedMaterial = config.pitFloorMaterial;
            for (int x = -w - 4; x <= w + 4; x++)
            for (int z = -d - 4; z <= d + 4; z++)
            {
                Place(config.soil, x, -1, z, 0);
                bool side = Mathf.Abs(x) == w && Mathf.Abs(z) <= d;
                bool end = Mathf.Abs(z) == d && Mathf.Abs(x) <= w;
                if (side || end) Place(config.fence, x, 0, z, side ? 90 : 0);
            }

            // L-shaped ruined workshop and a low wall leave several approaches.
            for (int x = -w + 2; x <= -w + 5; x++)
            for (int y = 0; y < 3; y++)
                Place(config.masonry, x, y, d - 3, 0);
            for (int z = d - 5; z < d - 3; z++)
            for (int y = 0; y < 2; y++)
                Place(config.masonry, -w + 2, y, z, 90);
            for (int x = w - 5; x <= w - 3; x++)
                Place(config.masonry, x, 0, 1, 0);
            if (config.supplyCrate)
            {
                var supplyCell = Cell(supplyPosition);
                Place(config.supplyCrate, supplyCell.x, 0, supplyCell.y, 0);
            }
        }

        private void Place(BlockConfig config, int x, int y, int z, int angle)
        {
            var cell = new Vector3Int(x, y, z);
            if (!blocks.Add(cell)) return;
            Vector3 pos = new Vector3(x, y + .5f, z) * Size;
            var block = Instantiate(config.prefab, pos, Quaternion.Euler(0, angle, 0), transform);
            block.transform.localScale = Vector3.one * Size;
            if (y == -1)
            {
                var turf = block.transform.Find("Turf");
                if (turf && turf.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    float patch = Mathf.PerlinNoise(x * .24f + 42, z * .24f + 17);
                    Color color = Color.Lerp(new Color(.37f,.39f,.27f), new Color(.55f,.55f,.35f), patch);
                    tint.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(tint);
                }
            }
            var health = block.GetComponent<Health>();
            health.Initialize(config.hp);
            health.Died += () =>
            {
                blocks.Remove(cell);
                dirty = true;
                block.SetActive(false);
                Destroy(block);
            };
        }

        public Vector2Int Cell(Vector3 position) => new(Mathf.RoundToInt(position.x / Size), Mathf.RoundToInt(position.z / Size));
        public Vector3 Center(Vector2Int cell) => new(cell.x * Size, Height(cell), cell.y * Size);

        public float Height(Vector2Int cell)
        {
            for (int y = 2; y >= -1; y--)
                if (blocks.Contains(new Vector3Int(cell.x, y, cell.y))) return (y + 1) * Size;
            bool insideYard = Mathf.Abs(cell.x) <= groundExtent.x && Mathf.Abs(cell.y) <= groundExtent.y;
            return insideYard ? -Size : float.NegativeInfinity;
        }

        public bool NextStep(Vector2Int from, Vector2Int target, float climb, out Vector2Int result)
        {
            if (dirty || lastTarget != target || lastClimb != climb)
            {
                lastTarget = target;
                lastClimb = climb;
                dirty = false;
                next.Clear();
                frontier.Clear();
                if (!float.IsNegativeInfinity(Height(target)))
                {
                    next[target] = target;
                    frontier.Enqueue(target);
                }
                while (frontier.Count > 0)
                {
                    var cell = frontier.Dequeue();
                    float height = Height(cell);
                    foreach (var direction in Neighbors)
                    {
                        var neighbor = cell + direction;
                        if (next.ContainsKey(neighbor)) continue;
                        float other = Height(neighbor);
                        if (float.IsNegativeInfinity(other) || height - other > climb + .001f) continue;
                        next.Add(neighbor, cell);
                        frontier.Enqueue(neighbor);
                    }
                }
            }
            return next.TryGetValue(from, out result);
        }
    }
}
