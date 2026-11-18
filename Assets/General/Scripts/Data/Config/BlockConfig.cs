using UnityEngine;

namespace ZombieYard
{
    [CreateAssetMenu(menuName = "Zombie Yard/Block")]
    public sealed class BlockConfig : ScriptableObject
    {
        public GameObject prefab;
        [Min(1)] public int hp = 60;
    }
}
