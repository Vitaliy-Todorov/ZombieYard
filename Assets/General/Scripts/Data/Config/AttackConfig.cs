using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/Attack")]
    public sealed class AttackConfig : ScriptableObject
    {
        [Min(1)] public int damage = 30;
        [Min(0.1f)] public float range = 2.4f;
    }
}
