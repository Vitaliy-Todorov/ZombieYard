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
        private Vector3 origin, direction;
        private float travelled, impactAge;
        private bool impacted;

        public void Launch(AmmoConfig settings, Vector3 start, Vector3 heading, Action damageFeedback)
        {
            ammo = settings;
            onDamage = damageFeedback;
            origin = start;
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

            float step = Mathf.Min(ammo.speed * Time.deltaTime, ammo.range - travelled);
            var previous = transform.position;
            bool contact = Physics.Raycast(previous, direction, out var hit, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            travelled += contact ? hit.distance : step;
            var tip = origin + direction * travelled;
            trail.SetPosition(0, tip - direction * Mathf.Min(.72f, travelled));
            trail.SetPosition(1, tip);
            transform.position = tip;
            if (contact)
            {
                impacted = true;
                transform.SetPositionAndRotation(hit.point + hit.normal * .012f, Quaternion.LookRotation(hit.normal));
                transform.localScale = new Vector3(.12f, .12f, .025f);
                var target = hit.collider.GetComponentInParent<Health>();
                if (target && target.Current > 0)
                {
                    target.Damage(ammo.damage);
                    onDamage();
                }
            }
            else if (travelled >= ammo.range) Destroy(gameObject);
        }
    }
}
