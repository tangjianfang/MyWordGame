using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Meshing
{
    /// <summary>
    /// 贪心网格合并：把朝向相同、材质相同的相邻可见面合并成尽量大的矩形，大幅降低三角形数量。
    /// </summary>
    public static class GreedyMesher
    {
        private const int Size = ChunkSection.Size;

        public static void Build<TSource>(TSource source, MeshBuffer output) where TSource : IBlockSource
        {
            output.Clear();

            var mask = new int[Size * Size];
            var cursor = new int[3];
            var step = new int[3];
            var spanU = new int[3];
            var spanV = new int[3];

            for (var axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                step[0] = step[1] = step[2] = 0;
                step[axis] = 1;

                // 从 -1 开始，使区块最外层也能与邻区块比对，正确剔除接缝处的隐藏面
                for (cursor[axis] = -1; cursor[axis] < Size;)
                {
                    BuildMask(source, mask, cursor, step, axis, u, v);
                    cursor[axis]++;
                    EmitQuads(source, output, mask, cursor, spanU, spanV, axis, u, v);
                }
            }
        }

        private static void BuildMask<TSource>(TSource source, int[] mask, int[] cursor, int[] step, int axis, int u, int v)
            where TSource : IBlockSource
        {
            var n = 0;
            for (cursor[v] = 0; cursor[v] < Size; cursor[v]++)
            {
                for (cursor[u] = 0; cursor[u] < Size; cursor[u]++, n++)
                {
                    ushort near = source.GetBlock(cursor[0], cursor[1], cursor[2]);
                    ushort far = source.GetBlock(cursor[0] + step[0], cursor[1] + step[1], cursor[2] + step[2]);

                    bool nearSolid = source.IsSolid(near);
                    bool farSolid = source.IsSolid(far);

                    if (nearSolid == farSolid)
                    {
                        mask[n] = 0;
                    }
                    else if (nearSolid)
                    {
                        // 面属于 near；near 在区域外时留给邻区块生成，避免两侧重复出面
                        mask[n] = cursor[axis] < 0 ? 0 : near;
                    }
                    else
                    {
                        mask[n] = cursor[axis] + 1 >= Size ? 0 : -far;
                    }
                }
            }
        }

        private static void EmitQuads<TSource>(TSource source, MeshBuffer output, int[] mask, int[] cursor, int[] spanU, int[] spanV, int axis, int u, int v)
            where TSource : IBlockSource
        {
            var n = 0;
            for (var j = 0; j < Size; j++)
            {
                for (var i = 0; i < Size;)
                {
                    int face = mask[n];
                    if (face == 0)
                    {
                        i++;
                        n++;
                        continue;
                    }

                    int width = 1;
                    while (i + width < Size && mask[n + width] == face)
                    {
                        width++;
                    }

                    int height = 1;
                    var blocked = false;
                    while (j + height < Size && !blocked)
                    {
                        for (var k = 0; k < width; k++)
                        {
                            if (mask[n + k + height * Size] != face)
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (!blocked)
                        {
                            height++;
                        }
                    }

                    cursor[u] = i;
                    cursor[v] = j;
                    spanU[0] = spanU[1] = spanU[2] = 0;
                    spanU[u] = width;
                    spanV[0] = spanV[1] = spanV[2] = 0;
                    spanV[v] = height;

                    // mask 的正负同时编码了朝向与归属方块：正数表示面朝 +axis 且属于 near，
                    // 负数表示面朝 -axis 且属于 far。因此一个合并出来的 quad 内贴图必然一致
                    bool facingPositive = face > 0;
                    var owner = (ushort)(facingPositive ? face : -face);
                    int textureIndex = source.GetTextureIndex(owner, BlockFaces.FromAxis(axis, facingPositive));

                    AddQuad(output, cursor, spanU, spanV, axis, facingPositive, width, height, textureIndex);

                    for (var l = 0; l < height; l++)
                    {
                        for (var k = 0; k < width; k++)
                        {
                            mask[n + k + l * Size] = 0;
                        }
                    }

                    i += width;
                    n += width;
                }
            }
        }

        private static void AddQuad(MeshBuffer output, int[] origin, int[] spanU, int[] spanV, int axis, bool facingPositive, int width, int height, int textureIndex)
        {
            int baseVertex = output.Positions.Count;

            output.QuadTextures.Add(textureIndex);

            output.Positions.Add(new Float3(origin[0], origin[1], origin[2]));
            output.Positions.Add(new Float3(origin[0] + spanU[0], origin[1] + spanU[1], origin[2] + spanU[2]));
            output.Positions.Add(new Float3(origin[0] + spanU[0] + spanV[0], origin[1] + spanU[1] + spanV[1], origin[2] + spanU[2] + spanV[2]));
            output.Positions.Add(new Float3(origin[0] + spanV[0], origin[1] + spanV[1], origin[2] + spanV[2]));

            float sign = facingPositive ? 1f : -1f;
            var normal = new Float3(axis == 0 ? sign : 0f, axis == 1 ? sign : 0f, axis == 2 ? sign : 0f);
            for (var i = 0; i < 4; i++)
            {
                output.Normals.Add(normal);
            }

            // UV 按合并后的格数铺开，配合可平铺纹理即可让大面保持单方块的贴图密度
            output.Uvs.Add(new Float2(0f, 0f));
            output.Uvs.Add(new Float2(width, 0f));
            output.Uvs.Add(new Float2(width, height));
            output.Uvs.Add(new Float2(0f, height));

            if (facingPositive)
            {
                output.Indices.Add(baseVertex);
                output.Indices.Add(baseVertex + 1);
                output.Indices.Add(baseVertex + 2);
                output.Indices.Add(baseVertex);
                output.Indices.Add(baseVertex + 2);
                output.Indices.Add(baseVertex + 3);
            }
            else
            {
                output.Indices.Add(baseVertex);
                output.Indices.Add(baseVertex + 2);
                output.Indices.Add(baseVertex + 1);
                output.Indices.Add(baseVertex);
                output.Indices.Add(baseVertex + 3);
                output.Indices.Add(baseVertex + 2);
            }
        }
    }
}
