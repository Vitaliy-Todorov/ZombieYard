// Ответственности
// - Проверяет приём патронов, расход запаса при перезарядке и отмену при смене слота.
// - Проверяет предел лечения и однократное событие смерти.

using NUnit.Framework;
using UnityEngine;

namespace ZombieYard.Tests
{
    public sealed class InventoryTests
    {
        [Test]
        public void ReloadConsumesReserveOnlyOnCompletionAndSwitchCancels()
        {
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
            var fists = ScriptableObject.CreateInstance<WeaponConfig>();
            var punch = ScriptableObject.CreateInstance<AttackConfig>();
            var ammo = ScriptableObject.CreateInstance<AmmoConfig>();
            try
            {
                fists.melee = punch;
                weapon.capacity = 5;
                weapon.compatibleAmmo = new[] { ammo };
                config.fists = fists;
                config.startingWeapons = new[] { fists, weapon };
                config.startingReserve = new[] { new AmmoStack { ammo = ammo, count = 8 } };
                var inventory = new Inventory(config);
                inventory.Select(1);
                var shotgun = inventory.Active;
                int initial = inventory.Stock(ammo);
                shotgun.Fire();
                inventory.StartReload(0);
                Assert.AreEqual(2, inventory.Slots.Length);
                inventory.Select(0);
                inventory.Tick(10);
                Assert.AreSame(config.startingWeapons[0], inventory.Active.Config);
                Assert.AreEqual(initial, inventory.Stock(ammo));
                Assert.AreEqual(weapon.capacity - 1, shotgun.Loaded);
                inventory.Select(1);
                inventory.StartReload(11);
                inventory.Tick(11 + shotgun.Config.reloadSeconds);
                Assert.AreEqual(weapon.capacity, shotgun.Loaded);
                Assert.AreEqual(initial - 1, inventory.Stock(ammo));
                Assert.AreSame(shotgun, inventory.Drop());
                Assert.AreSame(config.fists, inventory.Active.Config);
                Assert.IsFalse(inventory.Add(ammo, int.MaxValue));
                Assert.AreEqual(initial - 1, inventory.Stock(ammo));
            }
            finally
            {
                foreach (var asset in new ScriptableObject[] { config, weapon, fists, punch, ammo })
                    Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void HealingCapsAtMaximumAndCannotRevive()
        {
            var obj = new GameObject();
            try
            {
                var health = obj.AddComponent<Health>();
                health.Initialize(100);
                Assert.IsFalse(health.Heal(35));
                health.Damage(80);
                Assert.IsFalse(health.Heal(-1));
                Assert.IsTrue(health.Heal(35));
                Assert.AreEqual(55, health.Current);
                Assert.IsTrue(health.Heal(int.MaxValue));
                Assert.AreEqual(100, health.Current);
                health.Damage(100);
                Assert.IsFalse(health.Heal(int.MaxValue));
                Assert.AreEqual(0, health.Current);
            }
            finally { Object.DestroyImmediate(obj); }
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
