using UnityEngine;
using VContainer.Unity;

namespace ZombieYard
{
    public sealed class WorldBootstrap : IStartable
    {
        private readonly GameLifetimeScope scene;
        public WorldBootstrap(GameLifetimeScope scene) => this.scene = scene;
        public void Start()
        {
            var supply = scene.riflePosition.position;
            scene.grid.Build(scene.world, supply);
            Physics.SyncTransforms();
            scene.player.Initialize(-scene.grid.Size - 6f);
            supply.y = Mathf.Max(supply.y, scene.grid.Height(scene.grid.Cell(supply)) + .22f);
            Pickup.ForWeapon(new HeldWeapon(scene.rifle), supply);
        }
    }
}
