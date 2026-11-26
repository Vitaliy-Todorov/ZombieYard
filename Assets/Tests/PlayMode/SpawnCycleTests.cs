// Ответственности
// - Проверяет учёт спавна при ошибке конфига, выпадение добычи и сброс попытки.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ZombieYard.Tests
{
    public sealed class SpawnCycleTests
    {
        [UnityTest]
        public IEnumerator InvalidLootRowLeavesSpawnCountAndSceneEmpty()
        {
            GameLifetimeScope scene = null;
            ZombieSpawnerConfig spawn = null;
            ZombieConfig walker = null;
            try
            {
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Invalid walker: loot row 2"));
                yield return YardTestScene.Load(scope =>
                {
                    scene = scope;
                    spawn = Object.Instantiate(scope.spawn);
                    walker = Object.Instantiate(spawn.zombie);
                    walker.name = "Invalid walker";
                    walker.loot = new[] { walker.loot[0], new LootEntry() };
                    spawn.zombie = walker;
                    spawn.minInterval = spawn.maxInterval = .1f;
                    scope.spawn = spawn;
                });
                yield return new WaitForSeconds(.3f);
                Assert.AreEqual(0, scene.Container.Resolve<ZombieSpawnService>().Alive);
                Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
            }
            finally
            {
                Object.Destroy(walker);
                Object.Destroy(spawn);
            }
        }

        [UnityTest]
        public IEnumerator SpawnLimitClimbLootAndDeath()
        {
            GameLifetimeScope scene = null;
            ZombieSpawnerConfig spawning = null;
            ZombieConfig walker = null;
            LootEntry entry = default;
            yield return YardTestScene.Load(scope =>
            {
                scene = scope;
                // Override balance on this attempt only to exercise the full loop quickly.
                spawning = Object.Instantiate(scene.spawn);
                walker = Object.Instantiate(spawning.zombie);
                entry = walker.loot[0];
                entry.chance = 1;
                entry.quantity = new Vector2Int(2, 2);
                walker.loot = new[] { entry };
                walker.speed = 5;
                spawning.zombie = walker;
                spawning.minInterval = spawning.maxInterval = .04f;
                spawning.limit = 1;
                scene.spawn = spawning;
                var blockSize = scene.world.blockSize;
                var point = scene.points[0];
                point.position = new Vector3(0, 0, -(scene.world.halfSize.y + 1) * blockSize);
                scene.points = new[] { point };
            });
            float size = scene.grid.Size;
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
                    if (alive.Length == 1 && alive[0].transform.position.y > (YardGrid.YardLevel + .9f) * size) climbed = true;
                    yield return null;
                }
                Assert.IsTrue(climbed);
                Assert.Less(scene.player.Health.Current, scene.player.Health.Maximum);
                var first = Object.FindAnyObjectByType<ZombieAgent>();
                first.Health.Damage(first.Health.Maximum);
                Assert.AreEqual(1, scene.player.Kills);
                Assert.AreEqual(1, scene.player.Score.Normal);
                Assert.AreEqual(1, scene.player.Score.Balance);
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
                Assert.AreEqual(1, scene.player.Score.Balance, "The result is final once the player dies.");
                yield return new WaitForSeconds(.35f);
                Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
                scene.player.Restart();
                yield return null;
                yield return null;
                var fresh = Object.FindAnyObjectByType<YardPlayer>();
                Assert.AreEqual(fresh.config.hp, fresh.Health.Current);
                Assert.AreEqual(0, fresh.Kills);
                Assert.AreEqual(0, fresh.Score.Balance);
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
