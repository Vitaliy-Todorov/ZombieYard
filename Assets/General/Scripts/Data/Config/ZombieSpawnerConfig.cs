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
        public Material portalMaterial;
        [Range(0, 1)] public float portalChance = .12f, purpleChance = .2f;
        [Min(0)] public float portalSpacing = 6;
        [Min(.1f)] public float undergroundMinInterval = 5, undergroundMaxInterval = 8;
        [Min(1)] public int undergroundLimit = 4;
        [Min(1)] public int arenaLimit = 3;
        [Min(.1f)] public float arenaInterval = 4;
    }
}
