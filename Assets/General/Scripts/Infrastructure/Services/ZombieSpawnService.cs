using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ZombieYard
{
    public sealed class ZombieSpawnService : ITickable
    {
        private readonly GameLifetimeScope scene;
        private readonly HashSet<ZombieAgent> living = new();
        private float remaining;
        public int Alive => living.Count;

        public ZombieSpawnService(GameLifetimeScope scene)
        {
            this.scene = scene;
            remaining = Interval();
        }

        private float Interval() => Random.Range(scene.spawn.minInterval, Mathf.Max(scene.spawn.minInterval, scene.spawn.maxInterval));

        public void Tick()
        {
            if (!scene.player.Health || scene.player.Health.Current == 0 || scene.player.Paused) return;
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = Interval();
            if (Alive >= scene.spawn.limit || scene.points.Length == 0) return;
            var point = scene.points[Random.Range(0, scene.points.Length)];
            Vector3 position = point.position;
            float height = scene.grid.Height(scene.grid.Cell(position));
            if (float.IsNegativeInfinity(height)) return;
            position.y = height;
            var zombie = Object.Instantiate(scene.spawn.prefab, position, point.rotation);
            living.Add(zombie);
            zombie.Initialize(scene.spawn.zombie, scene.grid, scene.player, OnDeath);
        }

        private void OnDeath(ZombieAgent zombie)
        {
            if (living.Remove(zombie) && scene.player.Health.Current > 0)
                scene.player.Score.RecordKill(zombie.Kind);
        }
    }
}
