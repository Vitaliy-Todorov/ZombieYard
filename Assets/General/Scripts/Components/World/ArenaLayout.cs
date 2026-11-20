using UnityEngine;

namespace ZombieYard
{
    // X and Z below are cell offsets from the eye. Heights are in world grid levels.
    public sealed class ArenaLayout
    {
        private readonly int centerX;
        private readonly float size;
        public const int Roof = YardGrid.YardLevel + 7;

        internal ArenaLayout(int yardWidth, int ridge, float blockSize)
        {
            centerX = yardWidth + ridge - 4;
            size = blockSize;
        }

        public Vector3 Floor(int x, int z) => new Vector3(centerX + x, FloorLevel(z), z) * size;
        private static int FloorLevel(int z) => YardGrid.YardLevel + Mathf.Clamp(Mathf.Abs(z) - 4, 0, 2);
        private static bool Chamber(int x, int z) =>
            Mathf.Abs(x) <= 8 && Mathf.Abs(z) <= 7 && Mathf.Abs(x) + Mathf.Abs(z) <= 12;

        internal bool Contains(Vector3 feet)
        {
            int x = Mathf.FloorToInt(feet.x / size + .5f) - centerX;
            int z = Mathf.FloorToInt(feet.z / size + .5f);
            return Chamber(x, z) && feet.y >= FloorLevel(z) * size - .1f && feet.y < Roof * size;
        }

        internal bool ShapeColumn(int worldX, int z, ref int surface, out int floor, out int ceiling,
            out bool pillar, out bool rose)
        {
            int x = worldX - centerX;
            int ax = Mathf.Abs(x), az = Mathf.Abs(z);
            int radius = Mathf.Max(Mathf.Max(ax, az), (ax + az + 1) * 2 / 3);
            floor = FloorLevel(z);
            ceiling = Chamber(x, z) ? Roof : floor;
            pillar = Mathf.Abs(x) == 4 && Mathf.Abs(z) == 3;
            rose = false;
            if (radius > 13) return false;
            int summit = Roof + 10 - radius;
            int foothill = Roof + 2 - 3 * (radius - 8);
            surface = Mathf.Max(surface, Mathf.Min(summit, foothill));
            rose = radius <= 8 && surface >= Roof + 2;
            return true;
        }
    }
}
