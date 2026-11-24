// Ответственности
// - Задаёт урон, дальность и радиус контакта атаки.

using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/Attack")]
    public sealed class AttackConfig : ScriptableObject
    {
        [Min(1)] public int damage = 30;
        [Tooltip("Radius of continuous contact. Zero uses a single melee ray.")]
        [Min(0)] public float contactRadius;
        [Min(0.1f)] public float range = 2.4f;
    }
}
