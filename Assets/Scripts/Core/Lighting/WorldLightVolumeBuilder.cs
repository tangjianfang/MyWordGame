using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Lighting
{
    /// <summary>
    /// 带世界原点标记的采样光照体积：数组下标是<b>区域局部坐标</b>，
    /// 采样方（<c>ChunkLightSystem</c> 等）先减 <see cref="OriginX"/>/<see cref="OriginY"/>/
    /// <see cref="OriginZ"/> 再进体积——<see cref="ArrayLightVolume"/> 本身不知道自己在世界的哪里。
    /// </summary>
    public sealed class WorldLightVolume
    {
        /// <summary>光照数据本体（局部坐标）。</summary>
        public ArrayLightVolume Volume { get; }

        /// <summary>区域左下角的世界坐标（含）。</summary>
        public int OriginX { get; }

        public int OriginY { get; }

        public int OriginZ { get; }

        public WorldLightVolume(ArrayLightVolume volume, int originX, int originY, int originZ)
        {
            Volume = volume;
            OriginX = originX;
            OriginY = originY;
            OriginZ = originZ;
        }

        /// <summary>世界坐标是否落在本区域内（含边界）。</summary>
        public bool Contains(int worldX, int worldY, int worldZ)
        {
            return Volume.Contains(worldX - OriginX, worldY - OriginY, worldZ - OriginZ);
        }

        /// <summary>按世界坐标取合成后的光值（天光与方块光同一通道、取更亮者的结果）。
        /// 区域外返回 0（读容忍——不抛异常，调用方自行降级）。</summary>
        public byte GetLight(int worldX, int worldY, int worldZ)
        {
            int x = worldX - OriginX;
            int y = worldY - OriginY;
            int z = worldZ - OriginZ;
            return Volume.Contains(x, y, z) ? Volume.GetLight(x, y, z) : (byte)0;
        }
    }

    /// <summary>
    /// 从真实世界（<see cref="World"/>）与方块注册表构建采样用光照体积（m11 W1-4 集成点②）。
    /// <para>
    /// 构建顺序是 <see cref="LightPropagator.PropagateBlockLight"/> 注释锁定的契约
    /// （TorchLightPropagationTests 同款）：<b>先</b>填不透明通道 →
    /// <see cref="LightPropagator.PropagateSkyLight"/>（柱状直射会无条件覆写所在格，
    /// 必须先跑）→ 再把方块 <c>lightEmission</c>（火把=14）填进发射通道 →
    /// 最后 <see cref="LightPropagator.PropagateBlockLight"/>（只增不减，不削弱天光）。
    /// 顺序颠倒会让火把光被天光直射清零。
    /// </para>
    /// <para>
    /// 体积是<b>一个时刻的快照</b>：方块改动后由调用方按自己的节奏重建
    /// （运行时 ChunkLightSystem 定时重建；测试直接建小世界断言）。
    /// 区域外的方块读不到——光照洪泛在区域边界截断，是可接受的近似
    /// （边界取值只会偏暗，不会让暗处误亮）。
    /// </para>
    /// <para>
    /// 未加载区块的 <see cref="World.GetBlock"/> 读容忍返空气 → 视作透光。
    /// 这是构建期的近似：区块流式加载完成后调用方重建即自愈。
    /// </para>
    /// </summary>
    public static class WorldLightVolumeBuilder
    {
        /// <summary>
        /// 构建覆盖 <c>[originX, originX+sizeX) × [originY, originY+sizeY) × [originZ, originZ+sizeZ)</c>
        /// 的光照体积。三次扫过区域（不透明 → 天光 → 发射+方块光），全确定性、无随机数。
        /// </summary>
        public static WorldLightVolume Build(World world, BlockRegistry registry,
            int originX, int originY, int originZ, int sizeX, int sizeY, int sizeZ)
        {
            var volume = new ArrayLightVolume(sizeX, sizeY, sizeZ);

            // 1) 不透明通道：注册表 Opaque（挡视线语义，与网格剔面同源——水/玻璃透光）
            for (var x = 0; x < sizeX; x++)
            {
                for (var y = 0; y < sizeY; y++)
                {
                    for (var z = 0; z < sizeZ; z++)
                    {
                        ushort blockId = world.GetBlock(originX + x, originY + y, originZ + z);
                        volume.SetOpaque(x, y, z, IsOpaqueBlock(registry, blockId));
                    }
                }
            }

            // 2) 天光先行（契约顺序）：柱状直射 + BFS 洪泛
            LightPropagator.PropagateSkyLight(volume);

            // 3) 发射通道：注册表 lightEmission（火把=14、萤石类同理）
            for (var x = 0; x < sizeX; x++)
            {
                for (var y = 0; y < sizeY; y++)
                {
                    for (var z = 0; z < sizeZ; z++)
                    {
                        ushort blockId = world.GetBlock(originX + x, originY + y, originZ + z);
                        if (registry != null && registry.TryGetByNumericId(blockId, out var definition)
                            && definition.LightEmission > 0)
                        {
                            volume.SetLightEmission(x, y, z, definition.LightEmission);
                        }
                    }
                }
            }

            // 4) 方块光殿后：以发光强度为种子洪泛，与天光同通道取更亮者
            LightPropagator.PropagateBlockLight(volume);

            return new WorldLightVolume(volume, originX, originY, originZ);
        }

        /// <summary>方块是否不透光：未注册 id 与空气一律透光（读容忍，见类注释）。</summary>
        private static bool IsOpaqueBlock(BlockRegistry registry, ushort blockId)
        {
            return registry != null
                && registry.TryGetByNumericId(blockId, out var definition)
                && definition.Opaque;
        }

        /// <summary>
        /// 只铺<b>方块光</b>的体积（不跑天光）：不透明 + 发射通道 →
        /// <see cref="LightPropagator.PropagateBlockLight"/>。供「夜间采样」用——
        /// <see cref="Build"/> 的合成体积里天光与方块光共用一个字节（取更亮者），
        /// 入夜后露天格仍是 15，无法从合成值里剥出火把光；夜间刷怪判定需要的是
        /// 纯方块光，只能单独建一份（构建成本 ≈ <see cref="Build"/> 减一次天光洪泛）。
        /// </summary>
        public static WorldLightVolume BuildBlockLight(World world, BlockRegistry registry,
            int originX, int originY, int originZ, int sizeX, int sizeY, int sizeZ)
        {
            var volume = new ArrayLightVolume(sizeX, sizeY, sizeZ);
            FillOpaqueAndEmission(world, registry, volume, originX, originY, originZ, sizeX, sizeY, sizeZ);
            LightPropagator.PropagateBlockLight(volume);
            return new WorldLightVolume(volume, originX, originY, originZ);
        }

        /// <summary>一次世界扫描同时填不透明与发射两个通道（天光只读不透明、方块光只读发射，
        /// 两通道互不影响——Build 分两趟只为把契约顺序写进代码里，本方法给不分趟的调用方复用）。</summary>
        private static void FillOpaqueAndEmission(World world, BlockRegistry registry,
            ArrayLightVolume volume, int originX, int originY, int originZ,
            int sizeX, int sizeY, int sizeZ)
        {
            for (var x = 0; x < sizeX; x++)
            {
                for (var y = 0; y < sizeY; y++)
                {
                    for (var z = 0; z < sizeZ; z++)
                    {
                        ushort blockId = world.GetBlock(originX + x, originY + y, originZ + z);
                        volume.SetOpaque(x, y, z, IsOpaqueBlock(registry, blockId));
                        if (registry != null && registry.TryGetByNumericId(blockId, out var definition)
                            && definition.LightEmission > 0)
                        {
                            volume.SetLightEmission(x, y, z, definition.LightEmission);
                        }
                    }
                }
            }
        }
    }
}
