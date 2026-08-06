namespace MyWorld.Core.Meshing
{
    /// <summary>
    /// 网格生成的方块来源。坐标可越出 [0, 16) 到 -1 与 16，用于采样相邻区块以正确剔除接缝面。
    /// 设计为接口 + 泛型约束，使实现可以是 struct，避免热循环中的虚调用。
    /// </summary>
    public interface IBlockSource
    {
        ushort GetBlock(int x, int y, int z);

        bool IsSolid(ushort blockId);
    }
}
