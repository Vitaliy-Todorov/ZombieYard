using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    // Each cell holds 0..255 units. Apart from spillage into the void, every transfer
    // subtracts from one cell exactly what it adds to the other.
    internal sealed class FogVolume
    {
        private const int Width = YardChunk.Width;
        private static readonly Vector3Int[] Sides = {
            Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back
        };
        private readonly YardGrid grid;
        private readonly Vector2Int extent;
        private readonly int layers, chunksX;
        private readonly byte[] amounts;
        private readonly int[] chunkTotals;
        private HashSet<Vector3Int> pending = new(), current = new();
        internal readonly HashSet<int> ChangedChunks = new();
        internal int Layers => layers;
        internal int ChunkLength => Width * layers * Width;

        internal FogVolume(YardGrid yard, int depth)
        {
            grid = yard;
            extent = grid.Extent;
            int ceiling = -Mathf.Max(1, depth);
            layers = Mathf.Max(1, ceiling - grid.Bottom);
            chunksX = (2 * extent.x + Width) / Width;
            int chunksZ = (2 * extent.y + Width) / Width;
            chunkTotals = new int[chunksX * chunksZ];
            amounts = new byte[chunkTotals.Length * ChunkLength];

            // The generated terrain has solid ground columns. Fill their exposed lowlands
            // once at creation; opening more capacity later never creates additional fog.
            for (int z = -extent.y; z <= extent.y; z++)
            for (int x = -extent.x; x <= extent.x; x++)
            for (int y = grid.Bottom; y < ceiling; y++)
            {
                var cell = new Vector3Int(x, y, z);
                if (grid.BlockAt(cell) == Voxel.Air) SetAmount(Index(cell), byte.MaxValue);
            }
        }

        private int Index(Vector3Int cell)
        {
            int x = cell.x + extent.x, y = cell.y - grid.Bottom, z = cell.z + extent.y;
            if ((uint)x > 2 * extent.x || (uint)z > 2 * extent.y || (uint)y >= layers) return -1;
            int chunk = x / Width + chunksX * (z / Width);
            return chunk * ChunkLength + x % Width + Width * (y + layers * (z % Width));
        }

        internal Vector3 ChunkOrigin(int id) => new Vector3((id % chunksX) * Width - extent.x - .5f,
            grid.Bottom, (id / chunksX) * Width - extent.y - .5f) * grid.Size;
        internal bool HasFog(int chunk) => chunkTotals[chunk] > 0;
        internal void Upload(int chunk, Texture3D texture)
        {
            texture.SetPixelData(amounts, 0, chunk * ChunkLength);
            texture.Apply(false);
        }

        internal float FillAt(Vector3Int cell)
        {
            int i = Index(cell);
            return i >= 0 ? amounts[i] / 255f : 0;
        }

        internal bool Contains(Vector3 position)
        {
            var column = grid.Cell(position);
            float height = position.y / grid.Size;
            int y = Mathf.FloorToInt(height);
            return height - y < FillAt(new Vector3Int(column.x, y, column.y));
        }

        private void SetAmount(int index, int value)
        {
            int chunk = index / ChunkLength;
            chunkTotals[chunk] += value - amounts[index];
            amounts[index] = (byte)value;
            ChangedChunks.Add(chunk);
        }

        internal void Wake(Vector3Int cell)
        {
            if (Index(cell) < 0) return;
            pending.Add(cell);
            pending.Add(cell + Vector3Int.up);
            foreach (var side in Sides) pending.Add(cell + side);
        }

        internal void Advance()
        {
            (current, pending) = (pending, current);
            foreach (var cell in current)
            {
                int from = Index(cell);
                if (from < 0 || amounts[from] == 0) continue;
                if (cell.y == grid.Bottom)
                {
                    SetAmount(from, 0);
                    Wake(cell);
                    continue;
                }

                Transfer(cell, from, Vector3Int.down);
                foreach (var side in Sides)
                {
                    if (amounts[from] == 0) break;
                    Transfer(cell, from, side);
                }
            }
            current.Clear();
        }

        private void Transfer(Vector3Int cell, int from, Vector3Int step)
        {
            var neighbor = cell + step;
            int to = Index(neighbor);
            if (to < 0 || grid.BlockAt(neighbor) != Voxel.Air) return;
            int moved = step.y < 0 ? Mathf.Min(amounts[from], 255 - amounts[to])
                : (amounts[from] - amounts[to]) / 2;
            if (moved <= 0) return;
            SetAmount(from, amounts[from] - moved);
            SetAmount(to, amounts[to] + moved);
            Wake(cell);
            Wake(neighbor);
        }
    }
}
