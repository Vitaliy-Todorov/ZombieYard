// Ответственности
// - Проверяет слои участка, восстановление блоков и разрушение на шве чанков.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class TerrainTests
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
            float yardHeight = YardGrid.YardLevel * scene.grid.Size;
            Vector3 origin = Vector3.up * (yardHeight + 2);
            try
            {
                config.soil.hp = originalHp + 17;
                config.plateauHeight = originalHeight + 1;
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var hit, 4));
                var block = YardChunk.ResolveHit(hit);
                Assert.AreEqual(originalHp, block.Current);
                block.Damage(block.Current);
                Assert.Less(scene.grid.Height(Vector2Int.zero), yardHeight);

                yield return SceneManager.LoadSceneAsync("Yard");
                yield return null;
                scene = Object.FindAnyObjectByType<GameLifetimeScope>();
                Assert.AreEqual(yardHeight, scene.grid.Height(Vector2Int.zero));
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out hit, 4));
                Assert.AreEqual(originalHp + 17, YardChunk.ResolveHit(hit).Current);
                var lowland = new Vector2Int(config.halfSize.x + config.plateauHeight + 1, 0);
                Assert.AreEqual((YardGrid.YardLevel - config.plateauHeight) * scene.grid.Size, scene.grid.Height(lowland));
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
            var cell = new Vector2Int(-1, grid.Shop.Center.y >= 0 ? -4 : 4);
            Vector3 top = grid.Center(cell);
            Vector3 neighbor = top + Vector3.right * grid.Size;
            Assert.IsTrue(Physics.Raycast(neighbor + Vector3.up, Vector3.down, out var other, 2));
            var adjacent = YardChunk.ResolveHit(other);
            var strike = scene.player.Gear.Active.Config.melee;
            for (int layer = 0; layer < 2 + scene.world.subsoilDepth; layer++)
            {
                Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var hit));
                var block = YardChunk.ResolveHit(hit);
                int hp = scene.world.soil.hp * (layer < 2 + scene.world.plateauHeight ? 1 : scene.world.subsoilHpMultiplier);
                Assert.AreEqual(hp, block.Maximum);
                int hits = Mathf.CeilToInt((float)hp / strike.damage);
                for (int i = 1; i <= hits; i++)
                {
                    Assert.Greater(block.Current, 0);
                    block.Damage(strike.damage);
                    if (i < hits) Assert.AreEqual(top.y - layer * grid.Size, grid.Height(cell));
                }
                Assert.AreEqual(top.y - (layer + 1) * grid.Size, grid.Height(cell));
            }
            Assert.IsTrue(Physics.Raycast(top + Vector3.up, Vector3.down, out var floor));
            Assert.IsNull(YardChunk.ResolveHit(floor));
            Assert.AreEqual(top.y - (2 + scene.world.subsoilDepth) * grid.Size, floor.point.y, .01f);
            foreach (var dir in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                int half = dir.x == 0 ? scene.world.halfSize.y : scene.world.halfSize.x;
                var outer = dir * (half + scene.world.mountainDistance + 10);
                var origin = grid.Center(outer) + Vector3.up;
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var rim, 2));
                Assert.IsNull(YardChunk.ResolveHit(rim));
                var through = new Vector3(outer.x, YardGrid.YardLevel - 12, outer.y) * grid.Size;
                Vector3 outward = new Vector3(dir.x, 0, dir.y);
                Assert.IsTrue(Physics.Raycast(through + 2 * grid.Size * outward, -outward, out rim, 3 * grid.Size));
                Assert.IsNull(YardChunk.ResolveHit(rim));
            }
            Assert.IsTrue(Physics.Raycast(top - Vector3.up * grid.Size * .5f, Vector3.right, out var wall, grid.Size));
            Assert.AreSame(adjacent, YardChunk.ResolveHit(wall));
            Assert.AreEqual(adjacent.Maximum, adjacent.Current);
        }
    }
}
