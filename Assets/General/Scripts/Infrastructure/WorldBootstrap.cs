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
            Vector3 yardOffset = Vector3.up * (YardGrid.YardLevel * scene.world.blockSize);
            var supply = scene.riflePosition.position + yardOffset;
            scene.grid.Build(scene.world, supply);
            scene.player.transform.position += yardOffset;
            Physics.SyncTransforms();
            scene.player.Initialize(scene.grid.VoidHeight);
            scene.player.Fog = new GameObject("Exterior fog").AddComponent<ExteriorFog>();
            scene.player.Fog.Initialize(scene.world, scene.grid, scene.player);
            supply.y = Mathf.Max(supply.y, scene.grid.Height(scene.grid.Cell(supply)) + .22f);
            Pickup.ForWeapon(new HeldWeapon(scene.rifle), supply);
            var crate = scene.grid.Cell(supply);
            Vector3 BesideCrate(int x, int z) =>
                scene.grid.Center(crate + new Vector2Int(x, z)) + Vector3.up * .3f;
            Pickup.ForWeapon(new HeldWeapon(scene.staff), BesideCrate(-4, 1));
            Pickup.ForWeapon(new HeldWeapon(scene.chainsaw), BesideCrate(-4, -1));
            Pickup.ForMedkit(scene.player.config, false, BesideCrate(-5, 1));
            Pickup.ForMedkit(scene.player.config, true, BesideCrate(-5, -1));
        }
    }
}
