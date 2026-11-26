// Ответственности
// - Inventory хранит слоты, выбранное оружие и запас патронов, ведёт перезарядку.
// - HeldWeapon хранит содержимое магазина и время следующего применения конкретного оружия.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    public sealed class HeldWeapon
    {
        public readonly WeaponConfig Config;
        public AmmoConfig Ammo { get; private set; }
        public int Loaded { get; private set; }
        private float nextUse;
        public HeldWeapon(WeaponConfig config)
        {
            config.ValidateLoadout();
            Config = config;
            if (!config.UsesAmmo) return;
            Ammo = config.compatibleAmmo[0];
            Loaded = config.capacity;
        }

        internal bool TryStartUse(float now)
        {
            if (now < nextUse) return false;
            nextUse = now + Config.delay;
            return true;
        }

        public bool TryConsumeRound()
        {
            if (Loaded == 0) return false;
            Loaded--;
            return true;
        }

        internal void Load(AmmoConfig ammo, int quantity)
        {
            Ammo = ammo;
            Loaded += quantity;
        }
    }

    public sealed class Inventory
    {
        private readonly HeldWeapon[] slots = new HeldWeapon[PlayerConfig.WeaponSlots];
        public IReadOnlyList<HeldWeapon> Slots { get; }
        private readonly HeldWeapon fists;
        private readonly Dictionary<AmmoConfig, int> reserve = new();
        public int Selected { get; private set; }
        public HeldWeapon Active => slots[Selected] ?? fists;
        public bool Reloading { get; private set; }
        private float reloadEnds;

        public Inventory(PlayerConfig config)
        {
            Slots = Array.AsReadOnly(slots);
            fists = new HeldWeapon(config.fists);
            foreach (var weapon in config.startingWeapons)
                if (weapon) Take(new HeldWeapon(weapon));
            foreach (var stack in config.startingReserve) Add(stack.ammo, stack.count);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= slots.Length || index == Selected) return;
            Selected = index;
            Reloading = false;
        }

        public int Stock(AmmoConfig ammo) => ammo && reserve.TryGetValue(ammo, out int count) ? count : 0;
        public bool Add(AmmoConfig ammo, int count)
        {
            if (!ammo || count <= 0) return false;
            int current = Stock(ammo);
            if (count > int.MaxValue - current) return false;
            reserve[ammo] = current + count;
            return true;
        }

        public bool Take(HeldWeapon weapon)
        {
            int index = Array.IndexOf(slots, null);
            if (index < 0) return false;
            slots[index] = weapon;
            return true;
        }

        public HeldWeapon Drop()
        {
            var weapon = slots[Selected];
            slots[Selected] = null;
            Reloading = false;
            return weapon;
        }

        private AmmoConfig AvailableAmmo()
        {
            if (Active.Loaded > 0) return Stock(Active.Ammo) > 0 ? Active.Ammo : null;
            foreach (var ammo in Active.Config.compatibleAmmo)
                if (Stock(ammo) > 0) return ammo;
            return null;
        }

        public void StartReload(float now)
        {
            if (Reloading || !Active.Config.UsesAmmo || Active.Loaded >= Active.Config.capacity || !AvailableAmmo()) return;
            Reloading = true;
            reloadEnds = now + Active.Config.reloadSeconds;
        }

        public void Tick(float now)
        {
            if (!Reloading || now < reloadEnds) return;
            Reloading = false;
            var ammo = AvailableAmmo();
            if (!ammo) return;
            int count = Mathf.Min(Active.Config.capacity - Active.Loaded, Stock(ammo));
            Active.Load(ammo, count);
            reserve[ammo] -= count;
        }
    }
}
