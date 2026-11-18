using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/ZombieSpawner")]
    public sealed class ZombieSpawnerConfig : ScriptableObject
    {
        public ZombieAgent prefab;
        public ZombieConfig zombie;
        [Min(.1f)] public float minInterval = 3, maxInterval = 5;
        [Min(1)] public int limit = 12;
    }
}
