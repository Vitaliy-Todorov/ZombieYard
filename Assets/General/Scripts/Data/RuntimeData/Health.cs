using System;
using UnityEngine;

namespace ZombieYard
{
    public sealed class Health : MonoBehaviour
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool KilledByPlayer { get; private set; }
        public event Action Died;
        public event Action Hurt;

        public static Health AtHit(RaycastHit hit) =>
            hit.collider.TryGetComponent<YardChunk>(out var chunk) ? chunk.TargetAt(hit) : hit.collider.GetComponentInParent<Health>();

        public void Initialize(int maximum) => Current = Maximum = Mathf.Max(1, maximum);

        public bool Heal(int amount)
        {
            if (amount <= 0 || Current <= 0 || Current == Maximum) return false;
            Current += Mathf.Min(amount, Maximum - Current);
            return true;
        }

        public void Damage(int amount, bool byPlayer = false)
        {
            if (Current <= 0 || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount);
            KilledByPlayer = Current == 0 && byPlayer;
            Hurt?.Invoke();
            if (Current == 0) Died?.Invoke();
        }
    }
}
