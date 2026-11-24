// Ответственности
// - Проверяет погоню в крытых проходах, на развилках и между этажами.
// - Проверяет реакцию ходока на раскопку, препятствия и предельную высоту подъёма.

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ZombieYard.Tests
{
    public sealed class CavePursuitTests
    {
        private GameLifetimeScope scene;
        private ZombieAgent walker;
        private readonly HashSet<Vector3Int> excavated = new();
        private float oldTimeScale;
        private float Size => scene.grid.Size;
        // Cave layouts use coordinates relative to the yard surface.
        private float WorldY(int level) => (YardGrid.YardLevel + level) * Size;
        private Vector3 WorldPosition(Vector3Int cell) => (Vector3)(cell + YardGrid.YardLevel * Vector3Int.up) * Size;
        private int PlayerHp => scene.player.Health.Current;
        private Vector2Int WalkerColumn => scene.grid.Cell(walker.transform.position);

        [UnitySetUp]
        public IEnumerator LoadYard()
        {
            oldTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            excavated.Clear();
            Time.timeScale = 2;
        }

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = oldTimeScale;

        // Carve through the real chunk collider and damage the same block a shovel would hit.
        private void Dig(Vector3Int from, Vector3Int direction)
        {
            if (!excavated.Add(from + direction)) return;
            Vector3 origin = WorldPosition(from) + .5f * Size * Vector3.up;
            Assert.IsTrue(Physics.Raycast(origin, direction, out var hit, Size), $"No face at {from + direction}");
            var block = YardChunk.ResolveHit(hit);
            Assert.IsNotNull(block);
            block.Damage(block.Current);
        }

        private void Shaft(Vector3Int floor)
        {
            for (int y = 0; y > floor.y; y--)
                Dig(new Vector3Int(floor.x, y, floor.z), Vector3Int.down);
        }

        private void Tunnel(Vector3Int from, Vector3Int direction, int length, int height = 2)
        {
            for (int i = 0; i < length; i++)
            {
                Dig(from, direction);
                from += direction;
                for (int y = 1; y < height; y++)
                    Dig(from + Vector3Int.up * (y - 1), Vector3Int.up);
            }
        }

        private void MovePlayer(Vector3Int feet)
        {
            var body = scene.player.GetComponent<CharacterController>();
            body.enabled = false;
            scene.player.transform.position = WorldPosition(feet) + .03f * Vector3.up;
            body.enabled = true;
        }

        private void SpawnWalker(Vector3Int feet, ZombieConfig settings = null)
        {
            walker = Object.Instantiate(scene.spawn.prefab, WorldPosition(feet), Quaternion.identity);
            walker.Initialize(settings ? settings : scene.spawn.zombie, scene.grid, scene.player, _ => { });
        }

        private IEnumerator UntilHit(Action observe, float timeout = 25)
        {
            int hp = PlayerHp;
            float until = Time.time + timeout;
            while (PlayerHp == hp && Time.time < until)
            {
                yield return null;
                observe();
            }
            Assert.AreEqual(hp - scene.spawn.zombie.weapon.melee.damage, PlayerHp,
                $"Walker at {walker.transform.position}, player at {scene.player.transform.position}");
        }

        [UnityTest]
        public IEnumerator FallenWalkerPursuesPlayerAroundCoveredCaveBend()
        {
            var shaft = new Vector3Int(-3, -4, 3);
            Shaft(shaft);
            Tunnel(shaft, Vector3Int.right, 1, 1);
            Tunnel(shaft + Vector3Int.right, Vector3Int.right, 3);
            var bend = shaft + 4 * Vector3Int.right;
            Tunnel(bend, Vector3Int.forward, 3);
            MovePlayer(bend + 3 * Vector3Int.forward);
            SpawnWalker(new Vector3Int(shaft.x, 0, shaft.z));
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(WorldY(shaft.y), walker.transform.position.y, .04f, "Walker must land on the shaft floor.");
            Assert.AreEqual(new Vector2Int(shaft.x, shaft.z), WalkerColumn, "Low doorway must block the walker.");
            Assert.AreEqual(scene.player.Health.Maximum, PlayerHp);

            Dig(shaft + Vector3Int.right, Vector3Int.up);
            Assert.AreEqual(WorldY(0), scene.grid.Height(new Vector2Int(shaft.x + 1, shaft.z)), "Roof stays intact.");
            bool roundedBend = false;
            yield return UntilHit(() =>
            {
                Assert.AreEqual(WorldY(shaft.y), walker.transform.position.y, .04f, "Walker left the cave floor.");
                Assert.IsTrue(WalkerColumn.y == shaft.z || WalkerColumn.x == bend.x, "Walker crossed the solid corner.");
                roundedBend |= WalkerColumn.x == bend.x && WalkerColumn.y > shaft.z;
            });
            Assert.IsTrue(roundedBend);
        }

        [UnityTest]
        public IEnumerator WalkerLeavesDetourWhenShortTunnelOpens()
        {
            var shaft = new Vector3Int(-4, -4, -3);
            var fork = shaft + Vector3Int.right;
            var doorway = shaft + 4 * Vector3Int.right;
            Shaft(shaft);
            Tunnel(shaft, Vector3Int.right, 3);
            Tunnel(doorway + Vector3Int.left, Vector3Int.right, 1, 1);
            Tunnel(doorway, Vector3Int.right, 4);
            Tunnel(fork, Vector3Int.forward, 3);
            Tunnel(fork + 3 * Vector3Int.forward, Vector3Int.right, 6);
            Tunnel(fork + new Vector3Int(6, 0, 3), Vector3Int.back, 3);
            MovePlayer(doorway + 4 * Vector3Int.right);
            SpawnWalker(shaft);

            float until = Time.time + 6;
            while (WalkerColumn.y == shaft.z && Time.time < until) yield return null;
            Assert.AreEqual(new Vector2Int(fork.x, shaft.z + 1), WalkerColumn, "Closed shortcut requires a detour.");
            Dig(doorway, Vector3Int.up);
            bool tookShortcut = false;
            yield return UntilHit(() =>
            {
                Assert.AreNotEqual(0, WalkerColumn.y, "Walker continued along the long branch.");
                tookShortcut |= WalkerColumn == new Vector2Int(doorway.x, doorway.z);
            });
            Assert.IsTrue(tookShortcut);
        }

        [UnityTest]
        public IEnumerator WalkerTracksPlayerBetweenStackedFloorsUsingCoveredStairs()
        {
            var shaft = new Vector3Int(-3, -6, 1);
            var lower = new Vector3Int(0, -6, 1);
            var upper = lower + Vector3Int.up * 3;
            Shaft(shaft);
            Tunnel(shaft, Vector3Int.right, 6);
            var stair = shaft + Vector3Int.right * 6;
            for (int i = 1; i <= 3; i++)
            {
                stair += Vector3Int.up;
                Tunnel(stair, Vector3Int.forward, 1, i == 3 ? 2 : 3);
                stair += Vector3Int.forward;
            }
            Tunnel(stair, Vector3Int.left, 3);
            Tunnel(stair + Vector3Int.left * 3, Vector3Int.back, 3);
            MovePlayer(lower);
            SpawnWalker(shaft);
            yield return UntilHit(() => Assert.AreEqual(WorldY(lower.y), walker.transform.position.y, .04f));

            MovePlayer(upper);
            Vector3 stopped = walker.transform.position;
            yield return new WaitForSeconds(2);
            Assert.AreEqual(stopped, walker.transform.position, "Low ceiling above the first riser blocks ascent.");
            Dig(new Vector3Int(3, -5, 1), Vector3Int.up);

            bool usedStairs = false;
            yield return UntilHit(() => usedStairs |= WalkerColumn == new Vector2Int(3, 3));
            Assert.IsTrue(usedStairs, "Walker must take the stairs to the upper gallery.");
            Assert.AreEqual(WorldY(upper.y), walker.transform.position.y, .04f);
            MovePlayer(lower);
            usedStairs = false;
            yield return UntilHit(() => usedStairs |= WalkerColumn == new Vector2Int(3, 3));
            Assert.IsTrue(usedStairs, "A vertical-only target change must send the walker back downstairs.");
            Assert.AreEqual(WorldY(lower.y), walker.transform.position.y, .04f);
        }

        [UnityTest]
        public IEnumerator SealedRoomBlocksPursuitAndMeleeUntilWallIsDug()
        {
            var shaft = new Vector3Int(-3, -4, 2);
            var wall = new Vector3Int(1, -4, 2);
            var room = wall + Vector3Int.right;
            Shaft(shaft);
            Tunnel(shaft, Vector3Int.right, 3);

            // Seed the first air voxel directly to keep all six sides of this chamber sealed.
            var seed = scene.grid.GetOrCreateBlockHealth(room + YardGrid.YardLevel * Vector3Int.up);
            seed.Damage(seed.Current);
            Dig(room, Vector3Int.up);
            Tunnel(room, Vector3Int.right, 2);
            MovePlayer(room);
            var settings = Object.Instantiate(scene.spawn.zombie);
            var weapon = Object.Instantiate(settings.weapon);
            var melee = Object.Instantiate(weapon.melee);
            try
            {
                settings.weapon = weapon;
                weapon.melee = melee;
                melee.range = 2.5f * Size; // Put the player within melee range through a full block.
                SpawnWalker(wall + Vector3Int.left, settings);
                Vector3 start = walker.transform.position;
                Assert.Less(Vector3.Distance(start, scene.player.transform.position), melee.range);
                Assert.IsTrue(Physics.Linecast(start + Vector3.up, scene.player.transform.position + Vector3.up,
                    out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
                Assert.IsNotNull(hit.collider.GetComponent<YardChunk>());

                yield return new WaitForSeconds(2);
                Assert.AreEqual(start, walker.transform.position);
                Assert.AreEqual(scene.player.Health.Maximum, PlayerHp, "Melee must not pass through a solid wall.");
                MovePlayer(room + 2 * Vector3Int.right);
                yield return new WaitForSeconds(1);
                Assert.AreEqual(start, walker.transform.position, "Moving inside a sealed room cannot create a route.");
                Assert.AreEqual(scene.player.Health.Maximum, PlayerHp);

                Tunnel(wall + Vector3Int.left, Vector3Int.right, 1);
                bool enteredDoorway = false;
                yield return UntilHit(() => enteredDoorway |= WalkerColumn.x >= wall.x);
                Assert.IsTrue(enteredDoorway, "The waiting walker must resume pursuit through the new doorway.");
            }
            finally
            {
                Object.Destroy(melee);
                Object.Destroy(weapon);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator WalkerGoesAroundShopCountersToReachPlayer()
        {
            var shop = scene.grid.Shop;
            for (int x = -3; x <= 3; x++)
            for (int y = 0; y < 2; y++)
            {
                var block = scene.grid.GetOrCreateBlockHealth(shop.Cell(x, 3) + y * Vector3Int.up);
                block.Damage(block.Current);
            }
            MovePlayer(shop.Cell(0, 0) - YardGrid.YardLevel * Vector3Int.up);
            SpawnWalker(shop.Cell(0, 3) - YardGrid.YardLevel * Vector3Int.up);
            var body = walker.GetComponent<CapsuleCollider>();
            var colliders = new List<BoxCollider>();
            foreach (var stand in Object.FindObjectsByType<ShopStand>(FindObjectsSortMode.None))
                colliders.AddRange(stand.GetComponentsInChildren<BoxCollider>());
            Assert.IsNotEmpty(colliders);
            bool roundedEnd = false;
            yield return UntilHit(() =>
            {
                roundedEnd |= Mathf.Abs(WalkerColumn.x - shop.Center.x) == 3 && WalkerColumn.y == shop.Center.y + 2;
                foreach (var part in colliders)
                    Assert.IsFalse(Physics.ComputePenetration(body, walker.transform.position, walker.transform.rotation,
                        part, part.transform.position, part.transform.rotation, out _, out float depth) && depth > .005f,
                        $"Walker entered {part.name} at {walker.transform.position}");
            });
            Assert.IsTrue(roundedEnd, "The walker must pass around one end of the counter row.");
        }

        [UnityTest]
        public IEnumerator WalkerEscapesToSurfaceAtConfiguredClimbThreshold()
        {
            var shaft = new Vector3Int(-3, -3, -2);
            Shaft(shaft);
            Tunnel(shaft, Vector3Int.right, 4);
            var foot = shaft + 4 * Vector3Int.right;
            Dig(foot + Vector3Int.up, Vector3Int.up);
            Tunnel(foot + Vector3Int.up, Vector3Int.right, 1);
            Dig(new Vector3Int(3, 0, shaft.z), Vector3Int.down);
            MovePlayer(new Vector3Int(0, 0, shaft.z)); // Directly above the covered tunnel.
            var settings = Object.Instantiate(scene.spawn.zombie);
            try
            {
                settings.maxClimbHeight = Size - .01f;
                SpawnWalker(shaft, settings);
                yield return new WaitForSeconds(3);
                Assert.AreEqual(WorldPosition(shaft), walker.transform.position);
                Assert.AreEqual(scene.player.Health.Maximum, PlayerHp);
                Object.Destroy(walker.gameObject);
                yield return null;

                settings.maxClimbHeight = Size;
                SpawnWalker(shaft, settings);
                bool passedFirstStep = false, surfaced = false;
                yield return UntilHit(() =>
                {
                    passedFirstStep |= WalkerColumn.x == 2 && Mathf.Abs(walker.transform.position.y - WorldY(-2)) < .04f;
                    surfaced |= walker.transform.position.y >= WorldY(0) - .04f;
                });
                Assert.IsTrue(passedFirstStep, "Climb exactly equal to the configured limit is allowed.");
                Assert.IsTrue(surfaced, "Walker must emerge and reach the player above the tunnel.");
            }
            finally
            {
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator WalkerDropsTwoBlocksAndReturnsOnlyAfterOneBlockStepIsDug()
        {
            var high = new Vector3Int(-3, -3, -3);
            var lip = new Vector3Int(0, -3, -3);
            var landing = new Vector3Int(1, -5, -3);
            Shaft(high);
            Tunnel(high, Vector3Int.right, 4);
            Dig(landing + 2 * Vector3Int.up, Vector3Int.down);
            Dig(landing + Vector3Int.up, Vector3Int.down);
            Tunnel(landing, Vector3Int.right, 3);
            Assert.That(scene.spawn.zombie.maxClimbHeight, Is.InRange(Size, 2 * Size - .001f));
            MovePlayer(landing + 3 * Vector3Int.right);
            SpawnWalker(high);
            bool dropped = false;
            yield return UntilHit(() =>
            {
                if (walker.transform.position.y < WorldY(high.y) - .04f)
                    Assert.GreaterOrEqual(WalkerColumn.x, landing.x, "Walker descended inside the ledge.");
                dropped |= WalkerColumn.x == landing.x && walker.transform.position.y < WorldY(high.y - 1);
            });
            Assert.IsTrue(dropped);

            MovePlayer(high);
            Vector3 stopped = walker.transform.position;
            int hp = PlayerHp;
            yield return new WaitForSeconds(2);
            Assert.AreEqual(stopped, walker.transform.position, "Two-block climb must be unreachable.");
            Assert.AreEqual(hp, PlayerHp);
            Dig(lip, Vector3Int.down);
            bool usedStep = false;
            yield return UntilHit(() => usedStep |= WalkerColumn.x == lip.x &&
                Mathf.Abs(walker.transform.position.y - WorldY(lip.y - 1)) < .04f);
            Assert.IsTrue(usedStep);
            Assert.AreEqual(WorldY(high.y), walker.transform.position.y, .04f);
        }
    }
}
