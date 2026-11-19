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
        public IEnumerator NewAttemptReadsUpdatedWorldBalanceAndRestoresGround()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var config = scene.world;
            int originalHp = config.soil.hp;
            int originalHeight = config.plateauHeight;
            Vector3 origin = Vector3.up * 2;
            try
            {
                config.soil.hp = originalHp + 17;
                config.plateauHeight = originalHeight + 1;
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var hit, 4));
                var block = Health.AtHit(hit);
                Assert.AreEqual(originalHp, block.Current);
                block.Damage(block.Current);
                Assert.Less(scene.grid.Height(Vector2Int.zero), 0);

                yield return SceneManager.LoadSceneAsync("Yard");
                yield return null;
                scene = Object.FindAnyObjectByType<GameLifetimeScope>();
                Assert.AreEqual(0, scene.grid.Height(Vector2Int.zero));
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out hit, 4));
                Assert.AreEqual(originalHp + 17, Health.AtHit(hit).Current);
                var lowland = new Vector2Int(config.halfSize.x + config.plateauHeight + 1, 0);
                Assert.AreEqual(-config.plateauHeight * scene.grid.Size, scene.grid.Height(lowland));
            }
            finally
            {
                config.soil.hp = originalHp;
                config.plateauHeight = originalHeight;
            }
        }

        [UnityTest]
        public IEnumerator DigThroughChunkSeamAndSubsoil()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var grid = scene.grid;
            var cell = new Vector2Int(-1, 2);
            Vector3 top = grid.Center(cell);
            Vector3 neighbor = top + Vector3.right * grid.Size;
            Assert.IsTrue(Physics.Raycast(neighbor + Vector3.up, Vector3.down, out var other, 2));
            var adjacent = Health.AtHit(other);
            for (int layer = 0; layer < 3; layer++)
            {
                Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var hit));
                var block = Health.AtHit(hit);
                Assert.AreEqual(scene.world.soil.hp, block.Current);
                block.Damage(block.Current);
                Assert.AreEqual(-(layer + 1) * grid.Size, grid.Height(cell));
            }
            Assert.IsTrue(Physics.Raycast(top - Vector3.up * grid.Size * .5f, Vector3.right, out var wall, grid.Size));
            Assert.AreSame(adjacent, Health.AtHit(wall));
            Assert.AreEqual(adjacent.Maximum, adjacent.Current);
        }

        [UnityTest]
        public IEnumerator LowlandFogDamagesUntilPlayerReturnsToDryGround()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var player = scene.player;
            var body = player.GetComponent<CharacterController>();
            var start = player.transform.position;
            var lowland = new Vector2Int(scene.world.halfSize.x + scene.world.plateauHeight + 1, 0);
            Vector3 outside = scene.grid.Center(lowland);
            Assert.AreEqual(-scene.world.plateauHeight * scene.grid.Size, outside.y);
            body.enabled = false;
            player.transform.position = outside + Vector3.up * .03f;
            body.enabled = true;
            yield return new WaitForSeconds(scene.world.fogInterval + .1f);
            Assert.IsTrue(player.Fog.Exposed);
            Assert.AreEqual(player.config.hp - scene.world.fogDamage, player.Health.Current);
            body.enabled = false;
            player.transform.position = start;
            body.enabled = true;
            yield return new WaitForSeconds(scene.world.fogInterval + .1f);
            Assert.IsFalse(player.Fog.Exposed);
            Assert.AreEqual(player.config.hp - scene.world.fogDamage, player.Health.Current);
        }

        [UnityTest]
        public IEnumerator MistFlowsThroughAnOpenedWallIntoAnIsolatedPit()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var grid = scene.grid;
            float size = grid.Size;
            int edge = scene.world.halfSize.x;
            var shaft = new Vector2Int(edge + 2, 4);
            var bank = new Vector2Int(edge + 4, shaft.y);
            foreach (Vector2Int direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                int boundary = direction.x == 0 ? scene.world.halfSize.y : edge;
                for (int ring = 1; ring <= 6; ring++)
                    Assert.AreEqual(-(ring - 1) * size, grid.Height(direction * (boundary + ring)));
            }
            Vector3 reservoir = grid.Center(bank);
            reservoir.y = -2 * size;
            Assert.IsFalse(scene.player.Fog.Contains(reservoir + .01f * Vector3.up));
            Assert.IsTrue(scene.player.Fog.Contains(reservoir - .01f * Vector3.up));

            Vector3 pit = grid.Center(shaft);
            for (int y = 0; y < 4; y++)
            {
                Assert.IsTrue(Physics.Raycast(pit + Vector3.up, Vector3.down, out var ground));
                Health.AtHit(ground).Damage(int.MaxValue);
            }
            Vector3 channel = new Vector3(bank.x, -2.5f, bank.y) * size;
            Vector3 inlet = new Vector3(shaft.x, -2.5f, shaft.y) * size;
            Vector3 floor = inlet + Vector3.down * 2 * size;
            yield return new WaitForSeconds(scene.world.fogFlowInterval * 2);
            Assert.IsFalse(scene.player.Fog.Contains(floor));
            Assert.IsTrue(Physics.Raycast(channel, Vector3.left, out var wall, size));
            var fog = scene.player.Fog;
            // Only this neighborhood can exchange volume during the first four flow ticks.
            float LocalTotal()
            {
                float total = 0;
                for (int x = shaft.x - 12; x <= shaft.x + 12; x++)
                for (int z = shaft.y - 12; z <= shaft.y + 12; z++)
                for (int y = -6; y <= -3; y++)
                    total += Mathf.RoundToInt(fog.FillAt(new Vector3Int(x,y,z)) * 255);
                return total;
            }
            float before = LocalTotal();
            var donor = new Vector3Int(bank.x, -3, bank.y);
            Assert.AreEqual(1, fog.FillAt(donor));
            Health.AtHit(wall).Damage(int.MaxValue);
            Assert.IsFalse(fog.Contains(inlet));
            bool receded = false;
            float until = Time.time + scene.world.fogFlowInterval * 4;
            do
            {
                yield return null;
                receded |= fog.FillAt(donor) < 1;
            } while (Time.time < until);
            Assert.IsTrue(receded, "Original fog cell should lose volume.");
            Assert.AreEqual(before, LocalTotal());
            yield return new WaitForSeconds(scene.world.fogFlowInterval * 8);
            Assert.Greater(fog.FillAt(new Vector3Int(shaft.x, -5, shaft.y)), 0);
            Assert.IsTrue(fog.Contains(floor - Vector3.up * .49f * size));
            Assert.IsFalse(scene.player.Fog.Contains(inlet + Vector3.up * size));

            foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                int yardEdge = direction.x == 0 ? scene.world.halfSize.y : edge;
                // Between the foothills and the yard only lowland and its trees are visible.
                for (int i = 10; i < scene.world.mountainDistance - 16; i++)
                    Assert.LessOrEqual(grid.Height(direction * (yardEdge + i)), 2 * size);
                float peak = float.NegativeInfinity;
                for (int i = 10; i <= scene.world.mountainDistance + 8; i++)
                    peak = Mathf.Max(peak, grid.Height(direction * (yardEdge + i)));
                Assert.Greater(peak, 3 * size, $"Mountain skyline missing in direction {direction}");
            }
        }

        [UnityTest]
        public IEnumerator DropAndRetrieveSelectedWeaponUsingGameControls()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var player = Object.FindAnyObjectByType<YardPlayer>();
            var gear = player.Gear;
            gear.Select(1);
            var shotgun = gear.Active;
            shotgun.Fire();
            gear.StartReload(Time.time);
#if UNITY_EDITOR
            // The Test Runner can run with an unfocused Game View.
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keys = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.G));
                yield return null;
                yield return null;
                Assert.AreSame(player.config.fists, gear.Active.Config, $"G={keys.gKey.isPressed}, device={keys.enabled}, notice={player.Notice}, focus={Application.isFocused}");
                Assert.IsFalse(gear.Reloading);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return null;
                Assert.IsNotNull(player.Nearby);
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Assert.AreSame(shotgun, gear.Active);
                Assert.AreEqual(shotgun.Config.capacity - 1, shotgun.Loaded);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
                InputSystem.settings.backgroundBehavior = previousBackground;
#endif
            }
        }

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

            var source = new Vector2Int(-4, 2);
            var target = scene.grid.Cell(player.transform.position);
            Assert.IsTrue(scene.grid.NextStep(source, target, scene.spawn.zombie.maxClimbHeight, out var next));
            var direction = (scene.grid.Center(next) - scene.grid.Center(source)).normalized;
            var zombie = Object.Instantiate(scene.spawn.prefab, scene.grid.Center(source), Quaternion.LookRotation(-direction));
            zombie.Initialize(scene.spawn.zombie, scene.grid, player, _ => { });
            yield return new WaitForSeconds(.42f);
            Assert.Greater(Vector3.Dot(zombie.transform.forward, direction), .95f);
            Assert.Greater(Vector3.Dot(zombie.transform.position - scene.grid.Center(source), direction), .1f);
        }

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
            Assert.IsFalse(pickup.Collect(gear));
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
            gear.Drop();
            Assert.IsTrue(pickup.Collect(gear));
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
            var block = Health.AtHit(ground);
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
            Assert.AreEqual(2, gear.Slots.Length);
            Assert.IsFalse(weapon.Collect(gear));
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
