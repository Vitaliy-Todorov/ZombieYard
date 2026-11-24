// Ответственности
// - Задаёт границы полости магазина и переводит локальные места интерьера в клетки двора.

using UnityEngine;

namespace ZombieYard
{
    public readonly struct ShopLayout
    {
        public const int Height = 4;
        public readonly Vector2Int Center;
        public readonly int FloorLevel;
        internal int CeilingLevel => FloorLevel + Height;
        internal ShopLayout(Vector2Int center, int floorLevel)
        {
            Center = center;
            FloorLevel = floorLevel;
        }

        public bool ContainsColumn(int x, int z) => Mathf.Abs(x - Center.x) <= 3 && Mathf.Abs(z - Center.y) <= 2;
        public Vector3Int Cell(int x, int z) => new(Center.x + x, FloorLevel, Center.y + z);
    }
}
