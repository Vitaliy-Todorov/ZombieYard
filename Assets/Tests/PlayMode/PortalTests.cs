// Ответственности
// - Проверяет открытие подземных порталов, появление при занятом выходе и общий лимит с поверхностью.
// - Проверяет погоню, добычу и награды подземных видов зомби.

using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ZombieYard.Tests
{
    public sealed class PortalTests
    {
        private GameLifetimeScope scene;
        private ZombieSpawnerConfig spawn;
        private ZombieConfig walker;
        private float oldScale;
        private YardGrid Grid => scene.grid;
        private WeaponConfig Chainsaw => System.Array.Find(scene.spawn.zombie.loot, entry => entry.weapon).weapon;
        private YardPlayer Player => scene.player;
        private ZombiePortal[] Underground => Grid.GetComponentsInChildren<ZombiePortal>().Where(p => p.IsUnderground).ToArray();
        private int TunnelLevel => YardGrid.YardLevel - scene.world.plateauHeight - 5;

        [UnitySetUp]
        public IEnumerator LoadYard()
        {
            oldScale = Time.timeScale;
            yield return YardTestScene.Load(scope =>
            {
                scene = scope;
                spawn = Object.Instantiate(scene.spawn);
                walker = Object.Instantiate(spawn.zombie);
                scene.spawn = spawn;
                spawn.zombie = walker;
                spawn.portalChance = 1;
                spawn.undergroundMinInterval = spawn.undergroundMaxInterval = .4f;
                spawn.undergroundLimit = 1;
            });
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = oldScale;
            Object.Destroy(walker);
            Object.Destroy(spawn);
        }

        private void Dig(Vector3Int cell, bool playerHit = true)
        {
            var block = Grid.GetOrCreateBlockHealth(cell);
            Assert.IsNotNull(block, $"Expected a block at {cell}");
            block.Damage(block.Current, byPlayer: playerHit);
        }

        private void MovePlayer(Vector3 feet)
        {
            var body = Player.GetComponent<CharacterController>();
            body.enabled = false;
            Player.transform.position = feet + Vector3.up * .02f;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator PortalsRequireMinedStoneClearanceAndSpacing()
        {
            foreach (var point in scene.points)
            {
                var surface = point.GetComponentInChildren<ZombiePortal>();
                Assert.IsNotNull(surface);
                Assert.AreEqual(Grid.Height(Grid.Cell(point.position)), surface.transform.position.y);
                Assert.IsNotNull(surface.GetComponentInChildren<Light>());
                Assert.AreSame(spawn.portalMaterial, surface.GetComponentInChildren<LineRenderer>().sharedMaterial);
            }
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            var feet = new Vector3Int(0, TunnelLevel, 4);
            Dig(new Vector3Int(0, YardGrid.YardLevel - 1, 4));
            Dig(new Vector3Int(0, YardGrid.YardLevel - 2, 4));
            Dig(feet + Vector3Int.left, false);
            Dig(feet + Vector3Int.left + Vector3Int.up, false);
            Dig(feet); // The intact ceiling is too low.
            Assert.IsEmpty(Underground);

            // Finish the head opening with the player's equipped shovel.
            MovePlayer((Vector3)(feet + Vector3Int.left) * Grid.Size);
            Player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Player.eyes.transform.localRotation = Quaternion.identity;
            var ceiling = Grid.GetOrCreateBlockHealth(feet + Vector3Int.up);
            ceiling.Damage(ceiling.Maximum - Player.Gear.Active.Config.melee.damage);
            Physics.SyncTransforms();
            Player.Attack();
            Assert.AreEqual(0, ceiling.Current);
            var original = Underground.Single();
            Assert.AreEqual((Vector3)feet * Grid.Size, original.transform.position);

            spawn.portalSpacing = 2 * Grid.Size;
            var adjacent = feet + Vector3Int.right;
            Dig(adjacent + Vector3Int.up);
            Dig(adjacent);
            Assert.AreEqual(1, Underground.Length);
            var edge = adjacent + Vector3Int.right;
            Dig(edge + Vector3Int.up);
            Dig(edge);
            Assert.AreEqual(2, Underground.Length, "A portal exactly at the configured radius is allowed.");
            spawn.portalChance = 0;
            Dig(feet + Vector3Int.down);
            Dig(edge + Vector3Int.right * 2);
            Dig(edge + Vector3Int.right * 2 + Vector3Int.up);
            yield return null;
            Assert.AreEqual(2, Underground.Length);
            CollectionAssert.Contains(Underground, original);
            Assert.AreEqual((Vector3)feet * Grid.Size, original.transform.position);
        }

        [UnityTest]
        public IEnumerator SurfaceAndUndergroundShareTheLivingLimit()
        {
            Time.timeScale = 2;
            spawn.limit = 1;
            float until = Time.time + 7;
            ZombieAgent normal;
            while (!(normal = Object.FindAnyObjectByType<ZombieAgent>()) && Time.time < until) yield return null;
            Assert.IsNotNull(normal);
            Assert.AreEqual(ZombieKind.Normal, normal.Kind);
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            foreach (int x in new[] { -4, 4 })
            {
                var feet = new Vector3Int(x, TunnelLevel, 3);
                Dig(feet + Vector3Int.up);
                Dig(feet);
            }
            Assert.AreEqual(2, Underground.Length);
            yield return new WaitForSeconds(1);
            Assert.AreSame(normal, Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single());
            normal.Health.Damage(normal.Health.Current);
            Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
            yield return new WaitForSeconds(.7f);
            var replacement = Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single();
            Assert.AreNotEqual(ZombieKind.Normal, replacement.Kind);
            spawn.limit = 3;
            yield return new WaitForSeconds(1);
            Assert.AreSame(replacement, Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single(),
                "The underground limit applies across both exits even when the global limit has room.");
        }

        [UnityTest]
        public IEnumerator UndergroundPortalSpawnsWithAmmoLyingInExit()
        {
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            spawn.purpleChance = 0;
            var cell = new Vector3Int(-3, TunnelLevel, 3);
            Dig(cell + Vector3Int.up);
            Dig(cell);
            var exit = Underground.Single().transform.position;
            var ammo = Pickup.ForAmmo(scene.world.shells.pack.ammo, 1, exit + Vector3.up * .2f, Grid.VoidHeight);
            Physics.SyncTransforms();

            yield return new WaitForSeconds(spawn.undergroundMaxInterval * 2);
            var zombie = Object.FindAnyObjectByType<ZombieAgent>();
            Assert.IsNotNull(zombie, "A loose ammo pack must leave the portal usable.");
            Assert.AreEqual(ZombieKind.Red, zombie.Kind);
            Assert.AreEqual(Grid.Cell(exit), Grid.Cell(zombie.transform.position));
            Assert.IsTrue(ammo && ammo.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator UndergroundVariantsPursueDropLootAndAwardPoints()
        {
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            Time.timeScale = 2;
            walker.speed = 4;
            // Guarantee the configured drops for this attempt.
            for (int i = 0; i < walker.loot.Length; i++) walker.loot[i].chance = 1;
            spawn.purpleChance = 0;
            var start = new Vector3Int(-3, TunnelLevel, 3);
            var bend = start + 3 * Vector3Int.right;
            var end = bend + 3 * Vector3Int.forward;
            for (int i = 1; i <= 3; i++)
            {
                var first = start + i * Vector3Int.right;
                var second = bend + i * Vector3Int.forward;
                Dig(first, false);
                Dig(first + Vector3Int.up, false);
                Dig(second, false);
                Dig(second + Vector3Int.up, false);
            }
            Dig(start + Vector3Int.up);
            Dig(start);
            var portal = Underground.Single();
            MovePlayer(portal.transform.position);

            foreach (var kind in new[] { ZombieKind.Red, ZombieKind.Purple })
            {
                float until = Time.time + 3;
                ZombieAgent zombie;
                while (!(zombie = Object.FindAnyObjectByType<ZombieAgent>()) && Time.time < until) yield return null;
                Assert.IsNotNull(zombie);
                Assert.AreEqual(kind, zombie.Kind);
                Assert.AreEqual(Grid.Cell(portal.transform.position), Grid.Cell(zombie.transform.position));
                if (kind == ZombieKind.Red) Assert.AreEqual(portal.transform.position, zombie.transform.position);
                else
                {
                    yield return new WaitForSeconds(.5f);
                    Assert.AreEqual(portal.transform.position.y - Grid.Size, zombie.transform.position.y, .04f,
                        "The replacement drops from the suspended portal to the new floor.");
                }
                Assert.Greater(Grid.Height(Grid.Cell(zombie.transform.position)), zombie.transform.position.y + Grid.Size * 2);
                int strength = kind == ZombieKind.Red ? walker.redStrength : walker.purpleStrength;
                Assert.AreEqual(walker.hp * strength, zombie.Health.Current);
                var skin = zombie.GetComponentsInChildren<Renderer>().Single(part => part.name == "Head");
                var palette = new MaterialPropertyBlock();
                skin.GetPropertyBlock(palette);
                var expectedTint = kind == ZombieKind.Red ? walker.redTint : walker.purpleTint;
                Assert.Less(Vector4.Distance(expectedTint, palette.GetColor("_BaseColor")), .001f);

                MovePlayer((Vector3)end * Grid.Size);
                int hp = Player.Health.Current;
                until = Time.time + 10;
                while (Player.Health.Current == hp && Time.time < until)
                {
                    Assert.That(zombie.transform.position.y, Is.InRange(portal.transform.position.y - Grid.Size - .04f,
                        portal.transform.position.y + .04f));
                    Assert.AreEqual(1, Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Length);
                    yield return null;
                }
                Assert.AreEqual(hp - strength * walker.weapon.melee.damage, Player.Health.Current);
                spawn.purpleChance = 1;
                zombie.Health.Damage(zombie.Health.Current);
                var drops = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None)
                    .Where(item => Vector3.Distance(item.transform.position, zombie.transform.position) < 1).ToArray();
                Assert.AreEqual(2, drops.Count(item => item.IsAmmo));
                var saw = drops.Single(item => item.Title == Chainsaw.title);
                Player.Gear.Drop();
                Assert.IsTrue(saw.Collect(Player.Gear));
                Assert.AreSame(Chainsaw, Player.Gear.Active.Config);
                var medkits = drops.Where(item => item.Title == "Малая аптечка").ToArray();
                Assert.AreEqual(kind == ZombieKind.Purple ? 1 : 0, medkits.Length);
                if (medkits.Length > 0)
                {
                    int wounded = Player.Health.Current;
                    Assert.IsTrue(medkits[0].Collect(Player.Gear, Player.Health));
                    Assert.AreEqual(Math.Min(wounded + Player.config.partialHealing, Player.Health.Maximum), Player.Health.Current);
                }
                foreach (var ammo in drops.Where(item => item.IsAmmo)) ammo.Collect(Player.Gear);
                Assert.AreEqual(kind == ZombieKind.Red ? 2 : 5, Player.Score.Balance);
                MovePlayer(Grid.Center(Vector2Int.zero));
                if (kind == ZombieKind.Red)
                {
                    Dig(start + Vector3Int.down);
                    Assert.AreSame(portal, Underground.Single());
                    Assert.AreEqual((Vector3)start * Grid.Size, portal.transform.position);
                }
            }
            Assert.AreEqual(1, Player.Score.Red);
            Assert.AreEqual(1, Player.Score.Purple);
            Player.Health.Damage(Player.Health.Current);
            yield return new WaitForSeconds(.9f);
            Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
            Player.Restart();
            yield return null;
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            Assert.IsEmpty(Underground);
            Assert.AreEqual(0, Player.Score.Balance);
        }
    }
}
