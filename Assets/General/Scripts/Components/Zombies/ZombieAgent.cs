using System;
using UnityEngine;

namespace ZombieYard
{
    public sealed class ZombieAgent : MonoBehaviour
    {
        public Transform leftLeg, rightLeg, torso;
        private YardGrid grid;
        private YardPlayer player;
        private ZombieConfig config;
        private Vector2Int destination;
        private Vector3 start;
        private float stepTime, duration, nextAttack, phase;
        private bool stepping, climb;
        private Action<ZombieAgent> onDeath;
        public Health Health { get; private set; }

        public void Initialize(ZombieConfig settings, YardGrid yard, YardPlayer target, Action<ZombieAgent> died)
        {
            config = settings;
            grid = yard;
            player = target;
            onDeath = died;
            Health = GetComponent<Health>();
            Health.Initialize(settings.hp);
            Health.Died += Die;
            phase = UnityEngine.Random.Range(0f, 6f);
        }

        private void Update()
        {
            if (!player || player.Health.Current == 0 || !gameObject.activeSelf) return;
            Vector3 delta = player.transform.position - transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);

            var attack = config.weapon.melee;
            if (delta.magnitude <= attack.range &&
                (!Physics.Linecast(transform.position + Vector3.up * .85f, player.transform.position + Vector3.up,
                    out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<YardPlayer>() == player))
            {
                Face(flat);
                Strike(attack);
                return;
            }

            var cell = grid.Cell(transform.position);
            if (float.IsNegativeInfinity(grid.Height(cell)) && !stepping)
            {
                transform.position += Vector3.down * (8 * Time.deltaTime);
                if (transform.position.y < grid.VoidHeight) Health.Damage(Health.Current);
                return;
            }
            if (!stepping && grid.Height(cell) < transform.position.y - .03f)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(transform.position.x, grid.Height(cell), transform.position.z), 8 * Time.deltaTime);
                return;
            }
            if (!stepping)
            {
                var target = grid.Cell(player.transform.position);
                if (!grid.NextStep(cell, target, config.maxClimbHeight, out destination)) return;
                if (cell == target)
                {
                    Face(flat);
                    transform.position += Vector3.ClampMagnitude(flat, config.speed * Time.deltaTime);
                    return;
                }
                start = transform.position;
                climb = grid.Height(destination) > start.y + .1f;
                duration = climb ? config.climbDuration : Vector3.Distance(start, grid.Center(destination)) / config.speed;
                stepTime = 0;
                stepping = true;
            }
            Vector3 end = grid.Center(destination);
            if (float.IsNegativeInfinity(end.y)) { stepping = false; return; }
            stepTime += Time.deltaTime;
            float t = Mathf.Clamp01(stepTime / Mathf.Max(.01f, duration));
            Vector3 position = Vector3.Lerp(start, end, t);
            if (climb) position.y = Mathf.Lerp(start.y, end.y, Mathf.Clamp01(t * 2.4f));
            Face(position - transform.position);
            transform.position = position;
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
            player.Health.Damage(attack.damage);
        }

        private void Die()
        {
            foreach (var entry in config.loot)
            {
                if (!entry.ammo || entry.chance <= 0 || UnityEngine.Random.value > entry.chance) continue;
                int quantity = UnityEngine.Random.Range(entry.quantity.x, entry.quantity.y + 1);
                if (quantity > 0) Pickup.ForAmmo(entry.ammo, quantity, transform.position + Vector3.up * .45f);
            }
            onDeath(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
