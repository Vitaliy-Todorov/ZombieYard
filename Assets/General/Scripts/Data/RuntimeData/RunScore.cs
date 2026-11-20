using System;

namespace ZombieYard
{
    public enum ZombieKind { Normal, Red, Purple }

    public sealed class RunScore
    {
        public int Balance { get; private set; }
        public int Normal { get; private set; }
        public int Red { get; private set; }
        public int Purple { get; private set; }
        public int Total => Normal + Red + Purple;

        public void RecordKill(ZombieKind kind)
        {
            switch (kind)
            {
                case ZombieKind.Normal: Normal++; Balance++; break;
                case ZombieKind.Red: Red++; Balance += 2; break;
                case ZombieKind.Purple: Purple++; Balance += 3; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public bool TryPurchase(int price, Func<bool> deliver)
        {
            if (price < 0 || Balance < price || !deliver()) return false;
            Balance -= price;
            return true;
        }
    }
}
