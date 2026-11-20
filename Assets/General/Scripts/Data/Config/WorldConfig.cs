using UnityEngine;

namespace ZombieYard
{
    [System.Serializable]
    public struct EyeSettings
    {
        public ArenaEye prefab;
        [Min(1)] public int hp;
        public AttackConfig laser;
        [Min(1)] public float trackingDegrees, firingDegrees;
        [Min(.05f)] public float aimSeconds, chargeSeconds, fireSeconds, cooldownSeconds, tickSeconds;
    }

    [System.Serializable]
    public struct ShopTerms
    {
        [Min(0)] public int price, stock;
    }

    [System.Serializable]
    public struct AmmoDeal
    {
        public AmmoStack pack;
        public ShopTerms terms;
    }

    [CreateAssetMenu(menuName = "Zombie Yard/World")]
    public sealed class WorldConfig : ScriptableObject
    {
        [Tooltip("Half-width and half-depth of the fenced area in grid cells.")]
        public Vector2Int halfSize = new Vector2Int(10, 8);
        [Min(.25f)] public float blockSize = 1.25f;
        public BlockConfig soil, fence, masonry, supplyCrate;
        [Header("Underground shop")]
        public GameObject shopCounter;
        public AmmoDeal shells, rifleRounds;
        public ShopTerms partialMedkit, fullMedkit, staff;
        [Header("Eye in the red mountain")]
        public EyeSettings eye;
        public BlockConfig arenaPillar;
        [Header("Terrain and fog")]
        [Min(1)] public int plateauHeight = 4;
        [Min(10)] public int mountainDistance = 44;
        [Tooltip("Waterline depth below world zero, in blocks.")]
        [Min(1)] public int fogDepthBelowYard = 2;
        [Min(.02f)] public float fogFlowInterval = .12f;
        [Min(1)] public int subsoilDepth = 13;
        [Min(1)] public int subsoilHpMultiplier = 4;
        [UnityEngine.Serialization.FormerlySerializedAs("pitFloorMaterial")]
        public Material terrainMaterial;
        public Shader exteriorFogShader;
        public Color fogColor = new(.55f, .66f, .56f);
        [Min(.01f)] public float fogDensity = .13f;
        [Min(1)] public float fogDamage = 14.4f;
        [Min(.05f)] public float fogInterval = 1;
    }
}
