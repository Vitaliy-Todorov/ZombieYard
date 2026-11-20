using UnityEngine;

namespace ZombieYard
{
    public sealed class ZombiePortal : MonoBehaviour
    {
        private Transform arch;
        private Light glow;

        internal static ZombiePortal Create(Transform parent, Vector3 feet, Material material, bool underground)
        {
            var portal = new GameObject(underground ? "Подземный портал" : "Портал за оградой")
                .AddComponent<ZombiePortal>();
            portal.transform.SetParent(parent, false);
            portal.transform.position = feet;
            Color color = underground ? new Color(1, .16f, .08f) : new Color(.28f, 1, .52f);
            var palette = new MaterialPropertyBlock();
            palette.SetColor("_BaseColor", color * 2.2f);
            portal.arch = portal.Ring("Арка", .48f, .84f, material, palette);
            portal.arch.localPosition = Vector3.up * .89f;
            var baseRing = portal.Ring("Основание", .48f, .48f, material, palette);
            baseRing.localRotation = Quaternion.Euler(90, 0, 0);
            baseRing.localPosition = Vector3.up * .04f;
            portal.glow = portal.arch.gameObject.AddComponent<Light>();
            portal.glow.type = LightType.Point;
            portal.glow.color = color;
            portal.glow.range = 3.4f;
            return portal;
        }

        private Transform Ring(string title, float width, float height, Material material, MaterialPropertyBlock palette)
        {
            var line = new GameObject(title).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.SetPropertyBlock(palette);
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = .065f;
            line.positionCount = 8;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI / 4;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height, 0));
            }
            return line.transform;
        }

        private void Update()
        {
            arch.Rotate(Vector3.up, 32 * Time.deltaTime);
            glow.intensity = 2 + .35f * Mathf.Sin(3 * Time.time + transform.position.x);
        }
    }
}
