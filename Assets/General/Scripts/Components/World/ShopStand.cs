// Ответственности
// - Хранит цену и остаток товара на прилавке, показывает товар и подсказку.
// - Запрашивает покупку и уменьшает остаток после выдачи.

using System;
using UnityEngine;

namespace ZombieYard
{
    public sealed class ShopStand : MonoBehaviour
    {
        public string Title { get; private set; }
        public int Price { get; private set; }
        public int Remaining { get; private set; }
        public string Prompt => Remaining > 0
            ? $"E  Купить · {Title}   /   {Price} оч.   /   Осталось {Remaining}"
            : Title + "   /   Распродано";

        private Func<bool> deliver;
        private string refusal;
        private GameObject display;

        internal void Initialize(string title, ShopTerms terms, GameObject visual, Func<bool> deliver, string refusal)
        {
            Title = title;
            Price = terms.price;
            Remaining = terms.stock;
            this.deliver = deliver;
            this.refusal = refusal;
            name = "Прилавок · " + title;
            display = Instantiate(visual, transform);
            display.transform.localRotation = Quaternion.Euler(0, 75, 0);
            var parts = display.GetComponentsInChildren<Renderer>();
            var bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            float width = transform.lossyScale.x;
            float scale = width * .78f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            display.transform.localScale *= scale;
            bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            var bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            display.transform.position += transform.TransformPoint(Vector3.up * .82f) - bottomCenter;
            display.SetActive(Remaining > 0);
        }

        internal string Buy(RunScore score)
        {
            if (Remaining <= 0) return "Распродано";
            if (score.Balance < Price) return $"Не хватает {Price - score.Balance} оч.";
            if (!score.TryPurchase(Price, deliver)) return refusal;
            if (--Remaining == 0) display.SetActive(false);
            return "Куплено · " + Title;
        }
    }
}
