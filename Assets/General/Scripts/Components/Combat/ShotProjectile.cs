// Ответственности
// - Ведёт полёт снаряда, включая столкновения и рикошеты.
// - Передаёт урон цели и завершает полёт по дальности, попаданию или времени жизни.

using System;
using UnityEngine;

namespace ZombieYard
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ShotProjectile : MonoBehaviour
    {
        private AmmoConfig ammo;
        private LineRenderer trail;
        private Action onDamage;
        private bool firedByPlayer;
        private Vector3 direction;
        private float travelled, impactAge, flightRemaining;
        private bool impacted;

        public void Launch(AmmoConfig settings, Vector3 start, Vector3 heading, Action damageFeedback, bool firedByPlayer = false)
        {
            this.firedByPlayer = firedByPlayer;
            ammo = settings;
            onDamage = damageFeedback;
            flightRemaining = settings.ricochetSeconds;
            direction = heading.normalized;
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            trail = GetComponent<LineRenderer>();
            trail.SetPosition(0, start);
            trail.SetPosition(1, start);
        }

        private void Update()
        {
            if (impacted)
            {
                impactAge += Time.deltaTime;
                if (impactAge >= .055f) trail.enabled = false;
                if (impactAge >= .26f) Destroy(gameObject);
                return;
            }

            if (ammo.ricochetSeconds > 0)
            {
                float dt = Mathf.Min(Time.deltaTime, flightRemaining);
                FlyRicochet(dt * ammo.speed);
                flightRemaining -= dt;
                if (flightRemaining <= 0) Destroy(gameObject);
                return;
            }
            float step = Mathf.Min(ammo.speed * Time.deltaTime, ammo.range - travelled);
            var previous = transform.position;
            bool contact = Physics.Raycast(previous, direction, out var hit, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            travelled += contact ? hit.distance : step;
            var tip = contact ? hit.point : previous + direction * step;
            trail.SetPosition(0, tip - direction * Mathf.Min(.72f, travelled));
            trail.SetPosition(1, tip);
            transform.position = tip;
            if (contact)
            {
                impacted = true;
                transform.SetPositionAndRotation(hit.point + hit.normal * .012f, Quaternion.LookRotation(hit.normal));
                transform.localScale = new Vector3(.12f, .12f, .025f);
                Damage(hit);
            }
            else if (travelled >= ammo.range) Destroy(gameObject);
        }

        private void FlyRicochet(float distance)
        {
            const float skin = .001f;
            var position = transform.position;
            // Resolve successive impacts in the same frame, including in a narrow tunnel.
            for (int i = 0; distance > 0 && i < 64; i++)
            {
                bool contact = Physics.Raycast(position, direction, out var hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float step = contact ? hit.distance : distance;
                position += direction * step;
                trail.SetPosition(0, position - direction * Mathf.Min(.55f, step));
                trail.SetPosition(1, position);
                if (!contact) break;
                Damage(hit);
                direction = Vector3.Reflect(direction, hit.normal).normalized;
                position = hit.point + hit.normal * skin;
                distance -= step + skin;
            }
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        private void Damage(RaycastHit hit)
        {
            var target = YardChunk.ResolveHit(hit);
            if (!target || target.Current <= 0) return;
            target.Damage(ammo.damage, byPlayer: firedByPlayer);
            onDamage?.Invoke();
        }
    }
}
