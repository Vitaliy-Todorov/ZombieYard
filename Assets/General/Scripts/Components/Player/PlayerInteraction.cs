// Ответственности
// - Определяет доступные предметы и прилавки, выполняет подбор, покупку и бросок оружия.

using UnityEngine;

namespace ZombieYard
{
    internal sealed class PlayerInteraction
    {
        private readonly CharacterController body;
        private readonly Transform eyes;
        private readonly Inventory gear;
        private readonly Health health;
        private readonly RunScore score;
        private readonly YardSounds sounds;
        private readonly System.Action<string> notice;
        private readonly float reach, voidHeight;
        private Collider[] nearbyColliders = new Collider[40];
        internal Pickup Nearby { get; private set; }
        internal ShopStand AimedStand { get; private set; }

        internal PlayerInteraction(YardPlayer player, CharacterController body, YardSounds sounds, System.Action<string> notice)
        {
            this.body = body;
            this.sounds = sounds;
            this.notice = notice;
            eyes = player.eyes.transform;
            gear = player.Gear;
            health = player.Health;
            score = player.Score;
            reach = player.config.pickupReach;
            voidHeight = player.VoidHeight;
        }

        internal void Update(bool interact, bool drop)
        {
            Nearby = CollectSuppliesAndFindWeapon();
            AimedStand = Physics.Raycast(eyes.position, eyes.forward, out var target,
                reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? target.collider.GetComponentInParent<ShopStand>() : null;
            if (interact && AimedStand)
                notice(AimedStand.Buy(score));
            else if (interact && Nearby)
            {
                if (Nearby.Collect(gear)) { Nearby = null; sounds.Play(YardCue.Take); }
                else notice("Слоты заняты · G — бросить оружие");
            }
            if (drop)
            {
                var weapon = gear.Slots[gear.Selected];
                if (weapon != null)
                {
                    if (Pickup.TryThrow(weapon, body, body.transform.forward, voidHeight))
                    {
                        gear.Drop();
                        notice("Выброшено · " + weapon.Config.title);
                    }
                    else notice("Мало места для броска");
                }
            }
        }

        private Pickup CollectSuppliesAndFindWeapon()
        {
            Vector3 chest = body.transform.TransformPoint(body.center);
            Vector3 eye = eyes.position;
            float radius = reach + Vector3.Distance(eye, chest);
            int total = Physics.OverlapSphereNonAlloc(chest, radius, nearbyColliders);
            while (total == nearbyColliders.Length)
            {
                System.Array.Resize(ref nearbyColliders, total * 2);
                total = Physics.OverlapSphereNonAlloc(chest, radius, nearbyColliders);
            }
            Pickup best = null;
            float nearest = reach;
            bool collectedAmmo = false;
            int hpBefore = health.Current;
            for (int i = 0; i < total; i++)
            {
                var collider = nearbyColliders[i];
                if (!collider.TryGetComponent<Pickup>(out var pickup)) continue;
                Vector3 origin = pickup.Automatic ? chest : eye;
                Vector3 offset = collider.bounds.center - origin;
                float distance = offset.magnitude;
                if (distance > reach) continue;
                if (Physics.Linecast(origin, collider.bounds.center, out var hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.collider != collider) continue;
                if (pickup.Automatic)
                {
                    if (pickup.Collect(gear, health)) collectedAmmo |= pickup.IsAmmo;
                }
                else if (distance <= nearest && Vector3.Dot(offset.normalized, eyes.forward) >= .25f)
                {
                    nearest = distance;
                    best = pickup;
                }
            }
            int healed = health.Current - hpBefore;
            if (collectedAmmo || healed > 0)
            {
                sounds.Play(YardCue.Take);
                notice(healed > 0 ? $"Здоровье +{healed}" : "Патроны подобраны");
            }
            return best;
        }
    }
}
