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
        public Pickup Nearby { get; private set; }
        public int Kills { get; private set; }
        public float Elapsed { get; private set; }
        public float HitUntil { get; private set; }
        public float HurtUntil { get; private set; }
        public string Notice { get; private set; }
        public ExteriorFog Fog { get; internal set; }
        public bool Paused { get; private set; }

        private CharacterController body;
        private InputActionMap input;
        private GameObject model;
        private WeaponConfig shown;
        private float pitch, vertical, kick, noticeUntil;
        private Vector3 rest;
        private YardSounds sounds;
        private Collider[] nearbyColliders = new Collider[40];
        private Transform muzzle;
        private float voidHeight;

        public void Initialize(float voidHeight)
        {
            this.voidHeight = voidHeight;
            Gear = new Inventory(config);
            Health = GetComponent<Health>();
            Health.Initialize(config.hp);
            Health.Hurt += OnHurt;
            Health.Died += OnDeath;
            sounds = gameObject.AddComponent<YardSounds>();
            body = GetComponent<CharacterController>();
            input = controls.FindActionMap("Player", true).Clone();
            input.Enable();
            rest = hand.localPosition;
            ShowWeapon();
            SetCursor(true);
        }

        public void CountKill() => Kills++;
        private void OnHurt() { HurtUntil = Time.time + .3f; sounds.Play(YardCue.Hurt); }
        private void OnDeath() => SetCursor(false);
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
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
            SetCursor(!Paused);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            if (Gear == null) return;
            var keys = Keyboard.current;
            if (Health.Current == 0)
            {
                if (keys != null && keys.rKey.wasPressedThisFrame) Restart();
                return;
            }
            if (keys != null && keys.escapeKey.wasPressedThisFrame) { TogglePause(); return; }
            if (Paused) return;
            Elapsed += Time.deltaTime;
            if (transform.position.y < voidHeight) { Health.Damage(Health.Current); return; }
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

            if (keys != null && keys.digit1Key.wasPressedThisFrame) Gear.Select(0);
            else if (keys != null && keys.digit2Key.wasPressedThisFrame) Gear.Select(1);
            else
            {
                float wheel = Mouse.current?.scroll.ReadValue().y ?? 0;
                int step = input["Next"].WasPressedThisFrame() || wheel < 0 ? 1 :
                    input["Previous"].WasPressedThisFrame() || wheel > 0 ? -1 : 0;
                Gear.Select((Gear.Selected + step + Gear.Slots.Length) % Gear.Slots.Length);
            }
            Nearby = CollectAmmoAndFindWeapon();
            if (input["Interact"].WasPressedThisFrame() && Nearby)
            {
                if (Nearby.Collect(Gear)) { Nearby = null; sounds.Play(YardCue.Take); }
                else Message("Слоты заняты · G — бросить оружие");
            }
            if (input["Drop"].WasPressedThisFrame())
            {
                var weapon = Gear.Slots[Gear.Selected];
                if (weapon != null)
                {
                    if (Pickup.TryThrow(weapon, body, transform.forward))
                    {
                        Gear.Drop();
                        Message("Выброшено · " + weapon.Config.title);
                    }
                    else Message("Мало места для броска");
                }
            }
            if (input["Reload"].WasPressedThisFrame()) Gear.StartReload(Time.time);
            ShowWeapon();
            if (input["Attack"].IsPressed()) Attack();
            float cycle = Mathf.Min(.36f, shown.delay);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime / (shown.diggingMotion ? cycle : .25f));
            float bob = Mathf.Sin(Elapsed * speed * 1.9f) * move.magnitude * .012f;
            float reach = shown.diggingMotion ? .32f * Mathf.Sin(Mathf.PI * kick) : -kick * .09f;
            float swing = shown.diggingMotion ? 0 : (shown.UsesAmmo ? -10 : 38) * kick;
            hand.localPosition = rest + new Vector3(bob, Mathf.Abs(bob) - (Gear.Reloading ? .13f : 0), reach);
            hand.localRotation = Quaternion.Euler(swing + (Gear.Reloading ? 24 : 0), -6, -3);
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

        private Pickup CollectAmmoAndFindWeapon()
        {
            Vector3 chest = transform.TransformPoint(body.center);
            Vector3 eye = eyes.transform.position;
            float radius = config.pickupReach + Vector3.Distance(eye, chest);
            int total = Physics.OverlapSphereNonAlloc(chest, radius, nearbyColliders);
            while (total == nearbyColliders.Length)
            {
                System.Array.Resize(ref nearbyColliders, total * 2);
                total = Physics.OverlapSphereNonAlloc(chest, radius, nearbyColliders);
            }
            Pickup best = null;
            float nearest = config.pickupReach;
            bool collectedAmmo = false;
            for (int i = 0; i < total; i++)
            {
                var collider = nearbyColliders[i];
                if (!collider.TryGetComponent<Pickup>(out var pickup)) continue;
                Vector3 origin = pickup.IsAmmo ? chest : eye;
                Vector3 offset = collider.bounds.center - origin;
                float distance = offset.magnitude;
                if (distance > config.pickupReach) continue;
                if (Physics.Linecast(origin, collider.bounds.center, out var hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.collider != collider) continue;
                if (pickup.IsAmmo)
                {
                    collectedAmmo |= pickup.Collect(Gear);
                }
                else if (distance <= nearest && Vector3.Dot(offset.normalized, eyes.transform.forward) >= .25f)
                {
                    nearest = distance;
                    best = pickup;
                }
            }
            if (collectedAmmo) { sounds.Play(YardCue.Take); Message("Патроны подобраны"); }
            return best;
        }

        private void Message(string text)
        {
            Notice = text;
            noticeUntil = Time.time + 1.6f;
        }

        private void ShowWeapon()
        {
            if (Gear.Active.Config == shown) return;
            kick = 0;
            shown = Gear.Active.Config;
            if (model) Destroy(model);
            model = Instantiate(shown.visual, hand);
            muzzle = model.transform.Find("Muzzle");
        }

        public void Attack()
        {
            var held = Gear.Active;
            if (Health.Current == 0 || Paused || Gear.Reloading || Time.time < held.NextUse) return;
            held.NextUse = Time.time + held.Config.delay;
            if (held.Config.UsesAmmo && held.Loaded == 0)
            {
                Gear.StartReload(Time.time);
                if (!Gear.Reloading) Message("Нет патронов");
                return;
            }
            kick = 1;
            var view = eyes.transform;
            if (!held.Config.UsesAmmo)
            {
                sounds.Play(YardCue.Swing);
                if (Physics.Raycast(view.position, view.forward, out var hit, held.Config.melee.range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var target = Health.AtHit(hit);
                    if (target && target != Health) { target.Damage(held.Config.melee.damage); MarkHit(); }
                }
                return;
            }
            sounds.Play(YardCue.Shot);
            held.Fire();
            var ammo = held.Ammo;
            ShowWeapon();
            var start = muzzle ? muzzle.position : hand.position;
            bool nearCover = Physics.Linecast(view.position, start, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (nearCover) start = view.position;
            var aim = Physics.Raycast(view.position, view.forward, out var sight, ammo.range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? sight.point : view.position + view.forward * ammo.range;
            if (Vector3.Dot(aim - start, view.forward) <= 0) start = view.position;
            var alignment = Quaternion.LookRotation(aim - start, view.up);
            float radius = Mathf.Tan(ammo.spreadDegrees * Mathf.Deg2Rad);
            float rotation = Random.value * Mathf.PI * 2;
            for (int i = 0; i < ammo.projectiles; i++)
            {
                Vector2 offset = Vector2.zero;
                if (ammo.projectiles == 1) offset = Random.insideUnitCircle;
                else if (i > 0)
                {
                    // Keep one central pellet and distribute the others across different sectors.
                    float angle = rotation + (i - 1 + Random.Range(-.18f, .18f)) * 2 * Mathf.PI / (ammo.projectiles - 1);
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(.65f, 1f);
                }
                Vector3 heading = alignment * new Vector3(offset.x * radius, offset.y * radius, 1);
                Instantiate(ammo.projectilePrefab).Launch(ammo, start, heading, MarkHit);
            }
        }

        private void MarkHit() => HitUntil = Time.time + .14f;
    }
}
