using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ZombieYard
{
    public class GameLifetimeScope : LifetimeScope
    {
        public YardGrid grid;
        public WorldConfig world;
        public YardPlayer player;
        public ZombieSpawnerConfig spawn;
        public Transform[] points;
        public Transform riflePosition;
        public WeaponConfig rifle, staff, chainsaw;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<WorldBootstrap>();
            builder.RegisterEntryPoint<ZombieSpawnService>();
        }
    }
}
