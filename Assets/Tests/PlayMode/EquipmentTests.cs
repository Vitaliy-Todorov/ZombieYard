// Ответственности
// - Проверяет стартовую выкладку, баланс ружья, рикошеты посоха и контакт пилы.
// - Проверяет подбор аптечек и применение настройки лечения в новой попытке.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZombieYard.Tests
{
    public sealed class EquipmentTests
    {
        private GameLifetimeScope scene;
        private WeaponConfig Chainsaw => System.Array.Find(scene.spawn.zombie.loot, entry => entry.weapon).weapon;
        private YardPlayer Player => scene.player;

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
        }

        [Test]
        public void FreshYardHasRifleAndNoFreeStaffSawOrMedkits()
        {
            var pickups = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            var yard = System.Array.FindAll(pickups, p => !scene.grid.IsOutside(p.transform.position)
                && p.transform.position.y >= YardGrid.YardLevel * scene.grid.Size - .1f);
            var titles = System.Array.ConvertAll(yard, p => p.Title);
            CollectionAssert.Contains(titles, scene.rifle.title);
            foreach (var absent in new[] { scene.staff.title, Chainsaw.title, "Малая аптечка", "Полная аптечка" })
                CollectionAssert.DoesNotContain(titles, absent);
        }

        [Test]
        public void SuppliedRifleHoldsOneRoundAndOutdamagesFullShotgunBlast()
        {
            var shotgun = Player.config.startingWeapons[1].compatibleAmmo[0];
            var rifleRound = scene.rifle.compatibleAmmo[0];
            Assert.AreEqual(1, scene.rifle.capacity);
            Assert.Greater(rifleRound.damage, shotgun.damage * shotgun.projectiles);
        }

        private void Equip(WeaponConfig config)
        {
            var pickup = Pickup.ForWeapon(new HeldWeapon(config), Player.transform.position, Player.VoidHeight);
            Assert.IsFalse(pickup.Collect(Player.Gear));
            Player.Gear.Drop();
            Assert.IsTrue(pickup.Collect(Player.Gear));
            Assert.AreSame(config, Player.Gear.Active.Config);
        }

        private static Health Target(Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.position = position;
            obj.transform.localScale = size;
            var hp = obj.AddComponent<Health>();
            hp.Initialize(1000);
            return hp;
        }

        [UnityTest]
        public IEnumerator EquippedStaffHitsAcrossRicochetsUntilItsLifetimeEnds()
        {
            Equip(scene.staff);
            var bolt = scene.staff.staffBolt;
            float ready = Time.time + scene.staff.delay;
            Player.Attack();
            Player.Attack();
            var shots = Object.FindObjectsByType<ShotProjectile>(FindObjectsSortMode.None);
            Assert.AreEqual(1, shots.Length, "The firing delay also applies to the staff.");
            var launch = shots[0].transform;
            var faceSize = new Vector3(1, 1, .1f);
            var front = Target(launch.position + launch.forward * 1.5f, faceSize);
            var back = Target(launch.position - launch.forward * 1.5f, faceSize);
            front.transform.rotation = back.transform.rotation = launch.rotation;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(bolt.ricochetSeconds * .55f);
            Assert.IsTrue(shots[0]);
            Assert.LessOrEqual(front.Current, front.Maximum - 2 * bolt.damage);
            Assert.LessOrEqual(back.Current, back.Maximum - bolt.damage);
            yield return new WaitForSeconds(bolt.ricochetSeconds * .45f + .08f);
            Assert.IsFalse(shots[0]);
            while (Time.time < ready) yield return null;
            Player.Attack();
            Assert.IsNotNull(Object.FindAnyObjectByType<ShotProjectile>());
        }

        [UnityTest]
        public IEnumerator ShortLivedBoltBouncesOffYardBlockAndExpiresInFlight()
        {
            var bolt = Object.Instantiate(scene.staff.staffBolt);
            try
            {
                bolt.speed = 12;
                bolt.damage = 9;
                bolt.ricochetSeconds = .28f;
                var floor = scene.grid.Center(new Vector2Int(3, -2));
                var start = floor + Vector3.up * .45f;
                Assert.IsTrue(Physics.Raycast(start, Vector3.down, out var hit, 1));
                var block = YardChunk.ResolveHit(hit);
                Assert.IsNotNull(block);
                int before = block.Current;
                var ceiling = Target(floor + Vector3.up * 2, new Vector3(1, .2f, 1));
                Physics.SyncTransforms();
                var shot = Object.Instantiate(bolt.projectilePrefab);
                shot.Launch(bolt, start, Vector3.down, null);
                yield return new WaitForSeconds(.4f);
                Assert.IsFalse(shot);
                Assert.AreEqual(before - 9, block.Current);
                Assert.AreEqual(ceiling.Maximum - 9, ceiling.Current);
            }
            finally { Object.Destroy(bolt); }
        }

        [UnityTest]
        public IEnumerator SawTicksOncePerTargetAndStopsOnReleaseOrLostContact()
        {
            Equip(Chainsaw);
            var blade = Chainsaw.melee;
            var view = Player.eyes.transform;
            var target = new GameObject("Compound saw target");
            var hp = target.AddComponent<Health>();
            hp.Initialize(1000);
            target.transform.SetPositionAndRotation(view.position + view.forward * 1.05f, view.rotation);
            foreach (float side in new[] { -.11f, .11f })
            {
                var part = new GameObject("Side collider");
                part.transform.SetParent(target.transform, false);
                part.transform.localPosition = Vector3.right * side;
                part.AddComponent<BoxCollider>().size = new Vector3(.12f, .17f, .25f);
            }
            Physics.SyncTransforms();
            Player.Attack();
            Player.Attack();
            Assert.AreEqual(hp.Maximum - blade.damage, hp.Current);
#if UNITY_EDITOR
            // The Test Runner can run with an unfocused Game View.
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                int expectedHealth = hp.Maximum - 3 * blade.damage;
                float deadline = Time.time + Mathf.Max(2f, Chainsaw.delay * 3.5f);
                while (hp.Current > expectedHealth && Time.time < deadline)
                    yield return null;
                Assert.LessOrEqual(hp.Current, expectedHealth);
                target.transform.position += view.right * 3;
                Physics.SyncTransforms();
                int outOfReach = hp.Current;
                yield return new WaitForSeconds(Chainsaw.delay * 2);
                Assert.AreEqual(outOfReach, hp.Current);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                target.transform.position -= view.right * 3;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(Chainsaw.delay * 2);
                Assert.AreEqual(outOfReach, hp.Current);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
                InputSystem.settings.backgroundBehavior = previousBackground;
#endif
            }
        }

        private void PutWithinReach(Pickup item)
        {
            item.GetComponent<Rigidbody>().isKinematic = true;
            var body = Player.GetComponent<CharacterController>();
            item.transform.position = Player.transform.TransformPoint(body.center) - Player.transform.forward * .9f;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator MedkitsStayAtFullHealthAndRestoreConfiguredAmountsOnApproach()
        {
            var hp = Player.Health;
            var small = Pickup.ForMedkit(Player.config, false, Player.transform.position, Player.VoidHeight);
            PutWithinReach(small);
            yield return null;
            Assert.IsTrue(small && small.gameObject.activeSelf);
            hp.Damage(hp.Maximum - 1);
            yield return null;
            Assert.AreEqual(1 + Player.config.partialHealing, hp.Current);
            Assert.IsFalse(small && small.gameObject.activeSelf);
            var full = Pickup.ForMedkit(Player.config, true, Player.transform.position, Player.VoidHeight);
            PutWithinReach(full);
            yield return null;
            Assert.AreEqual(hp.Maximum, hp.Current);
            Assert.IsFalse(full && full.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator NewAttemptReadsChangedPartialHealingAndCapsItAtMaximum()
        {
            var config = Object.Instantiate(Player.config);
            try
            {
                config.partialHealing = 7;
                yield return YardTestScene.Load(scope =>
                {
                    scene = scope;
                    scope.player.config = config;
                });
                var hp = Player.Health;
                Assert.AreEqual(hp.Maximum, hp.Current);
                hp.Damage(20);
                PutWithinReach(Pickup.ForMedkit(Player.config, false, Player.transform.position, Player.VoidHeight));
                yield return null;
                Assert.AreEqual(hp.Maximum - 13, hp.Current);
                var extra = Pickup.ForMedkit(config, false, Player.transform.position, Player.VoidHeight);
                hp.Heal(10);
                PutWithinReach(extra);
                yield return null;
                Assert.AreEqual(hp.Maximum, hp.Current);
                Assert.IsFalse(extra && extra.gameObject.activeSelf);
            }
            finally { Object.Destroy(config); }
        }
    }
}
