using System.Collections.Generic;
using MyWorld.Core.Math;

namespace MyWorld.Core.Meshing
{
    /// <summary>网格生成的输出缓冲。不含任何 Unity 类型，由适配层负责上传。</summary>
    public sealed class MeshBuffer
    {
        /// <summary>一个 quad 固定 4 个顶点、6 个索引，<see cref="SplitByTexture"/> 依赖这个不变量。</summary>
        public const int VerticesPerQuad = 4;

        public const int IndicesPerQuad = 6;

        public readonly List<Float3> Positions = new List<Float3>();
        public readonly List<Float3> Normals = new List<Float3>();
        public readonly List<Float2> Uvs = new List<Float2>();
        public readonly List<int> Indices = new List<int>();

        /// <summary>每个 quad 一个贴图索引，下标与 quad 序号对应（顶点下标 / 4）。</summary>
        public readonly List<int> QuadTextures = new List<int>();

        // 复用的中间缓冲，避免每个区块段都产生一堆临时对象
        private readonly SortedDictionary<int, int> _quadCountsByTexture = new SortedDictionary<int, int>();
        private readonly Dictionary<int, int> _writeCursorByTexture = new Dictionary<int, int>();
        private readonly List<int> _reorderedIndices = new List<int>();

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

        /// <summary>
        /// 把索引缓冲按贴图重排，使同一张贴图的三角形连续，并输出各段的范围。
        /// <para>
        /// 只动 <see cref="Indices"/>，不动顶点与 <see cref="QuadTextures"/>——
        /// quad 序号是由顶点下标推出来的（<c>索引 / 4</c>），动了顶点侧的任何一个，对应关系就断了。
        /// 重排是稳定的，因此对已排好的缓冲再调一次结果不变。
        /// </para>
        /// </summary>
        public void SplitByTexture(List<Submesh> output)
        {
            output.Clear();
            _quadCountsByTexture.Clear();
            _writeCursorByTexture.Clear();

            if (Indices.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Indices.Count; i += IndicesPerQuad)
            {
                int texture = TextureOfTriangleGroupAt(i);
                _quadCountsByTexture.TryGetValue(texture, out int count);
                _quadCountsByTexture[texture] = count + 1;
            }

            // 前缀和定出每段起点；SortedDictionary 保证段按贴图索引升序，结果可预期
            var start = 0;
            foreach (KeyValuePair<int, int> pair in _quadCountsByTexture)
            {
                int indexCount = pair.Value * IndicesPerQuad;
                output.Add(new Submesh(pair.Key, start, indexCount));
                _writeCursorByTexture[pair.Key] = start;
                start += indexCount;
            }

            while (_reorderedIndices.Count < Indices.Count)
            {
                _reorderedIndices.Add(0);
            }

            for (int i = 0; i < Indices.Count; i += IndicesPerQuad)
            {
                int texture = TextureOfTriangleGroupAt(i);
                int target = _writeCursorByTexture[texture];

                for (var k = 0; k < IndicesPerQuad; k++)
                {
                    _reorderedIndices[target + k] = Indices[i + k];
                }

                _writeCursorByTexture[texture] = target + IndicesPerQuad;
            }

            for (var i = 0; i < Indices.Count; i++)
            {
                Indices[i] = _reorderedIndices[i];
            }
        }

        /// <summary>每组 6 个索引来自同一个 quad，取首个索引反推 quad 序号即可。</summary>
        private int TextureOfTriangleGroupAt(int indexOffset)
            => QuadTextures[Indices[indexOffset] / VerticesPerQuad];
    }
}
