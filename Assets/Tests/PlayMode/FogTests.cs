// Ответственности
// - Проверяет перенос объёма тумана и урон игроку при контакте с ним.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class FogTests
    {
        [UnityTest]
        public IEnumerator LowlandFogDamagesUntilPlayerReturnsToDryGround()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var fog = Object.FindAnyObjectByType<ExteriorFog>();
            var player = scene.player;
            var body = player.GetComponent<CharacterController>();
            var start = player.transform.position;
            foreach (var point in scene.points) point.gameObject.SetActive(false);
            var lowland = new Vector2Int(scene.world.halfSize.x + scene.world.plateauHeight + 1, 0);
            Vector3 outside = scene.grid.Center(lowland);
            Assert.AreEqual((YardGrid.YardLevel - scene.world.plateauHeight) * scene.grid.Size, outside.y);
            body.enabled = false;
            player.transform.position = outside + Vector3.up * .03f;
            body.enabled = true;
            int expected = player.Health.Current - Mathf.RoundToInt(5 * scene.world.fogDamage);
            yield return new WaitForSeconds(scene.world.fogInterval * 5 + .1f);
            Assert.IsTrue(fog.Exposed);
            Assert.AreEqual(expected, player.Health.Current);
            body.enabled = false;
            player.transform.position = start;
            body.enabled = true;
            yield return new WaitForSeconds(scene.world.fogInterval + .1f);
            Assert.IsFalse(fog.Exposed);
            Assert.AreEqual(expected, player.Health.Current);
        }

        [UnityTest]
        public IEnumerator MistFlowsThroughAnOpenedWallIntoAnIsolatedPit()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var fog = Object.FindAnyObjectByType<ExteriorFog>();
            var grid = scene.grid;
            float size = grid.Size;
            int edge = scene.world.halfSize.x;
            var shaft = new Vector2Int(edge + 1, 4);
            var bank = new Vector2Int(edge + 3, shaft.y);
            foreach (Vector2Int direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                int boundary = direction.x == 0 ? scene.world.halfSize.y : edge;
                for (int ring = 1; ring <= scene.world.plateauHeight + 1; ring++)
                    Assert.AreEqual((YardGrid.YardLevel - ring + 1) * size, grid.Height(direction * (boundary + ring)));
            }
            Vector3 reservoir = grid.Center(bank);
            reservoir.y = -2 * size;
            Assert.IsFalse(fog.Contains(reservoir + .01f * Vector3.up));
            Assert.IsTrue(fog.Contains(reservoir - .01f * Vector3.up));

            Vector3 pit = grid.Center(shaft);
            for (int y = 0; y < 4; y++)
            {
                Assert.IsTrue(Physics.Raycast(pit + Vector3.up, Vector3.down, out var ground));
                YardChunk.ResolveHit(ground).Damage(int.MaxValue);
            }
            Vector3 channel = new Vector3(bank.x, -2.5f, bank.y) * size;
            Vector3 inlet = new Vector3(shaft.x, -2.5f, shaft.y) * size;
            Vector3 floor = inlet + Vector3.down * 2 * size;
            yield return new WaitForSeconds(scene.world.fogFlowInterval * 2);
            Assert.IsFalse(fog.Contains(floor));
            Assert.IsTrue(Physics.Raycast(channel, Vector3.left, out var wall, size));
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
            YardChunk.ResolveHit(wall).Damage(int.MaxValue);
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
            Assert.IsFalse(fog.Contains(inlet + Vector3.up * size));

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
    }
}
