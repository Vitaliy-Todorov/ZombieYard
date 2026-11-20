using System;
using UnityEngine;

namespace ZombieYard
{
    public sealed class ZombieAgent : MonoBehaviour
    {
        public Transform leftLeg, rightLeg, torso;
        [SerializeField] private Renderer[] variantParts;
        private int strength;
        private YardGrid grid;
        private YardPlayer player;
        private ZombieConfig config;
        private Vector3Int source, destination;
        private int heightCells, radiusCells, climbCells;
        private Vector3 start;
        private float stepTime, duration, nextAttack, phase;
        private bool stepping;
        private Action<ZombieAgent> onDeath;
        public Health Health { get; private set; }
        public ZombieKind Kind { get; private set; }

        public void Initialize(ZombieConfig settings, YardGrid yard, YardPlayer target, Action<ZombieAgent> died,
            ZombieKind? kind = null)
        {
            config = settings;
            Kind = kind ?? settings.kind;
            strength = Kind switch
            {
                ZombieKind.Red => settings.redStrength,
                ZombieKind.Purple => settings.purpleStrength,
                _ => 1
            };
            if (Kind != ZombieKind.Normal)
            {
                var palette = new MaterialPropertyBlock();
                palette.SetColor("_BaseColor", Kind == ZombieKind.Red ? settings.redTint : settings.purpleTint);
                foreach (var part in variantParts) part.SetPropertyBlock(palette);
            }
            grid = yard;
            player = target;
            onDeath = died;
            var body = GetComponent<CapsuleCollider>().bounds;
            heightCells = Mathf.CeilToInt(body.size.y / yard.Size);
            radiusCells = Mathf.Max(0, Mathf.CeilToInt(body.extents.x / yard.Size - .5f));
            climbCells = Mathf.FloorToInt((config.maxClimbHeight + .001f) / yard.Size);
            Health = GetComponent<Health>();
            Health.Initialize(settings.hp * strength);
            Health.Died += Die;
            phase = UnityEngine.Random.Range(0f, 6f);
        }

        private void Update()
        {
            if (!player || player.Paused || player.Health.Current == 0 || !gameObject.activeSelf) return;
            Vector3 delta = player.transform.position - transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);

            if (stepping && (!grid.WalkerSolid(source + Vector3Int.down) ||
                             !grid.WalkerSolid(destination + Vector3Int.down)))
                stepping = false;

            if (!stepping)
            {
                bool grounded = grid.TryGround(transform.position, out source);
                float floor = grounded ? source.y * grid.Size : grid.VoidHeight - 1;
                if (!grounded || transform.position.y > floor + .03f)
                {
                    var feet = transform.position;
                    feet.y = Mathf.MoveTowards(feet.y, floor, 8 * Time.deltaTime);
                    transform.position = feet;
                    if (feet.y < grid.VoidHeight) Health.Damage(Health.Current);
                    return;
                }

                var attack = config.weapon.melee;
                if (delta.magnitude <= attack.range &&
                    (!Physics.Linecast(transform.position + Vector3.up * .85f, player.transform.position + Vector3.up,
                        out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<YardPlayer>() == player))
                {
                    Face(flat);
                    Strike(attack);
                    return;
                }

                if (!grid.TryGround(player.transform.position, out var target) ||
                    !grid.Routes.NextStep(source, target, climbCells, heightCells, radiusCells, out destination)) return;
                if (source == target)
                {
                    Face(flat);
                    transform.position += Vector3.ClampMagnitude(flat, config.speed * Time.deltaTime);
                    return;
                }
                start = transform.position;
                var goal = (Vector3)destination * grid.Size;
                float distance = Vector3.ProjectOnPlane(goal - start, Vector3.up).magnitude + Mathf.Abs(goal.y - start.y);
                duration = destination.y > source.y ? config.climbDuration : distance / config.speed;
                stepTime = 0;
                stepping = true;
            }
            Vector3 end = (Vector3)destination * grid.Size;
            // Follow the same two segments that the route checked for clearance.
            Vector3 corner = destination.y > source.y ? new Vector3(start.x, end.y, start.z)
                                                       : new Vector3(end.x, start.y, end.z);
            stepTime += Time.deltaTime;
            float t = Mathf.Clamp01(stepTime / Mathf.Max(.01f, duration));
            float first = Vector3.Distance(start, corner);
            float travel = t * (first + Vector3.Distance(corner, end));
            Face(end - start);
            transform.position = travel < first ? Vector3.MoveTowards(start, corner, travel)
                                                : Vector3.MoveTowards(corner, end, travel - first);
            stepping = t < 1;
            float stride = Mathf.Sin(Time.time * 8 + phase);
            leftLeg.localRotation = Quaternion.Euler(stride * 24, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-stride * 24, 0, 0);
            torso.localRotation = Quaternion.Euler(5, 0, stride * 3);
        }

        private void Face(Vector3 movement)
        {
            movement.y = 0;
            if (movement.sqrMagnitude < .0000001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement),
                config.turnSpeed * Time.deltaTime);
        }

        private void Strike(AttackConfig attack)
        {
            if (Time.time < nextAttack) return;
            nextAttack = Time.time + config.weapon.delay;
            player.Health.Damage(attack.damage * strength);
        }

        private void Die()
        {
            foreach (var entry in config.loot)
            {
                if (Kind < entry.minimumKind || entry.chance <= 0 || UnityEngine.Random.value > entry.chance) continue;
                var position = transform.position + Vector3.up * .45f;
                if (entry.weapon) Pickup.ForWeapon(new HeldWeapon(entry.weapon), position);
                else if (entry.partialMedkit) Pickup.ForMedkit(player.config, false, position);
                else if (entry.ammo)
                {
                    int quantity = UnityEngine.Random.Range(entry.quantity.x, entry.quantity.y + 1);
                    if (quantity > 0) Pickup.ForAmmo(entry.ammo, quantity, position);
                }
            }
            onDeath(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
