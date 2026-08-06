using System;
using MyWorld.Core.Math;

namespace MyWorld.Core.Physics
{
    /// <summary>
    /// 玩家包围盒与体素世界的碰撞解算。逐轴分离推进，天然获得沿墙滑动的手感。
    /// </summary>
    public static class VoxelCollision
    {
        /// <summary>解算后保留的间隙，避免浮点误差让包围盒正好贴到面上而被判定为嵌入。</summary>
        private const float Skin = 1e-4f;

        public static MoveResult Move<TSource>(TSource source, Aabb box, Float3 delta) where TSource : ISolidBlockSource
        {
            Span<float> min = stackalloc float[3] { box.Min.X, box.Min.Y, box.Min.Z };
            Span<float> max = stackalloc float[3] { box.Max.X, box.Max.Y, box.Max.Z };
            Span<float> move = stackalloc float[3] { delta.X, delta.Y, delta.Z };
            Span<bool> blocked = stackalloc bool[3];

            // Y 轴优先：落地判定依赖它，且先贴地再水平移动可避免在地面接缝处被卡住
            ResolveAxis(source, min, max, move, blocked, 1);
            ResolveAxis(source, min, max, move, blocked, 0);
            ResolveAxis(source, min, max, move, blocked, 2);

            return new MoveResult
            {
                Delta = new Float3(move[0], move[1], move[2]),
                HitX = blocked[0],
                HitY = blocked[1],
                HitZ = blocked[2],
                IsGrounded = blocked[1] && delta.Y < 0f
            };
        }

        private static void ResolveAxis<TSource>(TSource source, Span<float> min, Span<float> max,
            Span<float> move, Span<bool> blocked, int axis) where TSource : ISolidBlockSource
        {
            float delta = move[axis];
            if (delta == 0f)
            {
                return;
            }

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            int uStart = FloorToInt(min[u]);
            int uEnd = FloorToInt(max[u] - Skin);
            int vStart = FloorToInt(min[v]);
            int vEnd = FloorToInt(max[v] - Skin);

            if (delta > 0f)
            {
                int from = FloorToInt(max[axis]);
                int to = FloorToInt(max[axis] + delta);
                for (int a = from; a <= to; a++)
                {
                    if (!AnySolid(source, axis, a, u, uStart, uEnd, v, vStart, vEnd))
                    {
                        continue;
                    }

                    float allowed = a - max[axis] - Skin;
                    if (allowed < delta)
                    {
                        delta = allowed > 0f ? allowed : 0f;
                        blocked[axis] = true;
                    }

                    break;
                }
            }
            else
            {
                int from = FloorToInt(min[axis]);
                int to = FloorToInt(min[axis] + delta);
                for (int a = from; a >= to; a--)
                {
                    if (!AnySolid(source, axis, a, u, uStart, uEnd, v, vStart, vEnd))
                    {
                        continue;
                    }

                    float allowed = a + 1 - min[axis] + Skin;
                    if (allowed > delta)
                    {
                        delta = allowed < 0f ? allowed : 0f;
                        blocked[axis] = true;
                    }

                    break;
                }
            }

            move[axis] = delta;
            min[axis] += delta;
            max[axis] += delta;
        }

        private static bool AnySolid<TSource>(TSource source, int axis, int axisCell,
            int u, int uStart, int uEnd, int v, int vStart, int vEnd) where TSource : ISolidBlockSource
        {
            Span<int> cell = stackalloc int[3];
            cell[axis] = axisCell;

            for (int a = uStart; a <= uEnd; a++)
            {
                cell[u] = a;
                for (int b = vStart; b <= vEnd; b++)
                {
                    cell[v] = b;
                    if (source.IsSolidAt(cell[0], cell[1], cell[2]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int FloorToInt(float value)
        {
            var i = (int)value;
            return value < i ? i - 1 : i;
        }
    }
}
