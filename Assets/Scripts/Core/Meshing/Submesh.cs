namespace MyWorld.Core.Meshing
{
    /// <summary>
    /// 索引缓冲里连续的一段，段内所有三角形共用同一张贴图。
    /// 对应 Unity Mesh 的一个 submesh、<c>MeshRenderer.sharedMaterials</c> 的一个槽位。
    /// </summary>
    public readonly struct Submesh
    {
        public Submesh(int textureIndex, int indexStart, int indexCount)
        {
            TextureIndex = textureIndex;
            IndexStart = indexStart;
            IndexCount = indexCount;
        }

        public int TextureIndex { get; }

        public int IndexStart { get; }

        public int IndexCount { get; }
    }
}
