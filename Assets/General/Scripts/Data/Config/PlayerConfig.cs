using UnityEngine;

namespace ZombieYard
{
    [System.Serializable]
    public struct AmmoStack
    {
        public AmmoConfig ammo;
        [Min(0)] public int count;
    }

    [CreateAssetMenu(menuName = "Zombie Yard/Player")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Min(1)] public int hp = 100;
        [Min(.1f)] public float walk = 4, sprint = 6.8f, jumpHeight = 1.55f, pickupReach = 2.8f;
        [Min(1)] public int partialHealing = 35;
        public GameObject partialMedkitVisual, fullMedkitVisual;
        public const int WeaponSlots = 2;
        public WeaponConfig[] startingWeapons;
        public WeaponConfig fists;
        public AmmoStack[] startingReserve;
    }
}
