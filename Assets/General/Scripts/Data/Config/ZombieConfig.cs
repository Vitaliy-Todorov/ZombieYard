using UnityEngine;

namespace ZombieYard
{
    [System.Serializable]
    public struct LootEntry
    {
        public AmmoConfig ammo;
        [Range(0, 1)] public float chance;
        public Vector2Int quantity;
    }

    [CreateAssetMenu(menuName = "Zombie Yard/Zombie")]
    public sealed class ZombieConfig : ScriptableObject
    {
        [Min(1)] public int hp = 65;
        [Min(.1f)] public float speed = 1.65f, maxClimbHeight = 1.55f, climbDuration = .85f;
        public WeaponConfig weapon;
        public LootEntry[] loot;
    }
}
