using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 一个 16³ 区块段的渲染体：一个 GameObject + 一份 Mesh + 每张贴图一个材质槽。
    /// 网格顶点用段内局部坐标 [0, 16]，世界位置交给 Transform，这样同一份网格数据与位置解耦。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ChunkSectionView : MonoBehaviour
    {
        // 网格生成是同步的、一次只跑一个段，缓冲可以全局复用，省掉每段一次的分配
        private static readonly MeshBuffer SharedBuffer = new MeshBuffer();
        private static readonly List<Submesh> SharedSubmeshes = new List<Submesh>();

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private ChunkPos _chunk;
        private int _sectionIndex;

        public static ChunkSectionView Create(Transform parent, ChunkPos chunk, int sectionIndex)
        {
            var gameObject = new GameObject($"区块段 {chunk.X},{chunk.Z} #{sectionIndex}");
            gameObject.transform.SetParent(parent, worldPositionStays: false);
            gameObject.transform.localPosition = new Vector3(
                chunk.X * VoxelCoords.ChunkSize,
                VoxelCoords.MinY + sectionIndex * VoxelCoords.ChunkSize,
                chunk.Z * VoxelCoords.ChunkSize);

            var view = gameObject.AddComponent<ChunkSectionView>();
            view._chunk = chunk;
            view._sectionIndex = sectionIndex;
            view._mesh = new Mesh { name = gameObject.name };
            view.GetComponent<MeshFilter>().sharedMesh = view._mesh;
            view._renderer = view.GetComponent<MeshRenderer>();

            return view;
        }

        /// <summary>
        /// 重建网格。返回是否有可见几何——被完全包裹的段一个面都没有，调用方可以直接把它销毁。
        /// </summary>
        public bool Rebuild(World world, BlockRegistry registry, BlockMaterialLibrary materials)
        {
            int sectionBaseY = VoxelCoords.MinY + _sectionIndex * VoxelCoords.ChunkSize;
            var source = new ChunkMeshSource(world, registry, _chunk, sectionBaseY);

            GreedyMesher.Build(source, SharedBuffer);
            SharedBuffer.SplitByTexture(SharedSubmeshes);
            ChunkMeshBuilder.Apply(SharedBuffer, SharedSubmeshes, _mesh);

            var slots = new Material[SharedSubmeshes.Count];
            for (var i = 0; i < SharedSubmeshes.Count; i++)
            {
                slots[i] = materials.Get(SharedSubmeshes[i].TextureIndex);
            }

            _renderer.sharedMaterials = slots;

            return SharedSubmeshes.Count > 0;
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }
    }
}
