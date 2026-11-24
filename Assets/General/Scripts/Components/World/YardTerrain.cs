// Ответственности
// - Заполняет исходную сетку рельефом, полостями и постройками до создания чанков.

using System.Collections.Generic;
using UnityEngine;
using static ZombieYard.YardGrid;

namespace ZombieYard
{
    internal sealed class YardTerrain
    {
        internal const int SoftLayers = 2;
        private readonly YardGrid grid;
        private readonly int w, d, ridge, margin, drop, digDepth;
        private readonly List<Vector3Int> trees = new();

        internal YardTerrain(YardGrid grid, Vector2Int halfSize, int ridge, int drop, int digDepth)
        {
            this.grid = grid;
            w = halfSize.x;
            d = halfSize.y;
            this.ridge = ridge;
            margin = grid.Extent.x - w;
            this.drop = drop;
            this.digDepth = digDepth;
        }

        internal void Populate(Vector2Int supplyCell, bool hasCrate)
        {
            for (int x = -grid.Extent.x; x <= grid.Extent.x; x++)
            for (int z = -grid.Extent.y; z <= grid.Extent.y; z++)
                FillColumn(x, z);

            foreach (var root in trees)
                Tree(root.x, root.y, root.z, (root.x + root.z) % 3 == 0 ? 5 : 4);
            PlaceYardStructures(supplyCell, hasCrate);
            PlaceWaterTower();
        }

        private void FillColumn(int x, int z)
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
            var mountain = grid.Arena.ColumnAt(x, z, surface);
            surface = mountain.Surface;
            bool shopColumn = grid.Shop.ContainsColumn(x, z);
            int bedrock = shopColumn ? grid.Shop.FloorLevel - 1 : plain - digDepth;
            for (int y = grid.Bottom; y <= surface; y++)
            {
                if (shopColumn && y >= grid.Shop.FloorLevel && y < grid.Shop.CeilingLevel) continue;
                Voxel kind;
                if (y >= mountain.Floor && y < mountain.Ceiling)
                {
                    if (!mountain.Pillar) continue;
                    kind = Voxel.Pillar;
                }
                else if (mountain.Rose && y >= surface - 1) kind = beyondRidge ? Voxel.RoseBedrock : Voxel.RoseStone;
                else if (beyondRidge || outerWall || y <= bedrock) kind = Voxel.Bedrock;
                else if (y == surface) kind = Voxel.Turf;
                else if (y > surface - SoftLayers - (outside <= 0 ? drop : 0)) kind = Voxel.Earth;
                else kind = Voxel.Stone;
                grid.Place(kind, x, y, z);
            }
            if (outside == 0) grid.Place(Voxel.Wood, x, YardLevel, z);
            if (!mountain.InMountain && outside < margin - 3 && outside > drop + 4 && x % 5 == 0 && z % 5 == 0)
            {
                float cover = Mathf.PerlinNoise((x + 130) * .18f, (z + 270) * .18f);
                if (cover > .43f) trees.Add(new Vector3Int(x, surface + 1, z));
            }
        }

        private void PlaceYardStructures(Vector2Int supplyCell, bool hasCrate)
        {
            for (int x = -w + 2; x <= -w + 5; x++)
            for (int y = 0; y < 3; y++)
                grid.Place(Voxel.Brick, x, YardLevel + y, d - 3);
            for (int z = d - 5; z < d - 3; z++)
            for (int y = 0; y < 2; y++)
                grid.Place(Voxel.Brick, -w + 2, YardLevel + y, z);
            for (int x = w - 5; x <= w - 3; x++) grid.Place(Voxel.Brick, x, YardLevel, 1);
            if (hasCrate)
            {
                var cell = supplyCell;
                grid.Place(Voxel.Crate, cell.x, YardLevel, cell.y);
            }
        }

        private void PlaceWaterTower()
        {
            // The old water tower is a landmark on the far side of the northern slope.
            int towerZ = d + drop + 6;
            for (int x = 5; x <= 7; x++)
            for (int z = towerZ - 1; z <= towerZ + 1; z++)
            {
                if (x != 6 && z != towerZ)
                    for (int y = YardLevel - drop - 1; y < 1; y++) grid.Place(Voxel.Wood, x, y, z);
                for (int y = 1; y <= 2; y++) grid.Place(Voxel.Metal, x, y, z);
            }
        }

        private void Tree(int x, int baseY, int z, int height)
        {
            for (int y = 0; y < height; y++) grid.Place(Voxel.Bark, x, baseY + y, z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                grid.Place(Voxel.Leaves, x + dx, baseY + height - 1, z + dz);
                if (Mathf.Abs(dx) + Mathf.Abs(dz) < 2) grid.Place(Voxel.Leaves, x + dx, baseY + height, z + dz);
            }
        }

    }
}
