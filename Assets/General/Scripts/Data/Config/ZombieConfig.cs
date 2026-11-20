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
    }
}
