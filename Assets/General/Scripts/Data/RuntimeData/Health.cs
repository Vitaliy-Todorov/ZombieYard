using System;
using UnityEngine;

namespace ZombieYard
{
    public sealed class Health : MonoBehaviour
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public event Action Died;
        public event Action Hurt;

        public void Initialize(int maximum) => Current = Maximum = Mathf.Max(1, maximum);

        public void Damage(int amount)
        {
            if (Current <= 0 || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount);
            Hurt?.Invoke();
            if (Current == 0) Died?.Invoke();
        }
    }
}
