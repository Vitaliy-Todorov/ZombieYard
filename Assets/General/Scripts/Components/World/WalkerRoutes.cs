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
        private readonly YardGrid grid;
        private readonly Dictionary<Vector3Int, Vector3Int> next = new();
        private readonly Queue<Vector3Int> frontier = new();
        private Vector3Int target;
        private int climb, height, radius;
        private bool dirty = true;
        private int frame = -1, remaining;

        internal WalkerRoutes(YardGrid grid) => this.grid = grid;
        internal void Invalidate() => dirty = true;

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
                int highest = grid.WalkerTop(new Vector2Int(from.x, from.z)) + 1;
                int lowest = Mathf.Max(grid.Bottom + 1, to.y - climb);
                for (from.y = lowest; from.y <= highest; from.y++)
                {
                    if (!grid.WalkerSolid(from + Vector3Int.down) || next.ContainsKey(from)) continue;
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
                if (grid.WalkerSolid(new Vector3Int(x, y, z))) return false;
            return true;
        }
    }
}
