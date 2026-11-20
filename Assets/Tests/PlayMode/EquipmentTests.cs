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
        private YardPlayer Player => scene.player;

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            scene = Object.FindAnyObjectByType<GameLifetimeScope>();
        }

        private static Pickup FindPickup(string title)
        {
            var pickup = System.Array.Find(Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None),
                item => item.Title == title);
            Assert.IsNotNull(pickup, title);
            return pickup;
        }

        private void Equip(WeaponConfig config)
        {
            var pickup = FindPickup(config.title);
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
                var block = Health.AtHit(hit);
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
            Equip(scene.chainsaw);
            var blade = scene.chainsaw.melee;
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
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return new WaitForSeconds(scene.chainsaw.delay * 3.5f);
                Assert.LessOrEqual(hp.Current, hp.Maximum - 3 * blade.damage);
                target.transform.position += view.right * 3;
                Physics.SyncTransforms();
                int outOfReach = hp.Current;
                yield return new WaitForSeconds(scene.chainsaw.delay * 2);
                Assert.AreEqual(outOfReach, hp.Current);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                target.transform.position -= view.right * 3;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(scene.chainsaw.delay * 2);
                Assert.AreEqual(outOfReach, hp.Current);
            }
            finally { InputSystem.RemoveDevice(mouse); }
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
            var small = FindPickup("Малая аптечка");
            var full = FindPickup("Полная аптечка");
            PutWithinReach(small);
            yield return null;
            Assert.IsTrue(small && small.gameObject.activeSelf);
            hp.Damage(hp.Maximum - 1);
            yield return null;
            Assert.AreEqual(1 + Player.config.partialHealing, hp.Current);
            Assert.IsFalse(small && small.gameObject.activeSelf);
            PutWithinReach(full);
            yield return null;
            Assert.AreEqual(hp.Maximum, hp.Current);
            Assert.IsFalse(full && full.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator NewAttemptReadsChangedPartialHealingAndCapsItAtMaximum()
        {
            var config = Player.config;
            int original = config.partialHealing;
            try
            {
                config.partialHealing = 7;
                yield return OpenYard();
                var hp = Player.Health;
                Assert.AreEqual(hp.Maximum, hp.Current);
                hp.Damage(20);
                PutWithinReach(FindPickup("Малая аптечка"));
                yield return null;
                Assert.AreEqual(hp.Maximum - 13, hp.Current);
                var extra = Pickup.ForMedkit(config, false, Player.transform.position);
                hp.Heal(10);
                PutWithinReach(extra);
                yield return null;
                Assert.AreEqual(hp.Maximum, hp.Current);
                Assert.IsFalse(extra && extra.gameObject.activeSelf);
            }
            finally { config.partialHealing = original; }
        }
    }
}
