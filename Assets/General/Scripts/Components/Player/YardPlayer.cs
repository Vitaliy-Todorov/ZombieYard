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
        public RunScore Score { get; } = new();
        public int Kills => Score.Total;
        public ShopStand AimedStand { get; private set; }
        public float Elapsed { get; private set; }
        public float HitUntil { get; private set; }
        public float HurtUntil { get; private set; }
        public string Notice { get; private set; }
        public ExteriorFog Fog { get; internal set; }
        public bool Paused { get; private set; }
        public bool Victory { get; private set; }
        public int Cycle { get; private set; }
        private static int nextCycle;
        private bool reloadingScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCycle() => nextCycle = 0;

        private CharacterController body;
        private InputActionMap input;
        private GameObject model;
        private WeaponConfig shown;
        private float pitch, vertical, kick, noticeUntil;
        private Vector3 rest;
        private YardSounds sounds;
        private Collider[] nearbyColliders = new Collider[40];
        private Transform muzzle;
        private readonly System.Collections.Generic.HashSet<Health> contactTargets = new();
        private float voidHeight;

        public void Initialize(float voidHeight)
        {
            this.voidHeight = voidHeight;
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
            rest = hand.localPosition;
            ShowWeapon();
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
            Nearby = CollectSuppliesAndFindWeapon();
            AimedStand = Physics.Raycast(eyes.transform.position, eyes.transform.forward, out var target,
                config.pickupReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? target.collider.GetComponentInParent<ShopStand>() : null;
            if (input["Interact"].WasPressedThisFrame() && AimedStand)
                Message(AimedStand.Buy(Score));
            else if (input["Interact"].WasPressedThisFrame() && Nearby)
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
            bool attacking = input["Attack"].IsPressed();
            bool sawing = attacking && shown.melee && shown.melee.contactRadius > 0;
            sounds.RunSaw(sawing);
            if (attacking) Attack();
            float cycle = Mathf.Min(.36f, shown.delay);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime / (shown.diggingMotion ? cycle : .25f));
            float bob = Mathf.Sin(Elapsed * speed * 1.9f) * move.magnitude * .012f;
            float reach = sawing ? .012f * Mathf.Sin(Time.time * 94) : shown.diggingMotion ? .32f * Mathf.Sin(Mathf.PI * kick) : -kick * .09f;
            float swing = shown.diggingMotion ? 0 : (shown.melee && shown.melee.contactRadius > 0 ? 3 : shown.melee ? 38 : -10) * kick;
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

        private Pickup CollectSuppliesAndFindWeapon()
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
            int hpBefore = Health.Current;
            for (int i = 0; i < total; i++)
            {
                var collider = nearbyColliders[i];
                if (!collider.TryGetComponent<Pickup>(out var pickup)) continue;
                Vector3 origin = pickup.Automatic ? chest : eye;
                Vector3 offset = collider.bounds.center - origin;
                float distance = offset.magnitude;
                if (distance > config.pickupReach) continue;
                if (Physics.Linecast(origin, collider.bounds.center, out var hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.collider != collider) continue;
                if (pickup.Automatic)
                {
                    if (pickup.Collect(Gear, Health)) collectedAmmo |= pickup.IsAmmo;
                }
                else if (distance <= nearest && Vector3.Dot(offset.normalized, eyes.transform.forward) >= .25f)
                {
                    nearest = distance;
                    best = pickup;
                }
            }
            int healed = Health.Current - hpBefore;
            if (collectedAmmo || healed > 0)
            {
                sounds.Play(YardCue.Take);
                Message(healed > 0 ? $"Здоровье +{healed}" : "Патроны подобраны");
            }
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
            if (held.Config.melee)
            {
                if (held.Config.melee.contactRadius > 0)
                {
                    SawContact(view, held.Config.melee);
                    return;
                }
                sounds.Play(YardCue.Swing);
                if (Physics.Raycast(view.position, view.forward, out var hit, held.Config.melee.range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var target = Health.AtHit(hit);
                    if (target && target != Health) { target.Damage(held.Config.melee.damage, byPlayer: true); MarkHit(); }
                }
                return;
            }
            sounds.Play(YardCue.Shot);
            if (held.Config.UsesAmmo) held.Fire();
            var ammo = held.Config.staffBolt ? held.Config.staffBolt : held.Ammo;
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

        private void SawContact(Transform view, AttackConfig blade)
        {
            contactTargets.Clear();
            float radius = Mathf.Min(blade.contactRadius, blade.range * .5f);
            Vector3 start = view.position + view.forward * radius;
            // Keep the centre of the blade effective even when the initial sphere overlaps a chunk.
            if (Physics.Raycast(view.position, view.forward, out var center, blade.range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = Health.AtHit(center);
                if (target && target != Health) contactTargets.Add(target);
            }
            var contacts = Physics.SphereCastAll(start, radius, view.forward, blade.range - radius * 2,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var contact in contacts)
            {
                Vector3 point = contact.distance > 0 ? contact.point : contact.collider.bounds.center;
                Vector3 offset = point - view.position;
                // A ray resolves the visible voxel triangle and blocks contact through cover.
                if (!Physics.Raycast(view.position, offset, out var face, Mathf.Min(offset.magnitude, blade.range) + .015f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (face.collider != contact.collider) continue;
                var target = Health.AtHit(face);
                if (target && target != Health) contactTargets.Add(target);
            }
            foreach (var target in contactTargets)
                if (target && target.Current > 0) { target.Damage(blade.damage, byPlayer: true); MarkHit(); }
        }

        private void MarkHit() => HitUntil = Time.time + .14f;
    }
}
