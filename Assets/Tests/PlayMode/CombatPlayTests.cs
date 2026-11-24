// Ответственности
// - Проверяет удары, полёт снарядов и разрушение блоков через игровую сцену.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class CombatPlayTests
    {
        [UnityTest]
        public IEnumerator WalkerFacesItsStepAndShovelReturnsFromForwardStroke()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var player = scene.player;
            var hand = player.hand;
            var rest = hand.localPosition;
            player.Attack();
            float peak = rest.z;
            float until = Time.time + .45f;
            while (Time.time < until)
            {
                yield return null;
                peak = Mathf.Max(peak, hand.localPosition.z);
                Assert.AreEqual(rest.y, hand.localPosition.y, .005f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0, -6, -3), hand.localRotation), 1);
            }
            Assert.Greater(peak - rest.z, .18f);
            Assert.AreEqual(rest.z, hand.localPosition.z, .01f);

            var source = scene.grid.Cell(player.transform.position) + Vector2Int.left * 4;
            var direction = Vector3.right;
            var zombie = Object.Instantiate(scene.spawn.prefab, scene.grid.Center(source), Quaternion.LookRotation(-direction));
            zombie.Initialize(scene.spawn.zombie, scene.grid, player, _ => { });
            yield return new WaitForSeconds(.42f);
            Assert.Greater(Vector3.Dot(zombie.transform.forward, direction), .95f);
            Assert.Greater(Vector3.Dot(zombie.transform.position - scene.grid.Center(source), direction), .1f);
        }

        [UnityTest]
        public IEnumerator EquippedWeaponsSpendOneRoundAndDeliverPerProjectileDamage()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var player = scene.player;
            var gear = player.Gear;
            Assert.AreEqual(player.config.hp, player.Health.Current);
            Assert.AreSame(player.config.startingWeapons[0], gear.Active.Config);
            var pickup = System.Array.Find(Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None),
                item => item.Title == scene.rifle.title);
            Assert.IsNotNull(pickup);
            Assert.IsFalse(pickup.Collect(gear));
            gear.Select(1);
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.transform.position = player.eyes.transform.position + player.eyes.transform.forward * 1.4f;
            var health = dummy.AddComponent<Health>();
            var shells = gear.Active.Ammo;
            health.Initialize(shells.damage * shells.projectiles + scene.rifle.compatibleAmmo[0].damage + 1);
            Physics.SyncTransforms();
            int loaded = gear.Active.Loaded;
            player.Attack();
            Assert.AreEqual(shells.projectiles, Object.FindObjectsByType<ShotProjectile>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(health.Maximum, health.Current);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(shells.damage * shells.projectiles, health.Maximum - health.Current);
            Assert.AreEqual(loaded - 1, gear.Active.Loaded);
            gear.Drop();
            Assert.IsTrue(pickup.Collect(gear));
            Assert.AreSame(scene.rifle, gear.Active.Config);
            int healthBeforeRifle = health.Current;
            int rifleLoaded = gear.Active.Loaded;
            player.Attack();
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(gear.Active.Ammo.damage, healthBeforeRifle - health.Current);
            Assert.AreEqual(rifleLoaded - 1, gear.Active.Loaded);
            dummy.SetActive(false);
            Object.Destroy(dummy);
        }

        [UnityTest]
        public IEnumerator PelletReachesDistantTargetAfterShooterDies()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var player = Object.FindAnyObjectByType<YardPlayer>();
            player.Gear.Select(1);
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.transform.position = player.eyes.transform.position + player.eyes.transform.forward * 9;
            dummy.transform.localScale = Vector3.one * .45f;
            var health = dummy.AddComponent<Health>();
            health.Initialize(100);
            Physics.SyncTransforms();
            player.Attack();
            player.Health.Damage(player.Health.Current);
            float deadline = Time.time + player.Gear.Active.Ammo.range / player.Gear.Active.Ammo.speed + .1f;
            while (health.Current == health.Maximum && Time.time < deadline) yield return null;
            Assert.AreEqual(health.Maximum - player.Gear.Active.Ammo.damage, health.Current);
            Assert.AreEqual(player.Gear.Active.Config.capacity - 1, player.Gear.Active.Loaded);
        }
    }
}
