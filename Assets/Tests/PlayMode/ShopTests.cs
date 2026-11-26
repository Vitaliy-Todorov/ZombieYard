// Ответственности
// - Проверяет доступность и устройство подземного магазина.
// - Проверяет выдачу, списание очков и остаток товара при покупке и перезапуске.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class ShopTests
    {
        private GameLifetimeScope scene;
        private Keyboard keys;
        private YardPlayer Player => scene.player;
#if UNITY_EDITOR
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldRouting;
#endif

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return LoadYard();
#if UNITY_EDITOR
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keys = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadYard()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
        }

        [TearDown]
        public void RestoreInput()
        {
            if (keys != null) InputSystem.RemoveDevice(keys);
#if UNITY_EDITOR
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldRouting;
#endif
        }

        private IEnumerator AtCounter(int x)
        {
            var grid = scene.grid;
            var body = Player.GetComponent<CharacterController>();
            body.enabled = false;
            Player.transform.SetPositionAndRotation(grid.transform.position + ((Vector3)grid.Shop.Cell(x, 0) + Vector3.forward * .5f) * grid.Size
                + Vector3.up * .015f, Quaternion.identity);
            body.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.IsNotNull(Player.AimedStand, "The counter must be reachable through the player camera.");
        }

        private IEnumerator PressBuy()
        {
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator DiggingUnderYardOpensFourBlockRoomOnBedrock()
        {
            var grid = scene.grid;
            var column = grid.Shop.Center + new Vector2Int(0, -2);
            Vector3 top = grid.Center(column);
            float size = grid.Size;
            float floor = grid.transform.position.y + grid.ShopFloorLevel * size;
            var fog = Object.FindAnyObjectByType<ExteriorFog>();
            var inside = new Vector3(top.x, floor + size, top.z);
            Assert.IsFalse(fog.Contains(inside), "The sealed shop starts dry.");
            Assert.IsTrue(Physics.Raycast(inside, Vector3.up, out var roof));
            Assert.AreEqual(floor + 4 * size, roof.point.y, .01f);
            Assert.IsTrue(Physics.Raycast(inside, Vector3.down, out var baseHit));
            Assert.AreEqual(floor, baseHit.point.y, .01f);
            Assert.IsNull(YardChunk.ResolveHit(baseHit));
            for (int i = 0; i < YardGrid.YardLevel - grid.ShopFloorLevel - ShopLayout.Height; i++)
            {
                Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var hit));
                var block = YardChunk.ResolveHit(hit);
                Assert.IsNotNull(block);
                block.Damage(block.Current);
            }
            Assert.AreEqual(floor, grid.Height(column), .01f);
            Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var bottom));
            Assert.AreEqual(floor, bottom.point.y, .01f);
            Assert.AreEqual(5, Object.FindObjectsByType<ShopStand>(FindObjectsSortMode.None).Length);
            yield return AtCounter(0);
            yield return new WaitForSeconds(scene.world.fogInterval + .1f);
            Assert.IsFalse(fog.Exposed);
            Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
        }

        [UnityTest]
        public IEnumerator MovingShopMovesRoomAndSaleTogether()
        {
            var world = Object.Instantiate(scene.world);
            var original = scene.grid.Shop.Center;
            world.shopCenterX = original.x <= 0 ? 5 : -5;
            world.shopCenterZ = original.y + 1;
            try
            {
                yield return YardTestScene.Load(scope => { scene = scope; scene.world = world; });
                var grid = scene.grid;
                Assert.AreEqual(world.ShopCenter, grid.Shop.Center);
                var oldCell = new Vector3Int(original.x, grid.ShopFloorLevel + 1, original.y);
                Assert.IsNotNull(grid.GetOrCreateBlockHealth(oldCell), "Previous room must contain terrain.");
                Assert.IsNull(grid.GetOrCreateBlockHealth(grid.Shop.Cell(0, 0) + Vector3Int.up));
                yield return AtCounter(-2);
                var deal = world.shells;
                for (int i = 0; i < deal.terms.price; i++) Player.Score.RecordKill(ZombieKind.Normal);
                int before = Player.Gear.Stock(deal.pack.ammo);
                yield return PressBuy();
                Assert.AreEqual(before + deal.pack.count, Player.Gear.Stock(deal.pack.ammo));
            }
            finally { Object.Destroy(world); }
        }

        [UnityTest]
        public IEnumerator BothAmmoPacksReadPriceQuantityAndStockOnNewAttempt()
        {
            var world = Object.Instantiate(scene.world);
            var deal = world.shells;
            deal.terms.price = 2;
            deal.terms.stock = 1;
            deal.pack.count = 9;
            try
            {
                world.shells = deal;
                yield return YardTestScene.Load(scope => { scene = scope; scope.world = world; });
                yield return AtCounter(-2);
                var stand = Player.AimedStand;
                Assert.AreEqual(2, stand.Price);
                int before = Player.Gear.Stock(deal.pack.ammo);
                yield return PressBuy();
                Assert.AreEqual(before, Player.Gear.Stock(deal.pack.ammo));
                Assert.AreEqual(1, stand.Remaining);
                Player.Score.RecordKill(ZombieKind.Red);
                yield return PressBuy();
                Assert.AreEqual(before + 9, Player.Gear.Stock(deal.pack.ammo));
                Assert.AreEqual(0, Player.Score.Balance);
                Assert.AreEqual(0, stand.Remaining);
                var rifle = world.rifleRounds;
                for (int i = 0; i < rifle.terms.price; i++) Player.Score.RecordKill(ZombieKind.Normal);
                yield return PressBuy();
                Assert.AreEqual(before + 9, Player.Gear.Stock(deal.pack.ammo));
                Assert.AreEqual(rifle.terms.price, Player.Score.Balance);
                yield return AtCounter(-1);
                before = Player.Gear.Stock(rifle.pack.ammo);
                yield return PressBuy();
                Assert.AreEqual(before + rifle.pack.count, Player.Gear.Stock(rifle.pack.ammo));
                Assert.AreEqual(0, Player.Score.Balance);
                yield return YardTestScene.Load(scope => { scene = scope; scope.world = world; });
                yield return AtCounter(-2);
                Assert.AreEqual(1, Player.AimedStand.Remaining);
                Assert.AreEqual(0, Player.Kills);
            }
            finally { Object.Destroy(world); }
        }

        [UnityTest]
        public IEnumerator FullReserveLeavesBothPickupAndSaleUntouched()
        {
            var deal = scene.world.shells;
            var gear = Player.Gear;
            int space = int.MaxValue - gear.Stock(deal.pack.ammo);
            if (space > 0) Assert.IsTrue(gear.Add(deal.pack.ammo, space));
            var pickup = Pickup.ForAmmo(deal.pack.ammo, deal.pack.count, Player.transform.position, Player.VoidHeight);
            Assert.IsFalse(pickup.Collect(gear));
            Assert.IsTrue(pickup.gameObject.activeSelf);
            yield return AtCounter(-2);
            var stand = Player.AimedStand;
            for (int i = 0; i < stand.Price; i++) Player.Score.RecordKill(ZombieKind.Normal);
            int balance = Player.Score.Balance;
            int stock = stand.Remaining;
            yield return PressBuy();
            Assert.AreEqual(int.MaxValue, gear.Stock(deal.pack.ammo));
            Assert.AreEqual(balance, Player.Score.Balance);
            Assert.AreEqual(stock, stand.Remaining);
        }

        [UnityTest]
        public IEnumerator MedkitsAndStaffChargeOnlyWhenReceived()
        {
            var world = scene.world;
            int funds = world.staff.price + world.partialMedkit.price + world.fullMedkit.price;
            for (int i = 0; i < funds; i++) Player.Score.RecordKill(ZombieKind.Normal);
            yield return AtCounter(0);
            yield return PressBuy();
            Assert.AreEqual(funds, Player.Score.Balance);
            Assert.AreEqual(world.partialMedkit.stock, Player.AimedStand.Remaining);
            Player.Health.Damage(Player.Health.Maximum - 1);
            yield return PressBuy();
            Assert.AreEqual(1 + Player.config.partialHealing, Player.Health.Current);
            Assert.AreEqual(funds - world.partialMedkit.price, Player.Score.Balance);
            yield return AtCounter(1);
            yield return PressBuy();
            Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
            Assert.AreEqual(world.staff.price, Player.Score.Balance);
            yield return AtCounter(2);
            yield return PressBuy();
            Assert.AreEqual(world.staff.price, Player.Score.Balance);
            Assert.AreEqual(world.staff.stock, Player.AimedStand.Remaining);
            Player.Gear.Drop();
            yield return PressBuy();
            Assert.AreSame(scene.staff, Player.Gear.Active.Config);
            Assert.AreEqual(0, Player.Score.Balance);
            Assert.AreEqual(world.staff.stock - 1, Player.AimedStand.Remaining);
            Assert.AreEqual(funds, Player.Score.Normal);
        }
    }
}
