namespace MyWorld.Core.Physics
{
    /// <summary>射线与碰撞查询所需的实心判定。</summary>
    public interface ISolidBlockSource
    {
        bool IsSolidAt(int x, int y, int z);
    }
}
