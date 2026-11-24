// Ответственности
// - Строит общее для ходоков поле путей к игроку с учётом опор, высоты и ширины проходов.
// - Учитывает мебель при проверке опоры и проходимости клетки.
// - Выдаёт следующий шаг и пересчитывает путь при изменении цели или участка.

using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    // A stand node is the air voxel at a walker's feet. All walkers headed for the
    // same target with the same dimensions share the reverse breadth-first search.
    internal sealed class WalkerRoutes
    {
        private static readonly Vector3Int[] Directions =
            { Vector3Int.forward, Vector3Int.right, Vector3Int.back, Vector3Int.left };
        // Vertical span of fixed shop furniture in each occupied navigation column.
        private readonly Dictionary<Vector2Int, Vector2Int> furnitureLevels = new();
        private readonly YardGrid grid;
        private readonly Dictionary<Vector3Int, Vector3Int> next = new();
        private readonly Queue<Vector3Int> frontier = new();
        private Vector3Int target;
        private int climb, height, radius;
        private bool dirty = true;
        private int frame = -1, remaining;

        internal static (int height, int radius) BodyCells(CapsuleCollider body, float size)
        {
            var (height, radius) = BodySize(body);
            return (Mathf.CeilToInt(height / size), Mathf.Max(0, Mathf.CeilToInt(radius / size - .5f)));
        }

        internal static (float height, float radius) BodySize(CapsuleCollider body)
        {
            var scale = body.transform.lossyScale;
            float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return (Mathf.Max(2 * radius, body.height * Mathf.Abs(scale.y)), radius);
        }

        internal WalkerRoutes(YardGrid grid) => this.grid = grid;
        internal void Invalidate() => dirty = true;

        internal void RegisterCounter(Bounds bounds)
        {
            // A face exactly on a cell edge does not occupy the neighbouring cell.
            var min = bounds.min + Vector3.one * .001f;
            var max = bounds.max - Vector3.one * .001f;
            var first = grid.Cell(min);
            var last = grid.Cell(max);
            var levels = new Vector2Int(Mathf.FloorToInt((min.y - grid.transform.position.y) / grid.Size), Mathf.FloorToInt((max.y - grid.transform.position.y) / grid.Size));
            for (int x = first.x; x <= last.x; x++)
            for (int z = first.y; z <= last.y; z++)
                furnitureLevels[new Vector2Int(x, z)] = levels;
            Invalidate();
        }

        private int WalkerTop(Vector2Int column) => furnitureLevels.TryGetValue(column, out var levels)
            ? Mathf.Max(grid.Top(column), levels.y) : grid.Top(column);

        internal bool WalkerSolid(Vector3Int cell) => grid.BlockAt(cell) != Voxel.Air ||
            (furnitureLevels.TryGetValue(new Vector2Int(cell.x, cell.z), out var levels) &&
             cell.y >= levels.x && cell.y <= levels.y);

        // Project feet onto the first supporting block below them, including under a roof.
        internal bool TryGround(Vector3 feet, out Vector3Int stand)
        {
            var column = grid.Cell(feet);
            int top = WalkerTop(column);
            int y = Mathf.Min(top, Mathf.FloorToInt((feet.y - grid.transform.position.y + .04f) / grid.Size) - 1);
            stand = new Vector3Int(column.x, y + 1, column.y);
            while (y >= grid.Bottom)
            {
                if (y == top || WalkerSolid(new Vector3Int(column.x, y, column.y)))
                {
                    stand.y = y + 1;
                    return true;
                }
                y--;
            }
            return false;
        }

        internal bool NextStep(Vector3Int from, Vector3Int goal, int climbCells,
            int heightCells, int radiusCells, out Vector3Int step)
        {
            if (dirty || target != goal || climb != climbCells || height != heightCells || radius != radiusCells)
            {
                target = goal;
                climb = climbCells;
                height = heightCells;
                radius = radiusCells;
                dirty = false;
                next.Clear();
                frontier.Clear();
                if (Clear(goal, goal.y + height, radius))
                {
                    next.Add(goal, goal);
                    frontier.Enqueue(goal);
                }
            }

            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                remaining = 512;
            }
            while (!next.ContainsKey(from) && frontier.Count > 0 && remaining > 0)
            {
                remaining--;
                Expand(frontier.Dequeue());
            }
            return next.TryGetValue(from, out step);
        }

        private void Expand(Vector3Int to)
        {
            foreach (var direction in Directions)
            {
                var from = to + direction;
                int highest = WalkerTop(new Vector2Int(from.x, from.z)) + 1;
                int lowest = Mathf.Max(grid.Bottom + 1, to.y - climb);
                for (from.y = lowest; from.y <= highest; from.y++)
                {
                    if (!WalkerSolid(from + Vector3Int.down) || next.ContainsKey(from)) continue;
                    // Raise before crossing a ledge, or cross before dropping. Check the
                    // swept body in both columns so a low overhang blocks that move.
                    int head = Mathf.Max(from.y, to.y) + height;
                    if (!Clear(from, head, radius) || (from.y > to.y && !Clear(to, head, radius))) continue;
                    next.Add(from, to);
                    frontier.Enqueue(from);
                }
            }
        }

        internal bool Clear(Vector3Int feet, int headExclusive, int radius)
        {
            for (int x = feet.x - radius; x <= feet.x + radius; x++)
            for (int z = feet.z - radius; z <= feet.z + radius; z++)
            for (int y = feet.y; y < headExclusive; y++)
                if (WalkerSolid(new Vector3Int(x, y, z))) return false;
            return true;
        }
    }
}
