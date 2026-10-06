using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Persistence
{
    /// <summary>
    /// 区域文件：把 32×32 个区块聚合成一个文件，减少小文件数量。只存放被改动过的区块，
    /// 未改动区块在加载时由 seed 重新生成。
    /// </summary>
    public sealed class RegionFile
    {
        public const int RegionSize = 32;
        public const int FormatVersion = 1;

        /// <summary>单个区块负载的长度上界（评审 05 T-C1）。真实区块序列化后远小于 1MB；
        /// 声明值超过它即坏档——必须<b>先校验再分配</b>，坏档不能触发 GB 级内存分配
        /// （实测 64MB 声明先分配 64MB 才报截断，int.MaxValue 会直奔 2GB/OOM）。</summary>
        public const int MaxChunkPayloadBytes = 8 * 1024 * 1024;

        private const uint Magic = 0x4752574D; // "MWRG"
        private const int RegionShift = 5;
        private const int RegionMask = RegionSize - 1;

        private readonly Dictionary<int, byte[]> _payloads = new Dictionary<int, byte[]>();

        public RegionFile(ChunkPos regionPos)
        {
            RegionPos = regionPos;
        }

        public ChunkPos RegionPos { get; }

        public int ChunkCount => _payloads.Count;

        /// <summary>算术右移保证负区块坐标向下取整，否则 -1 会错误地落进 0 号区域。</summary>
        public static ChunkPos RegionFor(ChunkPos chunk)
            => new ChunkPos(chunk.X >> RegionShift, chunk.Z >> RegionShift);

        public void StoreChunk(ChunkPos chunk, ChunkColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            _payloads[LocalIndex(chunk)] = ChunkSerializer.Serialize(column);
        }

        public bool TryGetChunk(ChunkPos chunk, out ChunkColumn column)
        {
            if (_payloads.TryGetValue(LocalIndex(chunk), out byte[] payload))
            {
                column = ChunkSerializer.Deserialize(payload);
                return true;
            }

            column = null;
            return false;
        }

        public void Save(Stream stream)
        {
            WriteInt32(stream, unchecked((int)Magic));
            WriteInt32(stream, FormatVersion);
            WriteInt32(stream, RegionPos.X);
            WriteInt32(stream, RegionPos.Z);
            WriteInt32(stream, _payloads.Count);

            foreach (KeyValuePair<int, byte[]> entry in _payloads)
            {
                WriteInt32(stream, entry.Key);
                WriteInt32(stream, entry.Value.Length);
                stream.Write(entry.Value, 0, entry.Value.Length);
            }
        }

        public static RegionFile Load(Stream stream)
        {
            if (ReadInt32(stream) != unchecked((int)Magic))
            {
                throw new InvalidDataException("不是有效的区域文件。");
            }

            int version = ReadInt32(stream);
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"不支持的区域文件版本 {version}，当前版本为 {FormatVersion}。");
            }

            int regionX = ReadInt32(stream);
            int regionZ = ReadInt32(stream);
            int count = ReadInt32(stream);

            if (count < 0 || count > RegionSize * RegionSize)
            {
                throw new InvalidDataException($"区块数量 {count} 超出合理范围。");
            }

            var region = new RegionFile(new ChunkPos(regionX, regionZ));

            for (var i = 0; i < count; i++)
            {
                int localIndex = ReadInt32(stream);
                int length = ReadInt32(stream);

                if (length < 0)
                {
                    throw new InvalidDataException("区块长度为负，文件已损坏。");
                }
                if (length > MaxChunkPayloadBytes)
                {
                    // 评审 05 T-C1：先校验再分配——坏档的离谱声明不许换走真实内存
                    throw new InvalidDataException(
                        $"区块长度 {length} 超出上界 {MaxChunkPayloadBytes} 字节，文件已损坏。");
                }

                var payload = new byte[length];
                var read = 0;
                while (read < length)
                {
                    int chunk = stream.Read(payload, read, length - read);
                    if (chunk == 0)
                    {
                        throw new InvalidDataException("区域文件被截断。");
                    }

                    read += chunk;
                }

                region._payloads[localIndex] = payload;
            }

            return region;
        }

        private int LocalIndex(ChunkPos chunk)
        {
            if (!RegionFor(chunk).Equals(RegionPos))
            {
                throw new ArgumentOutOfRangeException(nameof(chunk), chunk,
                    $"区块 {chunk} 不属于区域 {RegionPos}。");
            }

            return ((chunk.Z & RegionMask) << RegionShift) | (chunk.X & RegionMask);
        }

        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static int ReadInt32(Stream stream)
        {
            var buffer = new byte[4];
            var read = 0;
            while (read < 4)
            {
                int chunk = stream.Read(buffer, read, 4 - read);
                if (chunk == 0)
                {
                    throw new InvalidDataException("区域文件被截断。");
                }

                read += chunk;
            }

            return buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
        }
    }
}
