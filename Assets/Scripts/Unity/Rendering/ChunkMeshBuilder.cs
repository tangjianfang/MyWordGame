using System.Collections.Generic;
using MyWorld.Core.Math;
using MyWorld.Core.Meshing;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 把 Core 层的 <see cref="MeshBuffer"/> 拷进 Unity 的 Mesh。
    /// 静态缓存只在主线程用——本里程碑的网格生成是同步的，多线程化时这里要一起改。
    /// </summary>
    public static class ChunkMeshBuilder
    {
        private static readonly List<Vector3> ScratchPositions = new List<Vector3>();
        private static readonly List<Vector3> ScratchNormals = new List<Vector3>();
        private static readonly List<Vector2> ScratchUvs = new List<Vector2>();
        private static int[] _scratchIndices = new int[0];

        public static void Apply(MeshBuffer buffer, IReadOnlyList<Submesh> submeshes, Mesh mesh)
        {
            mesh.Clear();

            if (buffer.VertexCount == 0 || submeshes.Count == 0)
            {
                mesh.subMeshCount = 0;
                return;
            }

            CopyPositions(buffer.Positions, ScratchPositions);
            CopyPositions(buffer.Normals, ScratchNormals);
            CopyUvs(buffer.Uvs, ScratchUvs);

            if (_scratchIndices.Length < buffer.IndexCount)
            {
                _scratchIndices = new int[buffer.IndexCount];
            }

            buffer.Indices.CopyTo(_scratchIndices, 0);

            // 一个 16³ 段最坏情况下的顶点数会超过 65535，索引格式统一用 32 位省得判断
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(ScratchPositions);
            mesh.SetNormals(ScratchNormals);
            mesh.SetUVs(0, ScratchUvs);

            mesh.subMeshCount = submeshes.Count;
            for (var i = 0; i < submeshes.Count; i++)
            {
                Submesh submesh = submeshes[i];
                mesh.SetIndices(_scratchIndices, submesh.IndexStart, submesh.IndexCount,
                    MeshTopology.Triangles, i, calculateBounds: false);
            }

            mesh.RecalculateBounds();
        }

        private static void CopyPositions(List<Float3> source, List<Vector3> target)
        {
            target.Clear();
            if (target.Capacity < source.Count)
            {
                target.Capacity = source.Count;
            }

            for (var i = 0; i < source.Count; i++)
            {
                Float3 value = source[i];
                target.Add(new Vector3(value.X, value.Y, value.Z));
            }
        }

        private static void CopyUvs(List<Float2> source, List<Vector2> target)
        {
            target.Clear();
            if (target.Capacity < source.Count)
            {
                target.Capacity = source.Count;
            }

            for (var i = 0; i < source.Count; i++)
            {
                Float2 value = source[i];
                target.Add(new Vector2(value.X, value.Y));
            }
        }
    }
}
