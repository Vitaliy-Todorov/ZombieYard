// Ответственности
// - Проверяет привязки слотов, подбор, выброс оружия и движение с заданной скоростью через ввод в Yard.

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
#if UNITY_EDITOR
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousRouting;

        [SetUp]
        public void RouteTestInput()
        {
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void RestoreInputRouting()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }
#endif

        [UnityTest]
        public IEnumerator ReboundKeySelectsSlotDirectly()
        {
            YardPlayer player = null;
            InputActionAsset controls = null;
            var keys = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return YardTestScene.Load(scope =>
                {
                    player = scope.player;
                    controls = Object.Instantiate(player.controls);
                    controls.FindAction("Player/SelectSlot2", true).ChangeBinding(0).WithPath("<Keyboard>/f2");
                    player.controls = controls;
                });
                IEnumerator Press(Key key)
                {
                    InputSystem.QueueStateEvent(keys, new KeyboardState(key));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(keys, new KeyboardState());
                    yield return null;
                }
                yield return Press(Key.Digit2);
                Assert.AreEqual(0, player.Gear.Selected);
                yield return Press(Key.F2);
                Assert.AreEqual(1, player.Gear.Selected);
                yield return Press(Key.F2);
                Assert.AreEqual(1, player.Gear.Selected);
                yield return Press(Key.Digit1);
                Assert.AreEqual(0, player.Gear.Selected);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
                Object.Destroy(controls);
            }
        }

        [UnityTest]
        public IEnumerator WalkingAndSprintingUseConfiguredSpeeds()
        {
            YardPlayer player = null;
            PlayerConfig config = null;
            float previousDelta = Time.captureDeltaTime;
            var keys = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return YardTestScene.Load(scope =>
                {
                    player = scope.player;
                    config = Object.Instantiate(player.config);
                    config.walk = .12f;
                    config.sprint = 3f;
                    player.config = config;
                });
                IEnumerator CheckSpeed(KeyboardState state, float expectedSpeed)
                {
                    InputSystem.QueueStateEvent(keys, state);
                    yield return null;
                    yield return null;
                    Vector3 start = player.transform.position;
                    float started = player.Elapsed;
                    for (int frame = 0; frame < 30; frame++) yield return null;
                    Assert.Greater(player.Elapsed, started);
                    var travelled = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up);
                    Assert.AreEqual(expectedSpeed * (player.Elapsed - started), travelled.magnitude, .001f);
                }
                // Each walking frame covers half a millimetre at this configured speed.
                Time.captureDeltaTime = 1f / 240;
                yield return CheckSpeed(new KeyboardState(Key.W), config.walk);
                Time.captureDeltaTime = 1f / 60;
                yield return CheckSpeed(new KeyboardState(Key.W, Key.LeftShift), config.sprint);
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
                InputSystem.RemoveDevice(keys);
                Object.Destroy(config);
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
            Assert.IsTrue(shotgun.TryConsumeRound());
            gear.StartReload(Time.time);
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
                var dropped = System.Array.Find(Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None),
                    item => item.Title == shotgun.Config.title);
                Assert.IsNotNull(dropped);
                var rigidbody = dropped.GetComponent<Rigidbody>();
                float settleBy = Time.time + 3;
                while (!rigidbody.IsSleeping() && Time.time < settleBy) yield return null;

                var center = dropped.GetComponent<Collider>().bounds.center;
                var approach = Vector3.ProjectOnPlane(center - player.transform.position, Vector3.up).normalized;
                var body = player.GetComponent<CharacterController>();
                float eyeHeight = player.eyes.transform.position.y - center.y;
                float horizontalReach = Mathf.Sqrt(Mathf.Max(0, player.config.pickupReach * player.config.pickupReach - eyeHeight * eyeHeight));
                var step = center - approach * (horizontalReach * .75f) - player.transform.position;
                body.Move(Vector3.ProjectOnPlane(step, Vector3.up));
                player.transform.rotation = Quaternion.LookRotation(approach);
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Assert.AreSame(dropped, player.Nearby, $"Eye {player.eyes.transform.position}, item {center}, reach {player.config.pickupReach}");
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Assert.AreSame(shotgun, gear.Active);
                Assert.AreEqual(shotgun.Config.capacity - 1, shotgun.Loaded);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
            }
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
            var rounds = Pickup.ForAmmo(ammo, 4, behind + Vector3.up * .3f, player.VoidHeight);
            yield return null;
            Assert.AreEqual(reserve, gear.Stock(ammo));
            var controller = player.GetComponent<CharacterController>();
            controller.Move(-player.transform.forward * 2);
            var weapon = Pickup.ForWeapon(new HeldWeapon(gear.Slots[0].Config),
                player.transform.position + player.transform.right * .65f + Vector3.up * .4f, player.VoidHeight);
            yield return null;
            yield return null;
            Assert.AreEqual(reserve + 4, gear.Stock(ammo));
            Assert.IsFalse(rounds && rounds.gameObject.activeSelf);
            Assert.IsTrue(weapon.gameObject.activeSelf);
            Assert.AreEqual(2, gear.Slots.Count);
            Assert.IsFalse(weapon.Collect(gear));
        }

        [UnityTest]
        public IEnumerator PlayerFallsIntoExcavatedCellAndCanJumpOut()
        {
            yield return SceneManager.LoadSceneAsync("Yard");
            yield return null;
            var scene = Object.FindAnyObjectByType<GameLifetimeScope>();
            var player = scene.player;
            var cell = scene.grid.Cell(player.transform.position);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = scene.grid.Center(cell) + Vector3.up * .04f;
            controller.enabled = true;
            var shovel = player.Gear.Active.Config;
            float yardHeight = scene.grid.Height(cell);
            int hits = Mathf.CeilToInt((float)scene.world.soil.hp / shovel.melee.damage);
            for (int i = 0; i < hits; i++)
            {
                if (i > 0) yield return new WaitForSeconds(shovel.delay);
                player.eyes.transform.rotation = Quaternion.LookRotation(Vector3.down);
                player.Attack();
            }
            Assert.AreEqual(yardHeight - scene.grid.Size, scene.grid.Height(cell));
            yield return new WaitForSeconds(.65f);
            Assert.IsTrue(controller.isGrounded);
            Assert.AreEqual(yardHeight - scene.grid.Size, player.transform.position.y, .1f);
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
                Assert.Greater(player.transform.position.y, yardHeight - .15f, $"Jump from {cell}, peak {peak}, final {player.transform.position}");
            }
            finally { InputSystem.RemoveDevice(pad); }
        }
    }
}
