using System.Collections.Generic;
using UnityEngine;

namespace ZombieYard
{
    internal enum Voxel { Air, Turf, Earth, Wood, Brick, Crate, Bark, Leaves, Metal, Stone, Bedrock, RoseStone, RoseBedrock, Pillar }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class YardChunk : MonoBehaviour
    {
        public const int Width = 8;
        private static readonly Vector3Int[] Normals = {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };
        private YardGrid grid;
        private Vector2Int first;
        private Mesh mesh;
        private MeshCollider surface;
        private readonly List<Vector3> vertices = new(), normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<Vector3Int> faceCells = new();
        private readonly List<int> triangles = new();

        internal void Initialize(YardGrid owner, Vector2Int origin, Material material)
        {
            grid = owner;
            first = origin;
            transform.localPosition = new Vector3(origin.x, 0, origin.y) * grid.Size;
            mesh = new Mesh { name = "Exposed voxel faces" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshRenderer>().sharedMaterial = material;
            surface = GetComponent<MeshCollider>();
            Rebuild();
        }

        internal void Rebuild()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
            faceCells.Clear();
            for (int x = first.x; x < first.x + Width; x++)
            for (int z = first.y; z < first.y + Width; z++)
            {
                int top = grid.Top(new Vector2Int(x, z));
                for (int y = grid.Bottom; y <= top; y++)
                {
                    var cell = new Vector3Int(x, y, z);
                    var kind = grid.BlockAt(cell);
                    if (kind == Voxel.Air) continue;
                    foreach (var n in Normals)
                        if (grid.BlockAt(cell + n) == Voxel.Air) AddFace(cell, n, kind);
                }
            }
            surface.sharedMesh = null;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            if (triangles.Count > 0) surface.sharedMesh = mesh;
        }

        private void AddFace(Vector3Int cell, Vector3Int normal, Voxel kind)
        {
            Vector3 n = normal;
            Vector3 right = normal.y != 0 ? Vector3.right : Vector3.Cross(Vector3.up, n);
            Vector3 up = Vector3.Cross(n, right);
            Vector3 center = new Vector3(cell.x - first.x, cell.y + .5f, cell.z - first.y) + .5f * n;
            int i = vertices.Count;
            faceCells.Add(cell);
            vertices.Add((center - (right + up) * .5f) * grid.Size);
            vertices.Add((center + (right - up) * .5f) * grid.Size);
            vertices.Add((center + (right + up) * .5f) * grid.Size);
            vertices.Add((center + (up - right) * .5f) * grid.Size);
            for (int j = 0; j < 4; j++) normals.Add(n);
            YardAtlas.AddUVs(uvs, kind, normal.y);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        public Health TargetAt(RaycastHit hit)
        {
            return grid.HealthAt(faceCells[hit.triangleIndex / 2]);
        }

        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}

