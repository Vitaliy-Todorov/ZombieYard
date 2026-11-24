// Ответственности
// - Создаёт порталы и планирует появление зомби на поверхности, под землёй и на арене.
// - Ведёт лимиты живых врагов, освобождает место и передаёт убийство в счёт попытки.

using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ZombieYard
{
    public sealed class ZombieSpawnService : ITickable, System.IDisposable
    {
        private ZombieSpawnerConfig settings;
        private YardGrid grid;
        private YardPlayer player;
        private ArenaEye eye;
        private readonly HashSet<ZombieAgent> living = new();
        private readonly List<ZombiePortal> surface = new();
        private readonly List<ZombiePortal> underground = new();
        private readonly List<ZombiePortal> arena = new();
        private readonly HashSet<ZombieAgent> arenaLiving = new();
        private CapsuleCollider body;
        private float remaining, undergroundRemaining, arenaRemaining;
        private readonly HashSet<ZombieAgent> undergroundLiving = new();
        private bool arenaActive;
        public int Alive => living.Count;

        private static float Interval(float min, float max) => Random.Range(min, Mathf.Max(min, max));

        internal void Begin(ZombieSpawnerConfig settings, Transform[] points, YardGrid grid, YardPlayer player, ArenaEye eye)
        {
            this.settings = settings;
            this.grid = grid;
            this.player = player;
            this.eye = eye;
            body = settings.prefab.GetComponent<CapsuleCollider>();
            remaining = Interval(settings.minInterval, settings.maxInterval);
            eye.EncounterStarted += OpenArena;
            eye.Health.Died += CloseArena;
            arenaRemaining = settings.arenaInterval;
            foreach (var point in points)
            {
                var feet = point.position;
                feet.y = grid.Height(grid.Cell(feet));
                if (!float.IsNegativeInfinity(feet.y))
                    surface.Add(ZombiePortal.Create(point, feet, settings.portalMaterial, false));
            }
            grid.StoneMined += OpenPortal;
            grid.BlockRemoved += UpdateSurfacePortals;
        }

        private void OpenArena()
        {
            arenaActive = true;
            var layout = grid.Arena;
            foreach (var feet in layout.PortalPositions())
            {
                var portal = ZombiePortal.Create(grid.transform, feet, settings.portalMaterial, false);
                portal.name = "Портал арены";
                arena.Add(portal);
            }
        }

        private void CloseArena()
        {
            foreach (var portal in arena) portal.gameObject.SetActive(false);
        }

        private void OpenPortal(Vector3Int mined)
        {
            if (player.Health.Current == 0 || settings.portalChance <= 0 || Random.value > settings.portalChance) return;
            if (!grid.Routes.TryGround(grid.WorldFeet(mined), out var floor)) return;
            var (height, radius) = WalkerRoutes.BodyCells(body, grid.Size);
            // The mined voxel must be part of the opening, including when the headroom was dug last.
            if (mined.y >= floor.y + height || !grid.Routes.Clear(floor, floor.y + height, radius)) return;
            Vector3 feet = grid.WorldFeet(floor);
            foreach (var portal in underground)
            {
                float distance = Vector3.Distance(feet, portal.transform.position);
                if (distance == 0 || distance < settings.portalSpacing) return;
            }
            if (underground.Count == 0)
                undergroundRemaining = Interval(settings.undergroundMinInterval, settings.undergroundMaxInterval);
            underground.Add(ZombiePortal.Create(grid.transform, feet, settings.portalMaterial, true));
        }

        private void UpdateSurfacePortals(Vector3Int removed)
        {
            var column = new Vector2Int(removed.x, removed.z);
            foreach (var portal in surface)
            {
                var feet = portal.transform.position;
                if (grid.Cell(feet) != column) continue;
                feet.y = grid.Height(column);
                portal.transform.position = feet;
            }
        }

        public void Tick()
        {
            if (!player || !player.Playing) return;
            if (arenaActive)
            {
                arenaRemaining -= Time.deltaTime;
                if (arenaRemaining <= 0)
                {
                    arenaRemaining = settings.arenaInterval;
                    if (Alive < settings.limit && arenaLiving.Count < settings.arenaLimit)
                    {
                        var exit = FreeExit(arena);
                        if (exit) arenaLiving.Add(Spawn(exit.position, exit.rotation, ZombieKind.Normal));
                    }
                }
                return;
            }
            if (underground.Count > 0)
            {
                undergroundRemaining -= Time.deltaTime;
                if (undergroundRemaining <= 0)
                {
                    undergroundRemaining = Interval(settings.undergroundMinInterval, settings.undergroundMaxInterval);
                    if (Alive < settings.limit && undergroundLiving.Count < settings.undergroundLimit) SpawnUnderground();
                }
            }
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = Interval(settings.minInterval, settings.maxInterval);
            if (Alive >= settings.limit || surface.Count == 0) return;
            int start = Random.Range(0, surface.Count);
            for (int i = 0; i < surface.Count; i++)
            {
                var portal = surface[(start + i) % surface.Count];
                if (!portal.isActiveAndEnabled) continue;
                Spawn(portal.transform.position, portal.transform.rotation, ZombieKind.Normal);
                break;
            }
        }

        private void SpawnUnderground()
        {
            var exit = FreeExit(underground);
            if (!exit) return;
            float chance = settings.purpleChance;
            var kind = chance > 0 && Random.value <= chance ? ZombieKind.Purple : ZombieKind.Red;
            undergroundLiving.Add(Spawn(exit.position, exit.rotation, kind));
        }

        private Transform FreeExit(List<ZombiePortal> portals)
        {
            var (height, radius) = WalkerRoutes.BodySize(body);
            var offset = Vector3.Scale(body.center, body.transform.lossyScale);
            var halfSegment = Vector3.up * (height / 2 - radius);
            int start = Random.Range(0, portals.Count);
            for (int i = 0; i < portals.Count; i++)
            {
                var portal = portals[(start + i) % portals.Count].transform;
                var feet = portal.position;
                // Leave a small skin above the supporting mesh. Occupied exits retry next interval.
                var center = feet + portal.rotation * offset;
                if (Physics.CheckCapsule(center - halfSegment, center + halfSegment, Mathf.Max(.001f, radius - .005f),
                    Physics.AllLayers, QueryTriggerInteraction.Ignore)) continue;
                return portal;
            }
            return null;
        }

        private ZombieAgent Spawn(Vector3 position, Quaternion rotation, ZombieKind kind)
        {
            var zombie = Object.Instantiate(settings.prefab, position, rotation);
            living.Add(zombie);
            zombie.Initialize(settings.zombie, grid, player, OnDeath, kind);
            return zombie;
        }

        private void OnDeath(ZombieAgent zombie)
        {
            if (!living.Remove(zombie)) return;
            arenaLiving.Remove(zombie);
            undergroundLiving.Remove(zombie);
            if (player.Health.Current > 0) player.Score.RecordKill(zombie.Kind);
        }

        public void Dispose()
        {
            if (eye)
            {
                eye.EncounterStarted -= OpenArena;
                eye.Health.Died -= CloseArena;
            }
            if (!grid) return;
            grid.StoneMined -= OpenPortal;
            grid.BlockRemoved -= UpdateSurfacePortals;
            foreach (var portal in surface) if (portal) Object.Destroy(portal.gameObject);
            foreach (var portal in underground) if (portal) Object.Destroy(portal.gameObject);
            foreach (var portal in arena) if (portal) Object.Destroy(portal.gameObject);
            undergroundLiving.Clear();
            arenaLiving.Clear();
            living.Clear();
        }
    }
}
