using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class YardPlayTests
    {
        [UnityTest]
        public IEnumerator ShotgunRiflePickupAndBlockDestruction()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var player = scene.player;
            var gear = player.Gear;
            Assert.AreEqual(player.config.hp, player.Health.Current);
            Assert.AreSame(player.config.startingWeapons[0], gear.Active.Config);
            var pickup = Object.FindAnyObjectByType<Pickup>();
            Assert.IsTrue(pickup.Collect(gear));
            gear.Select(1);
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.transform.position = player.eyes.transform.position + player.eyes.transform.forward * 1.4f;
            var health = dummy.AddComponent<Health>();
            health.Initialize(200);
            Physics.SyncTransforms();
            player.Attack();
            Assert.AreEqual(6, Object.FindObjectsByType<ShotProjectile>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(200, health.Current);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(128, health.Current);
            Assert.AreEqual(2, gear.Active.Loaded);
            gear.Select(2);
            Assert.AreSame(scene.rifle, gear.Active.Config);
            player.Attack();
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(28, health.Current);
            Assert.AreEqual(0, gear.Active.Loaded);
            dummy.SetActive(false);
            Object.Destroy(dummy);

            gear.Select(0);
            var cell = scene.grid.Cell(player.transform.position);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = scene.grid.Center(cell) + Vector3.up * .04f;
            controller.enabled = true;
            player.eyes.transform.rotation = Quaternion.LookRotation(Vector3.down);
            player.Attack();
            Assert.IsTrue(Physics.Raycast(player.eyes.transform.position, Vector3.down, out var ground, 3));
            var block = ground.collider.GetComponent<Health>();
            Assert.AreEqual(block.Maximum - 30, block.Current);
            block.Damage(block.Current);
            Assert.AreEqual(-scene.grid.Size, scene.grid.Height(cell));
            yield return new WaitForSeconds(.65f);
            Assert.IsTrue(controller.isGrounded);
            Assert.AreEqual(-scene.grid.Size, player.transform.position.y, .1f);
            Assert.AreEqual(player.config.hp, player.Health.Current);

            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var jumpRight = new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.South);
                InputSystem.QueueStateEvent(pad, jumpRight);
                float peak = player.transform.position.y, until = Time.time + .9f;
                while (Time.time < until)
                {
                    yield return null;
                    peak = Mathf.Max(peak, player.transform.position.y);
                }
                Assert.Greater(player.transform.position.y, -.15f, $"Jump from {cell}, peak {peak}, final {player.transform.position}");
            }
            finally { InputSystem.RemoveDevice(pad); }
        }

        [UnityTest]
        public IEnumerator AmmoCollectsFromBehindWhileWeaponsWaitForInteract()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var player = Object.FindAnyObjectByType<YardPlayer>();
            var gear = player.Gear;
            var ammo = gear.Slots[1].Ammo;
            int reserve = gear.Stock(ammo);
            var behind = player.transform.position - player.transform.forward * (player.config.pickupReach + 1);
            var rounds = Pickup.ForAmmo(ammo, 4, behind + Vector3.up * .3f);
            yield return null;
            Assert.AreEqual(reserve, gear.Stock(ammo));
            var controller = player.GetComponent<CharacterController>();
            controller.Move(-player.transform.forward * 2);
            var weapon = Pickup.ForWeapon(new HeldWeapon(gear.Slots[0].Config),
                player.transform.position + player.transform.right * .65f + Vector3.up * .4f);
            yield return null;
            yield return null;
            Assert.AreEqual(reserve + 4, gear.Stock(ammo));
            Assert.IsFalse(rounds && rounds.gameObject.activeSelf);
            Assert.IsTrue(weapon.gameObject.activeSelf);
            Assert.IsNull(gear.Slots[2]);
        }

        [UnityTest]
        public IEnumerator DistantSmallTargetReceivesOnePellet()
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
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(88, health.Current);
            Assert.AreEqual(2, player.Gear.Active.Loaded);
        }

        [UnityTest]
        public IEnumerator SpawnLimitClimbLootAndDeath()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            // Override balance on this attempt only to exercise the full loop quickly.
            var spawning = Object.Instantiate(scene.spawn);
            var walker = Object.Instantiate(spawning.zombie);
            var entry = walker.loot[0];
            entry.chance = 1;
            entry.quantity = new Vector2Int(2, 2);
            walker.loot = new[] { entry };
            walker.speed = 5;
            spawning.zombie = walker;
            spawning.minInterval = spawning.maxInterval = .04f;
            spawning.limit = 1;
            scene.spawn = spawning;
            var size = scene.grid.Size;
            var point = scene.points[0];
            point.position = new Vector3(0, 0, -(scene.world.halfSize.y + 1) * size);
            scene.points = new[] { point };
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 3;
                bool climbed = false;
                float until = Time.time + 20;
                while (scene.player.Health.Current == scene.player.Health.Maximum && Time.time < until)
                {
                    var alive = Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None);
                    Assert.LessOrEqual(alive.Length, 1);
                    if (alive.Length == 1 && alive[0].transform.position.y > size * .9f) climbed = true;
                    yield return null;
                }
                Assert.IsTrue(climbed);
                Assert.Less(scene.player.Health.Current, scene.player.Health.Maximum);
                var first = Object.FindAnyObjectByType<ZombieAgent>();
                first.Health.Damage(first.Health.Maximum);
                Assert.AreEqual(1, scene.player.Kills);
                var gear = scene.player.Gear;
                int before = gear.Stock(entry.ammo);
                foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                    if (pickup.Title.StartsWith(entry.ammo.title)) Assert.IsTrue(pickup.Collect(gear));
                Assert.AreEqual(before + 2, gear.Stock(entry.ammo));
                yield return new WaitForSeconds(.35f);
                var replacement = Object.FindAnyObjectByType<ZombieAgent>();
                Assert.IsNotNull(replacement);
                scene.player.Health.Damage(scene.player.Health.Current);
                replacement.Health.Damage(replacement.Health.Current);
                yield return new WaitForSeconds(.35f);
                Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
                scene.player.Restart();
                yield return null;
                yield return null;
                var fresh = Object.FindAnyObjectByType<YardPlayer>();
                Assert.AreEqual(fresh.config.hp, fresh.Health.Current);
                Assert.AreEqual(0, fresh.Kills);
            }
            finally
            {
                Time.timeScale = oldScale;
                Object.Destroy(walker);
                Object.Destroy(spawning);
            }
        }
    }
}
