// Ответственности
// - Обрабатывает ввод, движение игрока и обзор камеры.
// - Передаёт команды взаимодействия и атаки экипированному игроку.
// - Ведёт время попытки, паузу, победу и переход к следующей загрузке.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieYard
{
    [RequireComponent(typeof(Health), typeof(CharacterController))]
    public sealed class YardPlayer : MonoBehaviour
    {
        public Camera eyes;
        public Transform hand;
        public InputActionAsset controls;
        public PlayerConfig config;
        public Health Health { get; private set; }
        public Inventory Gear { get; private set; }
        public Pickup Nearby => interaction?.Nearby;
        public RunScore Score { get; } = new();
        public int Kills => Score.Total;
        public ShopStand AimedStand => interaction?.AimedStand;
        public float Elapsed { get; private set; }
        public float HitUntil { get; private set; }
        public float HurtUntil { get; private set; }
        public string Notice { get; private set; }
        public bool Paused { get; private set; }
        public bool Playing => Health && Health.Current > 0 && !Paused;
        public bool Victory { get; private set; }
        public int Cycle { get; private set; }
        private static int nextCycle;
        private bool reloadingScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCycle() => nextCycle = 0;

        private CharacterController body;
        private InputActionMap input;
        private PlayerInteraction interaction;
        private PlayerWeapon weapon;
        private WeaponView weaponView;
        private float pitch, vertical, noticeUntil;
        private YardSounds sounds;
        public float VoidHeight { get; private set; }

        public void Initialize(float voidHeight)
        {
            VoidHeight = voidHeight;
            Cycle = nextCycle;
            nextCycle = 0;
            Gear = new Inventory(config);
            Health = GetComponent<Health>();
            Health.Initialize(config.hp);
            Health.Hurt += OnHurt;
            Health.Died += OnDeath;
            sounds = gameObject.AddComponent<YardSounds>();
            body = GetComponent<CharacterController>();
            input = controls.FindActionMap("Player", true).Clone();
            input.Enable();
            interaction = new PlayerInteraction(this, body, sounds, Message);
            weaponView = new WeaponView(hand);
            weaponView.Show(Gear.Active.Config);
            weapon = new PlayerWeapon(Gear, Health, eyes.transform, weaponView, sounds, MarkHit, Message);
            SetCursor(true);
        }

        private void OnHurt() { HurtUntil = Time.time + .3f; sounds.Play(YardCue.Hurt); }
        private void OnDeath()
        {
            sounds.RunSaw(false);
            SetCursor(false);
        }
        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDestroy()
        {
            input?.Disable();
            input?.Dispose();
            if (Paused) Time.timeScale = 1;
            SetCursor(false);
        }

        public void TogglePause()
        {
            if (Victory) return;
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
            if (Paused) sounds.RunSaw(false);
            SetCursor(!Paused);
        }

        public void Restart()
        {
            if (!Victory) LoadCycle(Cycle);
        }

        internal void Win()
        {
            if (Victory || Health.Current == 0) return;
            Victory = true;
            Paused = true;
            sounds.RunSaw(false);
            Time.timeScale = 0;
            SetCursor(false);
        }

        public void ConfirmNewGamePlus()
        {
            if (Victory) LoadCycle(Cycle + 1);
        }

        private void LoadCycle(int cycle)
        {
            if (reloadingScene) return;
            reloadingScene = true;
            nextCycle = cycle;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            if (Gear == null) return;
            var keys = Keyboard.current;
            if (Victory) return;
            if (Health.Current == 0)
            {
                if (keys != null && keys.rKey.wasPressedThisFrame) Restart();
                return;
            }
            if (keys != null && keys.escapeKey.wasPressedThisFrame) { TogglePause(); return; }
            if (Paused) return;
            Elapsed += Time.deltaTime;
            if (transform.position.y < VoidHeight) { Health.Damage(Health.Current); return; }
            Gear.Tick(Time.time);
            if (Time.time > noticeUntil) Notice = null;

            var look = input["Look"];
            float sensitivity = look.activeControl?.device is Gamepad ? 130 * Time.deltaTime : .12f;
            Vector2 aim = look.ReadValue<Vector2>() * sensitivity;
            transform.Rotate(0, aim.x, 0);
            pitch = Mathf.Clamp(pitch - aim.y, -84, 84);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);

            Vector2 move = Vector2.ClampMagnitude(input["Move"].ReadValue<Vector2>(), 1);
            float speed = input["Sprint"].IsPressed() ? config.sprint : config.walk;
            Move((transform.right * move.x + transform.forward * move.y) * speed, input["Jump"].WasPressedThisFrame());

            if (input["SelectSlot1"].WasPressedThisFrame()) Gear.Select(0);
            else if (input["SelectSlot2"].WasPressedThisFrame()) Gear.Select(1);
            else
            {
                float wheel = input["ScrollSlots"].ReadValue<float>();
                int step = input["Next"].WasPressedThisFrame() || wheel < 0 ? 1 :
                    input["Previous"].WasPressedThisFrame() || wheel > 0 ? -1 : 0;
                Gear.Select((Gear.Selected + step + Gear.Slots.Count) % Gear.Slots.Count);
            }
            interaction.Update(input["Interact"].WasPressedThisFrame(), input["Drop"].WasPressedThisFrame());
            if (input["Reload"].WasPressedThisFrame()) Gear.StartReload(Time.time);
            var shown = Gear.Active.Config;
            weaponView.Show(shown);
            bool attacking = input["Attack"].IsPressed();
            bool sawing = attacking && shown.UsesContactAttack;
            sounds.RunSaw(sawing);
            if (attacking) Attack();
            weaponView.Animate(Elapsed, speed, move.magnitude, sawing, Gear.Reloading);
        }

        private void Move(Vector3 horizontal, bool jump)
        {
            const float gravity = 18f;
            if (body.isGrounded && jump) vertical = Mathf.Sqrt(2 * gravity * config.jumpHeight);
            float remaining = Time.deltaTime;
            while (remaining > 0)
            {
                float dt = Mathf.Min(remaining, 1f / 60f);
                if (body.isGrounded && vertical < 0) vertical = -2;
                float rise = (vertical - gravity * dt * .5f) * dt;
                var flags = body.Move(horizontal * dt + Vector3.up * rise);
                vertical -= gravity * dt;
                if ((flags & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
                remaining -= dt;
            }
        }

        private void Message(string text)
        {
            Notice = text;
            noticeUntil = Time.time + 1.6f;
        }

        public void Attack()
        {
            if (Playing) weapon.Attack();
        }

        private void MarkHit() => HitUntil = Time.time + .14f;
    }
}
