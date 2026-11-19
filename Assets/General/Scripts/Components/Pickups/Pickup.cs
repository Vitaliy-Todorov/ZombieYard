using UnityEngine;

namespace ZombieYard
{
    public sealed class Pickup : MonoBehaviour
    {
        private HeldWeapon weapon;
        private AmmoConfig ammo;
        private int count;
        public bool IsAmmo => ammo != null;
        public string Title => weapon != null ? weapon.Config.title : ammo.title + " ×" + count;

        private void FixedUpdate()
        {
            if (transform.position.y < -12) Destroy(gameObject);
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
            return root.AddComponent<Pickup>();
        }

        public bool Collect(Inventory inventory)
        {
            if (!gameObject.activeSelf) return false;
            if (weapon != null && !inventory.Take(weapon)) return false;
            if (weapon == null) inventory.Add(ammo, count);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
