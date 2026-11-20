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
            BuildShop();
            BuildArena();
        }

        private void BuildArena()
        {
            var grid = scene.grid;
            var layout = grid.Arena;
            scene.Eye = Object.Instantiate(scene.world.eye.prefab, grid.transform);
            scene.Eye.Initialize(scene.world.eye, scene.player, grid);
            scene.player.GetComponent<YardHud>().Boss = scene.Eye;
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 position = layout.Floor(-6, side * 3);
                Pickup.ForMedkit(scene.player.config, true, position + Vector3.up * .35f);
                var light = new GameObject("Свет у аптечки").AddComponent<Light>();
                light.transform.SetParent(grid.transform, false);
                light.transform.position = position + Vector3.up * (2.8f * grid.Size);
                light.color = new Color(.92f, .77f, .62f);
                light.intensity = 3;
                light.range = 9 * grid.Size;
            }
        }

        private void BuildShop()
        {
            var world = scene.world;
            var player = scene.player;
            var grid = scene.grid;
            var room = new GameObject("Магазин под двором").transform;
            room.SetParent(grid.transform, false);
            room.localPosition = new Vector3(0, grid.ShopFloorLevel, 1) * grid.Size;
            room.localScale = Vector3.one * grid.Size;
            var lamp = new GameObject("Свет прилавков").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.localPosition = new Vector3(0, 2.8f, -.8f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1, .82f, .57f);
            lamp.range = 8 * grid.Size;
            lamp.intensity = 3;

            void Stand(int x, string title, ShopTerms terms, GameObject visual, System.Func<bool> deliver, string refusal)
            {
                var counter = Object.Instantiate(world.shopCounter, room);
                counter.transform.localPosition = new Vector3(x, 0, 1);
                counter.AddComponent<ShopStand>().Initialize(title, terms, visual, deliver, refusal);
            }

            void Ammo(int x, AmmoDeal deal)
            {
                var pack = deal.pack;
                Stand(x, $"{pack.ammo.title} ×{pack.count}", deal.terms, pack.ammo.pickupVisual, () =>
                {
                    if (pack.count <= 0 || player.Gear.Stock(pack.ammo) > int.MaxValue - pack.count) return false;
                    player.Gear.Add(pack.ammo, pack.count);
                    return true;
                }, "Патроны недоступны");
            }

            Ammo(-2, world.shells);
            Ammo(-1, world.rifleRounds);
            int heal = player.config.partialHealing;
            Stand(0, "Малая аптечка", world.partialMedkit, player.config.partialMedkitVisual,
                () => player.Health.Heal(heal), "Здоровье уже полное");
            Stand(1, "Полная аптечка", world.fullMedkit, player.config.fullMedkitVisual,
                () => player.Health.Heal(int.MaxValue), "Здоровье уже полное");
            var staff = scene.staff;
            Stand(2, staff.title, world.staff, staff.visual,
                () => player.Gear.Take(new HeldWeapon(staff)), "Слоты заняты · G — бросить оружие");
            Physics.SyncTransforms();
            foreach (Transform counter in room)
            {
                if (!counter.TryGetComponent<ShopStand>(out _)) continue;
                var parts = counter.GetComponentsInChildren<BoxCollider>();
                var bounds = parts[0].bounds;
                foreach (var part in parts) bounds.Encapsulate(part.bounds);
                grid.RegisterCounter(bounds);
            }
        }
    }
}
