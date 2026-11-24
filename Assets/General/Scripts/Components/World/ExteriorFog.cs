// Ответственности
// - Обновляет симуляцию тумана и отображает изменившиеся объёмы.
// - Отслеживает контакт игрока с туманом и наносит периодический урон.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieYard
{
    public sealed class ExteriorFog : MonoBehaviour
    {
        private readonly Dictionary<int, MeshRenderer> renderers = new();
        private readonly Dictionary<int, Texture3D> textures = new();
        private Material mist;
        private Mesh cube;
        private FogVolume volume;
        private YardGrid grid;
        private YardPlayer player;
        private float damage, damageRemainder;
        private float interval, exposure, flowInterval, flowTime;
        public bool Exposed { get; private set; }

        public void Initialize(WorldConfig config, YardGrid yard, YardPlayer survivor)
        {
            grid = yard;
            transform.SetParent(grid.transform, false);
            volume = new FogVolume(yard, config.fogDepthBelowYard);
            grid.BlockRemoved += volume.Wake;
            flowInterval = Mathf.Max(.02f, config.fogFlowInterval);
            player = survivor;
            interval = Mathf.Max(.05f, config.fogInterval);
            damage = config.fogDamage;
            player.eyes.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            mist = new Material(config.exteriorFogShader);
            mist.SetVector("_FogDimensions", new Vector4(YardChunk.Width, volume.Layers, YardChunk.Width, 0));
            mist.SetFloat("_CellSize", yard.Size);
            mist.SetColor("_MistColor", config.fogColor);
            mist.SetFloat("_MistDensity", config.fogDensity);
            RefreshChunks();
        }

        private void RefreshChunks()
        {
            foreach (int id in volume.ChangedChunks)
            {
                bool occupied = volume.HasFog(id);
                if (!renderers.TryGetValue(id, out var renderer))
                {
                    if (!occupied) continue;
                    var obj = new GameObject($"Mist chunk {id}");
                    obj.transform.SetParent(transform, false);
                    var dimensions = new Vector3(YardChunk.Width, volume.Layers, YardChunk.Width);
                    obj.transform.localScale = dimensions * grid.Size;
                    obj.transform.localPosition = volume.ChunkOrigin(id) + obj.transform.localScale * .5f;
                    obj.AddComponent<MeshFilter>().sharedMesh = cube;
                    renderer = obj.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = mist;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var texture = new Texture3D(YardChunk.Width, volume.Layers, YardChunk.Width, TextureFormat.R8, false)
                        { name = "Fog levels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_FogCells", texture);
                    properties.SetVector("_FogOrigin", grid.transform.position + volume.ChunkOrigin(id));
                    renderer.SetPropertyBlock(properties);
                    renderers.Add(id, renderer);
                    textures.Add(id, texture);
                }
                renderer.enabled = occupied;
                if (occupied) volume.Upload(id, textures[id]);
            }
            volume.ChangedChunks.Clear();
        }

        public bool Contains(Vector3 position) => volume.Contains(position);
        public float FillAt(Vector3Int cell) => volume.FillAt(cell);

        private void LateUpdate()
        {
            if (!player.Playing) return;
            flowTime += Time.deltaTime;
            while (flowTime >= flowInterval)
            {
                flowTime -= flowInterval;
                volume.Advance();
            }
            RefreshChunks();
            Exposed = Contains(player.transform.position + Vector3.up * .05f);
            if (!Exposed) { exposure = 0; return; }
            exposure += Time.deltaTime;
            while (exposure >= interval && player.Health.Current > 0)
            {
                exposure -= interval;
                damageRemainder += damage;
                int wholeHP = Mathf.RoundToInt(damageRemainder);
                damageRemainder -= wholeHP;
                player.Health.Damage(wholeHP);
            }
        }

        private void OnDestroy()
        {
            if (grid && volume != null) grid.BlockRemoved -= volume.Wake;
            foreach (var texture in textures.Values) Destroy(texture);
            if (mist) Destroy(mist);
        }
    }
}
