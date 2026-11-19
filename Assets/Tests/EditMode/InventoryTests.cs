using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieYard.Tests
{
    public sealed class InventoryTests
    {
        [Test]
        public void ReloadConsumesReserveOnlyOnCompletionAndSwitchCancels()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>("Assets/General/Configs/Player/Survivor.asset");
            var inventory = new Inventory(config);
            inventory.Select(1);
            var shotgun = inventory.Active;
            var ammo = shotgun.Ammo;
            int initial = inventory.Stock(ammo);
            shotgun.Fire();
            inventory.StartReload(0);
            Assert.AreEqual(2, inventory.Slots.Length);
            inventory.Select(0);
            inventory.Tick(10);
            Assert.AreSame(config.startingWeapons[0], inventory.Active.Config);
            Assert.AreEqual(initial, inventory.Stock(ammo));
            Assert.AreEqual(2, shotgun.Loaded);
            inventory.Select(1);
            inventory.StartReload(11);
            inventory.Tick(11 + shotgun.Config.reloadSeconds);
            Assert.AreEqual(3, shotgun.Loaded);
            Assert.AreEqual(initial - 1, inventory.Stock(ammo));
            Assert.AreSame(shotgun, inventory.Drop());
            Assert.AreSame(config.fists, inventory.Active.Config);
        }

        [Test]
        public void LethalDamageRaisesDeathOnlyOnce()
        {
            var obj = new GameObject();
            try
            {
                var health = obj.AddComponent<Health>();
                health.Initialize(65);
                int deaths = 0;
                health.Died += () => deaths++;
                for (int i = 0; i < 6; i++) health.Damage(12);
                health.Damage(100);
                Assert.AreEqual(0, health.Current);
                Assert.AreEqual(1, deaths);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
