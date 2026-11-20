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
            Player.transform.SetPositionAndRotation(new Vector3(x, grid.ShopFloorLevel, .5f) * grid.Size
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
            var column = new Vector2Int(0, -2);
            Vector3 top = grid.Center(column);
            float size = grid.Size;
            float floor = (YardGrid.YardLevel - 2 - scene.world.subsoilDepth) * size;
            var inside = new Vector3(top.x, floor + size, top.z);
            Assert.IsFalse(Player.Fog.Contains(inside), "The sealed shop starts dry.");
            Assert.IsTrue(Physics.Raycast(inside, Vector3.up, out var roof));
            Assert.AreEqual(floor + 4 * size, roof.point.y, .01f);
            Assert.IsTrue(Physics.Raycast(inside, Vector3.down, out var baseHit));
            Assert.AreEqual(floor, baseHit.point.y, .01f);
            Assert.IsNull(Health.AtHit(baseHit));
            for (int i = 0; i < 2 + scene.world.subsoilDepth - 4; i++)
            {
                Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var hit));
                var block = Health.AtHit(hit);
                Assert.IsNotNull(block);
                block.Damage(block.Current);
            }
            Assert.AreEqual(floor, grid.Height(column), .01f);
            Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var bottom));
            Assert.AreEqual(floor, bottom.point.y, .01f);
            Assert.AreEqual(5, Object.FindObjectsByType<ShopStand>(FindObjectsSortMode.None).Length);
            yield return AtCounter(0);
            yield return new WaitForSeconds(scene.world.fogInterval + .1f);
            Assert.IsFalse(Player.Fog.Exposed);
            Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
        }

        [UnityTest]
        public IEnumerator BothAmmoPacksReadPriceQuantityAndStockOnNewAttempt()
        {
            var world = scene.world;
            var original = world.shells;
            var deal = original;
            deal.terms.price = 2;
            deal.terms.stock = 1;
            deal.pack.count = 9;
            try
            {
                world.shells = deal;
                yield return LoadYard();
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
                yield return LoadYard();
                yield return AtCounter(-2);
                Assert.AreEqual(1, Player.AimedStand.Remaining);
                Assert.AreEqual(0, Player.Kills);
            }
            finally { world.shells = original; }
        }

        [UnityTest]
        public IEnumerator MedkitsAndStaffChargeOnlyWhenReceived()
        {
            var world = scene.world;
            Assert.AreEqual(100, world.staff.price);
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
