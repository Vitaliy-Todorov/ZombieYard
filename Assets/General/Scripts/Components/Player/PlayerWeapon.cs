// Ответственности
// - Применяет экипированное оружие, разрешает контакт ближнего боя и выпускает снаряды.

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ZombieYard
{
    internal sealed class PlayerWeapon
    {
        private readonly Inventory gear;
        private readonly Health ownerHealth;
        private readonly Transform eyes;
        private readonly WeaponView weaponView;
        private readonly YardSounds sounds;
        private readonly Action onHit;
        private readonly Action<string> notice;
        private readonly HashSet<Health> contactTargets = new();

        internal PlayerWeapon(Inventory gear, Health ownerHealth, Transform eyes, WeaponView weaponView,
            YardSounds sounds, Action onHit, Action<string> notice)
        {
            this.gear = gear;
            this.ownerHealth = ownerHealth;
            this.eyes = eyes;
            this.weaponView = weaponView;
            this.sounds = sounds;
            this.onHit = onHit;
            this.notice = notice;
        }

        internal void Attack()
        {
            var held = gear.Active;
            if (gear.Reloading || Time.time < held.NextUse) return;
            held.NextUse = Time.time + held.Config.delay;
            if (held.Config.UsesAmmo && held.Loaded == 0)
            {
                gear.StartReload(Time.time);
                if (!gear.Reloading) notice("Нет патронов");
                return;
            }
            weaponView.Recoil();
            var view = eyes;
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
                    var target = YardChunk.ResolveHit(hit);
                    if (target && target != ownerHealth) { target.Damage(held.Config.melee.damage, byPlayer: true); onHit(); }
                }
                return;
            }
            sounds.Play(YardCue.Shot);
            if (held.Config.UsesAmmo) held.Fire();
            var ammo = held.Config.staffBolt ? held.Config.staffBolt : held.Ammo;
            weaponView.Show(held.Config);
            var start = weaponView.Muzzle;
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
                UnityEngine.Object.Instantiate(ammo.projectilePrefab).Launch(ammo, start, heading, onHit, firedByPlayer: true);
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
                var target = YardChunk.ResolveHit(center);
                if (target && target != ownerHealth) contactTargets.Add(target);
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
                var target = YardChunk.ResolveHit(face);
                if (target && target != ownerHealth) contactTargets.Add(target);
            }
            foreach (var target in contactTargets)
                if (target && target.Current > 0) { target.Damage(blade.damage, byPlayer: true); onHit(); }
        }
    }
}
