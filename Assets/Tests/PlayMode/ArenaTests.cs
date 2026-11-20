using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ZombieYard.Tests
{
    public sealed class ArenaTests
    {
        private GameLifetimeScope scene;
        private ZombieSpawnerConfig spawn;
        private YardPlayer Player => scene.player;
        private ArenaEye Eye => scene.Eye;
        private int ActivePortals => scene.grid.GetComponentsInChildren<ZombiePortal>().Length;
        private Vector3 Floor(int x, int z) => scene.grid.Arena.Floor(x, z);

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            scene.points = System.Array.Empty<Transform>();
            spawn = Object.Instantiate(scene.spawn);
            scene.spawn = spawn;
            spawn.limit = 0;
            spawn.portalChance = 0;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1;
            Object.Destroy(spawn);
        }

        private void Move(Vector3 feet)
        {
            var body = Player.GetComponent<CharacterController>();
            body.enabled = false;
            Player.transform.position = feet + Vector3.up * .02f;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator SealedSlopeCanBeExcavatedToReachGalleryAndMedkits()
        {
            float size = scene.grid.Size;
            var outside = Floor(-16, 0);
            var inside = Floor(-7, 0);
            for (int layer = 0; layer < 2; layer++)
            {
                var height = Vector3.up * ((layer + .5f) * size);
                int dug = 0;
                while (Physics.Linecast(outside + height, inside + height, out var hit))
                {
                    var block = Health.AtHit(hit);
                    Assert.IsNotNull(block, "The mountain must be diggable from the yard side.");
                    block.Damage(block.Current, byPlayer: true);
                    Assert.Less(++dug, 10);
                }
                Assert.Greater(dug, 0, "Fresh terrain must seal both levels of the tunnel.");
            }
            Move(Floor(-10, 0));
            var controller = Player.GetComponent<CharacterController>();
            for (int step = 0; step < 6; step++) controller.Move(Vector3.right * size * .5f);
            Assert.AreEqual(inside.x, Player.transform.position.x, .08f);
            yield return null;
            yield return null;
            Assert.AreNotEqual(EyePhase.Dormant, Eye.Phase);
            Assert.Greater(Floor(0, 6).y, Floor(0, 0).y + size);
            Assert.IsTrue(Physics.Raycast(Floor(0, 6) + Vector3.up, Vector3.down, out var ledge, 2));
            Assert.AreEqual(Floor(0, 6).y, ledge.point.y, .01f);
            Assert.IsTrue(Physics.Raycast(Floor(2, 0) + Vector3.up, Vector3.up, out var roof));
            Assert.AreEqual(ArenaLayout.Roof * size, roof.point.y, .01f);
            Assert.IsFalse(Player.Fog.Contains(Floor(0, 0) + Vector3.up * .1f));
            var kits = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None)
                .Where(p => p.Title == "Полная аптечка").ToArray();
            Assert.AreEqual(2, kits.Length);
            Player.Health.Damage(40);
            Move(Floor(-6, 3));
            yield return null;
            yield return null;
            Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
            Assert.AreEqual(1, kits.Count(p => p && p.gameObject.activeSelf));
        }

        [UnityTest]
        public IEnumerator LaserChargesBreaksCoverAndHitsOnlyAfterOpening()
        {
            Assert.AreEqual(EyePhase.Dormant, Eye.Phase);
            Move(Floor(6, 4));
            var origin = Eye.transform.position;
            Assert.IsTrue(Physics.Linecast(origin, Player.GetComponent<CharacterController>().bounds.center, out var face));
            var cover = Health.AtHit(face);
            Assert.IsNotNull(cover);
            Assert.AreEqual(240, cover.Maximum);
            Assert.AreEqual(12, scene.world.eye.laser.damage);
            var beam = Eye.transform.Find("Луч");
            bool charged = false, chipped = false, cooled = false;
            float until = Time.time + 40;
            while ((!cooled || Player.Health.Current == Player.Health.Maximum) && Time.time < until)
            {
                yield return null;
                if (Eye.Phase == EyePhase.Charge)
                {
                    charged |= beam.gameObject.activeSelf;
                    if (!chipped) Assert.AreEqual(cover.Maximum, cover.Current);
                }
                if (cover && cover.Current > 0 && cover.Current < cover.Maximum)
                {
                    chipped = true;
                    Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
                    if (Eye.Phase == EyePhase.Fire)
                        Assert.Less(Vector3.Distance(beam.TransformPoint(Vector3.up), face.point), .18f);
                }
                if (!cooled && Eye.Phase == EyePhase.Cooldown)
                {
                    Assert.IsFalse(beam.gameObject.activeSelf);
                    Assert.IsTrue(cover && cover.Current > 0 && cover.Current < cover.Maximum,
                        "A full laser burst chips an intact column and leaves it standing.");
                    Assert.AreEqual(Player.Health.Maximum, Player.Health.Current);
                    cooled = true;
                }
            }
            Assert.IsTrue(charged && chipped && cooled);
            Assert.IsTrue(!cover || cover.Current == 0);
            Assert.Less(Player.Health.Current, Player.Health.Maximum);
            Assert.Greater(Player.Health.Current, 0);
        }

        [UnityTest]
        public IEnumerator ArenaPortalsWaitForEntryAndRespectBothCaps()
        {
            Player.Health.Initialize(1000);
            spawn.limit = 2;
            spawn.arenaLimit = 1;
            spawn.arenaInterval = .4f;
            Assert.IsFalse(Eye.InAttackRange);
            Move(scene.grid.Center(scene.grid.Cell(Floor(-11, 0))));
            yield return new WaitForSeconds(4.4f);
            Assert.AreEqual(EyePhase.Dormant, Eye.Phase);
            Assert.IsTrue(Eye.InAttackRange, "The health bar follows attack radius even while the mountain is sealed.");
            Assert.AreEqual(0, ActivePortals);
            Assert.IsNull(Object.FindAnyObjectByType<ZombieAgent>());
            Move(Floor(-7, -3));
            yield return new WaitForSeconds(4.5f);
            Assert.AreEqual(3, ActivePortals);
            var first = Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single();
            Assert.Less(Vector3.Distance(first.transform.position, Floor(0, 0)), 12 * scene.grid.Size);
            spawn.arenaLimit = 2;
            spawn.limit = 1;
            yield return new WaitForSeconds(.6f);
            Assert.AreSame(first, Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single());
            first.Health.Damage(first.Health.Current);
            yield return new WaitForSeconds(.65f);
            Assert.AreNotSame(first, Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Single());
            Move(scene.grid.Center(Vector2Int.zero));
            yield return null;
            yield return null;
            Assert.IsFalse(Eye.InAttackRange);
        }

        [UnityTest]
        public IEnumerator ShotEndsFightAndConfirmationStartsFreshCycle()
        {
            Player.ConfirmNewGamePlus();
            Move(Floor(-5, 0));
            yield return null;
            yield return null;
            Assert.AreEqual(0, Player.Cycle);
            Assert.AreEqual(3, ActivePortals);
            var oldPlayer = Player;
            Player.Score.RecordKill(ZombieKind.Red);
            var pillarFeet = scene.grid.Cell(Floor(-4, 3));
            var cell = new Vector3Int(pillarFeet.x, YardGrid.YardLevel, pillarFeet.y);
            scene.grid.HealthAt(cell).Damage(int.MaxValue);
            Eye.Health.Damage(Eye.Health.Maximum - 1);
            Player.Gear.Select(1);
            Player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Player.eyes.transform.LookAt(Eye.transform.position);
            Player.Attack();
            float until = Time.realtimeSinceStartup + 3;
            while (!Player.Victory && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(Player.Victory);
            Assert.AreEqual(EyePhase.Defeated, Eye.Phase);
            Assert.IsFalse(Eye.InAttackRange);
            Assert.AreEqual(0, ActivePortals);
            Player.TogglePause();
            Player.Restart();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreSame(oldPlayer, Object.FindAnyObjectByType<YardPlayer>());
            Assert.AreEqual(0, Time.timeScale);
            Player.ConfirmNewGamePlus();
            yield return null;
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            Assert.AreNotSame(oldPlayer, Player);
            Assert.AreEqual(1, Player.Cycle);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(0, Player.Score.Balance);
            Assert.AreEqual(0, Player.Kills);
            Assert.AreEqual(Player.config.hp, Player.Health.Current);
            Assert.AreEqual(scene.world.eye.hp, Eye.Health.Current);
            Assert.AreEqual(EyePhase.Dormant, Eye.Phase);
            Assert.AreEqual(0, ActivePortals);
            Assert.AreEqual(scene.world.arenaPillar.hp, scene.grid.HealthAt(cell).Current);
        }
    }
}
