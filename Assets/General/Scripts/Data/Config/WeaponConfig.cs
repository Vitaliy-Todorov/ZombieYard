// Ответственности
// - Задаёт способ атаки, задержку применения и модель оружия.
// - Описывает совместимые патроны, ёмкость магазина и длительность перезарядки.

using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/Weapon")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public string title;
        [Min(.05f)] public float delay = .65f;
        public AttackConfig melee;
        public bool diggingMotion;
        public AmmoConfig staffBolt;
        public AmmoConfig[] compatibleAmmo;
        [Min(0)] public int capacity;
        [Min(.05f)] public float reloadSeconds = 1.3f;
        public GameObject visual;
        internal void ValidateLoadout()
        {
            if (melee && staffBolt)
                throw new System.InvalidOperationException($"{name}: select one attack type.");
            if (UsesAmmo && (capacity <= 0 || compatibleAmmo == null || compatibleAmmo.Length == 0 ||
                             System.Array.Exists(compatibleAmmo, ammo => !ammo)))
                throw new System.InvalidOperationException($"{name}: a firearm needs capacity and compatible ammunition.");
        }

        public bool UsesContactAttack => melee && melee.contactRadius > 0;
        public bool UsesAmmo => melee == null && staffBolt == null;
    }
}
