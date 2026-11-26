// Ответственности
// - Задаёт здоровье, движение, оружие и особенности видов ходоков.
// - Описывает условия, вероятность и состав добычи.

using UnityEngine;

namespace ZombieYard
{
    [System.Serializable]
    public struct LootEntry
    {
        public ZombieKind minimumKind;
        public AmmoConfig ammo;
        public WeaponConfig weapon;
        public bool partialMedkit;
        [Range(0, 1)] public float chance;
        public Vector2Int quantity;

        internal void Validate(string owner, int row)
        {
            if ((ammo ? 1 : 0) + (weapon ? 1 : 0) + (partialMedkit ? 1 : 0) != 1)
                throw new System.InvalidOperationException($"{owner}: loot row {row} must grant exactly one item.");
        }
    }

    [CreateAssetMenu(menuName = "Zombie Yard/Zombie")]
    public sealed class ZombieConfig : ScriptableObject
    {
        public ZombieKind kind;
        [Min(1)] public int redStrength = 2, purpleStrength = 3;
        public Color redTint = new(.85f, .12f, .08f), purpleTint = new(.57f, .13f, .9f);
        [Min(1)] public int hp = 65;
        [Min(.1f)] public float speed = 1.65f, maxClimbHeight = 1.55f, climbDuration = .85f;
        [Min(1)] public float turnSpeed = 540;
        public WeaponConfig weapon;
        public LootEntry[] loot;

        internal void ValidateLoot()
        {
            for (int i = 0; i < loot.Length; i++)
                loot[i].Validate(name, i + 1);
        }
    }
}
