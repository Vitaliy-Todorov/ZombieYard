using System;
using UnityEngine;

namespace ZombieYard
{
    public enum EyePhase { Dormant, Aim, Charge, Fire, Cooldown, Defeated }

    [RequireComponent(typeof(Health), typeof(SphereCollider))]
    public sealed class ArenaEye : MonoBehaviour
    {
        [SerializeField] private Transform beam, pupilFlare;
        [SerializeField] private Light glow;
        public EyePhase Phase { get; private set; }
        public Health Health { get; private set; }
        public bool InAttackRange { get; private set; }
        public event Action EncounterStarted;

        private YardPlayer player;
        private ArenaLayout layout;
        private EyeSettings settings;
        private Vector3 aimOffset;
        private float phaseEnd, nextHit, radius;

        internal void Initialize(EyeSettings balance, YardPlayer survivor, YardGrid grid)
        {
            settings = balance;
            player = survivor;
            layout = grid.Arena;
            aimOffset = player.GetComponent<CharacterController>().center;
            radius = grid.Size * 1.25f;
            transform.position = layout.Floor(0, 0) + Vector3.up * (3.2f * grid.Size);
            transform.localScale = Vector3.one * radius;
            Health = GetComponent<Health>();
            Health.Initialize(settings.hp);
            Health.Died += Defeat;
        }

        private void Update()
        {
            if (player.Paused) return;
            if (player.Health.Current == 0)
            {
                SetBeamVisible(false);
                enabled = false;
                return;
            }

            var delta = player.transform.TransformPoint(aimOffset) - transform.position;
            bool wasInRange = InAttackRange;
            InAttackRange = delta.sqrMagnitude <= settings.laser.range * settings.laser.range;
            if (Phase == EyePhase.Dormant)
            {
                if (!layout.Contains(player.transform.position)) return;
                Begin(EyePhase.Aim, settings.aimSeconds);
                GetComponent<SphereCollider>().enabled = true;
                EncounterStarted?.Invoke();
            }
            if (!InAttackRange)
            {
                if (wasInRange) Begin(EyePhase.Aim, settings.aimSeconds);
                return;
            }
            if (!wasInRange) phaseEnd = Time.time + settings.aimSeconds;
            float speed = Phase == EyePhase.Fire ? settings.firingDegrees : settings.trackingDegrees;
            if (delta.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), speed * Time.deltaTime);
            if (Time.time >= phaseEnd) AdvancePhase();
            if (Phase == EyePhase.Charge || Phase == EyePhase.Fire) UpdateBeam();
        }

        private void AdvancePhase()
        {
            switch (Phase)
            {
                case EyePhase.Aim: Begin(EyePhase.Charge, settings.chargeSeconds); break;
                case EyePhase.Charge:
                    Begin(EyePhase.Fire, settings.fireSeconds);
                    nextHit = Time.time;
                    break;
                case EyePhase.Fire: Begin(EyePhase.Cooldown, settings.cooldownSeconds); break;
                case EyePhase.Cooldown: Begin(EyePhase.Aim, settings.aimSeconds); break;
            }
        }

        private void Begin(EyePhase phase, float duration)
        {
            Phase = phase;
            phaseEnd = Time.time + duration;
            SetBeamVisible(phase == EyePhase.Charge || phase == EyePhase.Fire);
        }

        private void SetBeamVisible(bool visible)
        {
            beam.gameObject.SetActive(visible);
            pupilFlare.gameObject.SetActive(visible);
            glow.intensity = 2;
        }

        private void UpdateBeam()
        {
            bool firing = Phase == EyePhase.Fire;
            float charge = firing ? 1 : Mathf.Clamp01(1 - (phaseEnd - Time.time) / settings.chargeSeconds);
            var ray = new Ray(transform.position, transform.forward);
            // Include the survivor, whose controller is on Ignore Raycast for their own weapons.
            bool contact = Physics.Raycast(ray, out var hit, settings.laser.range, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            float end = contact ? hit.distance : settings.laser.range;
            // The prefab cylinder is two units tall, with its Y axis pointing out of the pupil.
            float halfLength = Mathf.Max(0, end / radius - 1) * .5f;
            float width = (firing ? .44f : Mathf.Lerp(.07f, .15f, charge)) / radius;
            beam.localPosition = Vector3.forward * (1 + halfLength);
            beam.localScale = new Vector3(width, halfLength, width);
            pupilFlare.localScale = Vector3.one * Mathf.Lerp(.12f, firing ? .48f : .33f, charge);
            glow.intensity = 2 + charge * 5;
            if (!firing || Time.time < nextHit) return;
            nextHit = Time.time + settings.tickSeconds;
            if (contact) Health.AtHit(hit)?.Damage(settings.laser.damage);
        }

        private void Defeat()
        {
            Phase = EyePhase.Defeated;
            InAttackRange = false;
            gameObject.SetActive(false);
            player.Win();
        }

        private void OnDestroy()
        {
            if (Health) Health.Died -= Defeat;
        }
    }
}
