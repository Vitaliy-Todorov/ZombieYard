using UnityEngine;

namespace ZombieYard
{
    public sealed class Pickup : MonoBehaviour
    {
        private HeldWeapon weapon;
        private AmmoConfig ammo;
        private int count, healing;
        private float voidHeight;
        public bool Automatic => IsAmmo || healing > 0;
        public bool IsAmmo => ammo != null;
        public string Title => healing > 0 ? (healing == int.MaxValue ? "Полная аптечка" : "Малая аптечка") :
            weapon != null ? weapon.Config.title : ammo.title + " ×" + count;

        private void FixedUpdate()
        {
            if (transform.position.y < voidHeight) Destroy(gameObject);
        }
        public static Pickup ForWeapon(HeldWeapon weapon, Vector3 position)
        {
            var pickup = Create(weapon.Config.visual, position, true);
            pickup.weapon = weapon;
            return pickup;
        }

        public static bool TryThrow(HeldWeapon weapon, CharacterController holder, Vector3 forward)
        {
            Vector3 chest = holder.transform.TransformPoint(holder.center);
            var pickup = ForWeapon(weapon, chest);
            var box = pickup.GetComponent<BoxCollider>();
            box.enabled = false;
            Vector3 half = box.size * .5f;
            float clearance = Vector3.Dot(half, new Vector3(Mathf.Abs(forward.x), 0, Mathf.Abs(forward.z)));
            float reach = holder.radius + clearance + .12f;
            Vector3 center = chest + forward * reach;
            var hits = Physics.BoxCastAll(chest, half, forward, Quaternion.identity, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (System.Array.Exists(hits, hit => hit.collider != holder) ||
                Physics.CheckBox(center, half, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                pickup.gameObject.SetActive(false);
                Destroy(pickup.gameObject);
                return false;
            }
            pickup.transform.position = center - box.center;
            box.enabled = true;
            pickup.GetComponent<Rigidbody>().linearVelocity = forward * 2.8f + Vector3.up * .8f;
            return true;
        }

        public static Pickup ForAmmo(AmmoConfig ammo, int count, Vector3 position)
        {
            var pickup = Create(ammo.pickupVisual, position, false);
            pickup.ammo = ammo;
            pickup.count = count;
            return pickup;
        }

        public static Pickup ForMedkit(PlayerConfig config, bool full, Vector3 position)
        {
            var pickup = Create(full ? config.fullMedkitVisual : config.partialMedkitVisual, position, false);
            pickup.healing = full ? int.MaxValue : Mathf.Max(1, config.partialHealing);
            pickup.name = pickup.Title;
            return pickup;
        }

        private static Pickup Create(GameObject visual, Vector3 position, bool isWeapon)
        {
            var root = new GameObject(isWeapon ? "Weapon pickup" : "Ammo pickup");
            root.transform.position = position;
            var model = Instantiate(visual, root.transform);
            model.transform.localRotation = isWeapon ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            var box = root.AddComponent<BoxCollider>();
            var bounds = new Bounds(position, Vector3.zero);
            foreach (var part in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            box.center = bounds.center - position;
            box.size = Vector3.Max(bounds.size, Vector3.one * .12f);
            root.AddComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var pickup = root.AddComponent<Pickup>();
            var grid = FindAnyObjectByType<YardGrid>();
            pickup.voidHeight = grid ? grid.VoidHeight : -12;
            return pickup;
        }

        public bool Collect(Inventory inventory, Health recipient = null)
        {
            if (!gameObject.activeSelf) return false;
            if (weapon != null && !inventory.Take(weapon)) return false;
            if (healing > 0 && (!recipient || !recipient.Heal(healing))) return false;
            if (IsAmmo) inventory.Add(ammo, count);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
