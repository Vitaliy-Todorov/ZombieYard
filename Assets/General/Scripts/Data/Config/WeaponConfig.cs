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
        public AmmoConfig[] compatibleAmmo;
        [Min(0)] public int capacity;
        [Min(.05f)] public float reloadSeconds = 1.3f;
        public GameObject visual;
        public bool UsesAmmo => melee == null;
    }
}
