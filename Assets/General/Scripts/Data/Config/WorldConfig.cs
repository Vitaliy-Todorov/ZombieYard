using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/World")]
    public sealed class WorldConfig : ScriptableObject
    {
        [Tooltip("Half-width and half-depth of the fenced area in grid cells.")]
        public Vector2Int halfSize = new Vector2Int(10, 8);
        [Min(.25f)] public float blockSize = 1.25f;
        public BlockConfig soil, fence, masonry, supplyCrate;
        public Material pitFloorMaterial;
    }
}
