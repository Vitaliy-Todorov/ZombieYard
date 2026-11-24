// Ответственности
// - Задаёт урон, число снарядов, разброс и параметры полёта боеприпаса.
// - Связывает боеприпас с префабом снаряда и моделью подбираемых патронов.

using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/Ammo")]
    public sealed class AmmoConfig : ScriptableObject
    {
        public string title;
        [Min(1)] public int damage = 12;
        [Min(1)] public int projectiles = 6;
        [Range(0, 30)] public float spreadDegrees = 4;
        [Min(1)] public float range = 45;
        [Min(1)] public float speed = 36;
        [Tooltip("Positive lifetime enables ricochets and replaces the range limit during flight.")]
        [Min(0)] public float ricochetSeconds;
        public ShotProjectile projectilePrefab;
        public GameObject pickupVisual;
    }
}
