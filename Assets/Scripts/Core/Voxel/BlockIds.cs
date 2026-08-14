namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 内置方块 ID。后续由 <c>StreamingAssets/blocks/*.json</c> 注册表接管，这里只保留生成器必需的基础方块。
    /// </summary>
    public static class BlockIds
    {
        public const ushort Air = 0;
        public const ushort Stone = 1;
        public const ushort Dirt = 2;
        public const ushort Grass = 3;
        public const ushort Sand = 4;
        public const ushort Water = 5;
        public const ushort Bedrock = 6;

        /// <summary>雪原地表方块。非 0–6 内置段，与 <c>blocks/snow.json</c> 的 numericId=1008 手动保持一致。</summary>
        public const ushort Snow = 1008;
    }
}
