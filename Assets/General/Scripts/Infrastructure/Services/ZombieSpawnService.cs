using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ZombieYard
{
    public sealed class ZombieSpawnService : IStartable, ITickable, System.IDisposable
    {
        private readonly GameLifetimeScope scene;
        private readonly HashSet<ZombieAgent> living = new();
        private readonly List<ZombiePortal> surface = new();
        private readonly List<ZombiePortal> underground = new();
        private readonly List<ZombiePortal> arena = new();
        private readonly HashSet<ZombieAgent> arenaLiving = new();
        private readonly CapsuleCollider body;
        private float remaining, undergroundRemaining, arenaRemaining;
        private int undergroundAlive;
        public int Alive => living.Count;

        public ZombieSpawnService(GameLifetimeScope scene)
        {
            this.scene = scene;
            body = scene.spawn.prefab.GetComponent<CapsuleCollider>();
            remaining = Interval(scene.spawn.minInterval, scene.spawn.maxInterval);
        }

        private static float Interval(float min, float max) => Random.Range(min, Mathf.Max(min, max));

        public void Start()
        {
            scene.Eye.EncounterStarted += OpenArena;
            scene.Eye.Health.Died += CloseArena;
            arenaRemaining = scene.spawn.arenaInterval;
            foreach (var point in scene.points)
            {
                var feet = point.position;
                feet.y = scene.grid.Height(scene.grid.Cell(feet));
                if (!float.IsNegativeInfinity(feet.y))
                    surface.Add(ZombiePortal.Create(point, feet, scene.spawn.portalMaterial, false));
            }
            scene.grid.StoneMined += OpenPortal;
            scene.grid.BlockRemoved += UpdateSurfacePortals;
        }

        private void OpenArena()
        {
            var layout = scene.grid.Arena;
            foreach (var feet in new[] { layout.Floor(1, -6), layout.Floor(1, 6), layout.Floor(7, 0) })
            {
                var portal = ZombiePortal.Create(scene.grid.transform, feet, scene.spawn.portalMaterial, false);
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
            var settings = scene.spawn;
            if (scene.player.Health.Current == 0 || settings.portalChance <= 0 || Random.value > settings.portalChance) return;
            var grid = scene.grid;
            if (!grid.TryGround((Vector3)mined * grid.Size, out var floor)) return;
            int height = Mathf.CeilToInt(body.height / grid.Size);
            int radius = Mathf.Max(0, Mathf.CeilToInt(body.radius / grid.Size - .5f));
            // The mined voxel must be part of the opening, including when the headroom was dug last.
            if (mined.y >= floor.y + height || !grid.Routes.Clear(floor, floor.y + height, radius)) return;
            Vector3 feet = (Vector3)floor * grid.Size;
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
            var grid = scene.grid;
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
            if (!scene.player.Health || scene.player.Health.Current == 0 || scene.player.Paused) return;
            var settings = scene.spawn;
            if (arena.Count > 0)
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
                    if (Alive < settings.limit && undergroundAlive < settings.undergroundLimit) SpawnUnderground();
                }
            }
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = Interval(settings.minInterval, settings.maxInterval);
            if (Alive >= scene.spawn.limit || scene.points.Length == 0) return;
            var point = scene.points[Random.Range(0, scene.points.Length)];
            Vector3 position = point.position;
            float height = scene.grid.Height(scene.grid.Cell(position));
            if (float.IsNegativeInfinity(height)) return;
            position.y = height;
            Spawn(position, point.rotation, ZombieKind.Normal);
        }

        private void SpawnUnderground()
        {
            var exit = FreeExit(underground);
            if (!exit) return;
            float chance = scene.spawn.purpleChance;
            var kind = chance > 0 && Random.value <= chance ? ZombieKind.Purple : ZombieKind.Red;
            Spawn(exit.position, exit.rotation, kind);
            undergroundAlive++;
        }

        private Transform FreeExit(List<ZombiePortal> portals)
        {
            int start = Random.Range(0, portals.Count);
            for (int i = 0; i < portals.Count; i++)
            {
                var portal = portals[(start + i) % portals.Count].transform;
                var feet = portal.position;
                // Leave a small skin above the supporting mesh. Occupied exits retry next interval.
                var bottom = feet + body.center - Vector3.up * (body.height / 2 - body.radius);
                var top = feet + body.center + Vector3.up * (body.height / 2 - body.radius);
                if (Physics.CheckCapsule(bottom, top, body.radius - .005f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore)) continue;
                return portal;
            }
            return null;
        }

        private ZombieAgent Spawn(Vector3 position, Quaternion rotation, ZombieKind kind)
        {
            var zombie = Object.Instantiate(scene.spawn.prefab, position, rotation);
            living.Add(zombie);
            zombie.Initialize(scene.spawn.zombie, scene.grid, scene.player, OnDeath, kind);
            return zombie;
        }

        private void OnDeath(ZombieAgent zombie)
        {
            if (!living.Remove(zombie)) return;
            arenaLiving.Remove(zombie);
            if (zombie.Kind != ZombieKind.Normal) undergroundAlive--;
            if (scene.player.Health.Current > 0) scene.player.Score.RecordKill(zombie.Kind);
        }

        public void Dispose()
        {
            if (scene.Eye)
            {
                scene.Eye.EncounterStarted -= OpenArena;
                scene.Eye.Health.Died -= CloseArena;
            }
            scene.grid.StoneMined -= OpenPortal;
            scene.grid.BlockRemoved -= UpdateSurfacePortals;
            foreach (var portal in surface) if (portal) Object.Destroy(portal.gameObject);
            foreach (var portal in underground) if (portal) Object.Destroy(portal.gameObject);
            foreach (var portal in arena) if (portal) Object.Destroy(portal.gameObject);
            arenaLiving.Clear();
            living.Clear();
        }
    }
}
