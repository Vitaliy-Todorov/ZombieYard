// Ответственности
// - Определяет форму горы, полости, уступов и колонн арены в блочной сетке.
// - Вычисляет позиции на полу арены и проверяет вход в её границы.

using UnityEngine;

namespace ZombieYard
{
    // X and Z below are cell offsets from the eye. Heights are in world grid levels.
    public sealed class ArenaLayout
    {
        private readonly int centerX;
        private readonly YardGrid grid;
        public const int Roof = YardGrid.YardLevel + 7;

        internal ArenaLayout(int yardWidth, int ridge, YardGrid grid)
        {
            centerX = yardWidth + ridge - 4;
            this.grid = grid;
        }

        public Vector3 Floor(int x, int z) => grid.WorldFeet(new Vector3Int(centerX + x, FloorLevel(z), z));
        internal System.Collections.Generic.IEnumerable<Vector3> PortalPositions()
        {
            yield return Floor(1, -6);
            yield return Floor(1, 6);
            yield return Floor(7, 0);
        }

        internal Vector3 MedkitPosition(int side) => Floor(-6, side * 3);
        internal Vector3 EyePosition => Floor(0, 0) + Vector3.up * (3.2f * grid.Size);

        private static int FloorLevel(int z) => YardGrid.YardLevel + Mathf.Clamp(Mathf.Abs(z) - 4, 0, 2);
        private static bool Chamber(int x, int z) =>
            Mathf.Abs(x) <= 8 && Mathf.Abs(z) <= 7 && Mathf.Abs(x) + Mathf.Abs(z) <= 12;

        internal bool Contains(Vector3 feet)
        {
            var cell = grid.Cell(feet);
            return Chamber(cell.x - centerX, cell.y) && feet.y >= grid.LevelY(FloorLevel(cell.y)) - .1f
                && feet.y < grid.LevelY(Roof);
        }

        internal (int Surface, int Floor, int Ceiling, bool Pillar, bool Rose, bool InMountain)
            ColumnAt(int worldX, int z, int surface)
        {
            int x = worldX - centerX;
            int ax = Mathf.Abs(x), az = Mathf.Abs(z);
            int radius = Mathf.Max(Mathf.Max(ax, az), (ax + az + 1) * 2 / 3);
            int floor = FloorLevel(z);
            int ceiling = Chamber(x, z) ? Roof : floor;
            bool pillar = Mathf.Abs(x) == 4 && Mathf.Abs(z) == 3;
            if (radius > 13) return (surface, floor, ceiling, false, false, false);
            int summit = Roof + 10 - radius;
            int foothill = Roof + 2 - 3 * (radius - 8);
            surface = Mathf.Max(surface, Mathf.Min(summit, foothill));
            return (surface, floor, ceiling, pillar, radius <= 8 && surface >= Roof + 2, true);
        }
    }
}
