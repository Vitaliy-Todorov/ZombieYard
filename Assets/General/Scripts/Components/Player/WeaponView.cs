// Ответственности
// - Показывает оружие в руке, анимирует движение и отдачу, предоставляет положение ствола.

using UnityEngine;

namespace ZombieYard
{
    internal sealed class WeaponView
    {
        private readonly Transform hand;
        private readonly Vector3 rest;
        private GameObject model;
        private Transform muzzle;
        private WeaponConfig shown;
        private float kick;
        internal Vector3 Muzzle => muzzle ? muzzle.position : hand.position;

        internal WeaponView(Transform hand)
        {
            this.hand = hand;
            rest = hand.localPosition;
        }

        internal void Recoil() => kick = 1;

        internal void Show(WeaponConfig weapon)
        {
            if (weapon == shown) return;
            kick = 0;
            shown = weapon;
            if (model) Object.Destroy(model);
            model = Object.Instantiate(shown.visual, hand);
            muzzle = model.transform.Find("Muzzle");
        }

        internal void Animate(float elapsed, float speed, float movement, bool sawing, bool reloading)
        {
            float cycle = Mathf.Min(.36f, shown.delay);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime / (shown.diggingMotion ? cycle : .25f));
            float bob = Mathf.Sin(elapsed * speed * 1.9f) * movement * .012f;
            float reach = sawing ? .012f * Mathf.Sin(Time.time * 94) : shown.diggingMotion ? .32f * Mathf.Sin(Mathf.PI * kick) : -kick * .09f;
            float swing = shown.diggingMotion ? 0 : (shown.UsesContactAttack ? 3 : shown.melee ? 38 : -10) * kick;
            hand.localPosition = rest + new Vector3(bob, Mathf.Abs(bob) - (reloading ? .13f : 0), reach);
            hand.localRotation = Quaternion.Euler(swing + (reloading ? 24 : 0), -6, -3);
        }
    }
}
