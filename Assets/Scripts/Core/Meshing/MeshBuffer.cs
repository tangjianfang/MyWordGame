using System.Collections.Generic;
using MyWorld.Core.Math;

namespace MyWorld.Core.Meshing
{
    /// <summary>网格生成的输出缓冲。不含任何 Unity 类型，由适配层负责上传。</summary>
    public sealed class MeshBuffer
    {
        public readonly List<Float3> Positions = new List<Float3>();
        public readonly List<Float3> Normals = new List<Float3>();
        public readonly List<Float2> Uvs = new List<Float2>();
        public readonly List<int> Indices = new List<int>();

        /// <summary>每个 quad 一个贴图索引，下标与 quad 序号对应（顶点下标 / 4）。</summary>
        public readonly List<int> QuadTextures = new List<int>();

        public int VertexCount => Positions.Count;

        public int IndexCount => Indices.Count;

        public int QuadCount => Positions.Count / 4;

        public void Clear()
        {
            Positions.Clear();
            Normals.Clear();
            Uvs.Clear();
            Indices.Clear();
            QuadTextures.Clear();
        }
    }
}
