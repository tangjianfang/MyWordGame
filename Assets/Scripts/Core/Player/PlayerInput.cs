namespace MyWorld.Core.Player
{
    /// <summary>
    /// 一帧的移动意图。<see cref="MoveX"/>/<see cref="MoveZ"/> 是**已经旋转到世界空间**的
    /// 水平方向，长度不超过 1——Core 不知道相机朝哪，旋转由 Unity 层负责。
    /// </summary>
    public readonly struct PlayerInput
    {
        public readonly float MoveX;
        public readonly float MoveZ;
        public readonly bool Jump;
        public readonly bool Sprint;

        public PlayerInput(float moveX, float moveZ, bool jump, bool sprint)
        {
            MoveX = moveX;
            MoveZ = moveZ;
            Jump = jump;
            Sprint = sprint;
        }

        public static readonly PlayerInput None = new PlayerInput(0f, 0f, false, false);
    }
}
