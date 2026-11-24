// Ответственности
// - Создаёт текстурный атлас блоков и выбирает участок текстуры для каждой грани.

using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    internal static class YardAtlas
    {
        private const int Pixels = 16, Tiles = 13;
        private enum Tile { Turf, Earth, Wood, Brick, Crate, Bark, Leaves, Metal, TurfSide, EndGrain, Stone, Bedrock, Rose }

        // Block prefabs supply the art palette; a chunk shares a single material and texture.
        private static Color32 Sample(BlockConfig block, string part, Color32 fallback)
        {
            if (!block || !block.prefab) return fallback;
            var child = block.prefab.transform.Find(part);
            var renderer = child ? child.GetComponent<MeshRenderer>() : block.prefab.GetComponentInChildren<MeshRenderer>();
            return renderer && renderer.sharedMaterial ? (Color32)renderer.sharedMaterial.color : fallback;
        }

        internal static Texture2D Create(WorldConfig config)
        {
            Color32 grass = Sample(config.soil, "Turf", new Color32(112, 131, 68, 255));
            Color32 earth = Sample(config.soil, "Soil", new Color32(119, 83, 56, 255));
            Color32[] palette = {
                grass, earth, Sample(config.fence, "Board", new Color32(147, 105, 69, 255)),
                Sample(config.masonry, "Brick", new Color32(155, 81, 63, 255)),
                Sample(config.supplyCrate, "Case", new Color32(181, 132, 78, 255)),
                new(100, 71, 49, 255), new(68, 95, 65, 255), new(108, 133, 133, 255), earth, new(165, 125, 81, 255),
                new(103, 98, 89, 255), new(59, 63, 68, 255), new(169, 100, 114, 255)
            };
            var atlas = new Texture2D(Tiles * Pixels, Pixels, TextureFormat.RGBA32, false)
                { name = "Yard block tiles", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[atlas.width * Pixels];
            var random = new System.Random(712);
            for (int tile = 0; tile < Tiles; tile++)
            for (int y = 0; y < Pixels; y++)
            for (int x = 0; x < Pixels; x++)
            {
                var surface = (Tile)tile;
                Color color = palette[tile];
                float shade = 1f + (float)(random.NextDouble() - .5) * .14f;
                if (surface == Tile.TurfSide && y >= 13 - (x * 7 % 3)) color = grass;
                if ((surface == Tile.Wood || surface == Tile.Bark) && (x % 5 == 1 || x == 15)) shade *= .78f;
                if (surface == Tile.Brick && (y % 8 == 0 || (x + 8 * (y / 8)) % 16 == 0)) color = new Color32(98,88,77,255);
                if (surface == Tile.Crate && (x < 2 || y < 2 || x > 13 || y > 13 || Mathf.Abs(x-y) < 2)) shade *= .67f;
                if (surface == Tile.Leaves && ((x / 3 + y / 2) % 3 == 0)) shade *= .86f;
                if (surface == Tile.EndGrain && Mathf.Max(Mathf.Abs(x - 7), Mathf.Abs(y - 7)) % 3 == 1) shade *= .80f;
                color *= shade;
                color.a = 1;
                pixels[y * atlas.width + tile * Pixels + x] = color;
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            return atlas;
        }

        internal static void AddUVs(List<Vector2> uvs, Voxel kind, int normalY)
        {
            int tile = kind switch
            {
                Voxel.Stone => (int)Tile.Stone,
                Voxel.Bedrock => (int)Tile.Bedrock,
                Voxel.Pillar => (int)Tile.Brick,
                Voxel.RoseStone or Voxel.RoseBedrock => (int)Tile.Rose,
                Voxel.Turf => (int)Tile.Turf,
                Voxel.Earth => (int)Tile.Earth,
                Voxel.Wood => (int)Tile.Wood,
                Voxel.Brick => (int)Tile.Brick,
                Voxel.Crate => (int)Tile.Crate,
                Voxel.Bark => (int)Tile.Bark,
                Voxel.Leaves => (int)Tile.Leaves,
                Voxel.Metal => (int)Tile.Metal,
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind))
            };
            if (kind == Voxel.Turf && normalY <= 0) tile = normalY == 0 ? (int)Tile.TurfSide : (int)Tile.Earth;
            if (kind == Voxel.Bark && normalY != 0) tile = (int)Tile.EndGrain;
            float inset = .5f / Pixels;
            float left = (tile + inset) / Tiles, right = (tile + 1 - inset) / Tiles;
            uvs.Add(new Vector2(left, inset));
            uvs.Add(new Vector2(right, inset));
            uvs.Add(new Vector2(right, 1 - inset));
            uvs.Add(new Vector2(left, 1 - inset));
        }
    }
}
