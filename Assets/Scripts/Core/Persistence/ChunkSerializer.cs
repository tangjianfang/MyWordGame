using System;
using System.IO;
using System.IO.Compression;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Persistence
{
    /// <summary>
    /// 区块列的二进制序列化。只写出非空 section，未被改动的空区域完全不占空间。
    /// </summary>
    public static class ChunkSerializer
    {
        public const byte FormatVersion = 1;

        public static byte[] Serialize(ChunkColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            using var output = new MemoryStream();
            output.WriteByte(FormatVersion);

            // 非空 section 的位掩码，读取端据此知道后续有哪些数据块
            uint mask = 0;
            for (var i = 0; i < VoxelCoords.SectionCount; i++)
            {
                if (column.HasSection(i) && !column.GetSection(i).IsEmpty)
                {
                    mask |= 1u << i;
                }
            }

            WriteUInt32(output, mask);

            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var i = 0; i < VoxelCoords.SectionCount; i++)
                {
                    if ((mask & (1u << i)) == 0)
                    {
                        continue;
                    }

                    ChunkSection section = column.GetSection(i);
                    for (var y = 0; y < ChunkSection.Size; y++)
                    for (var z = 0; z < ChunkSection.Size; z++)
                    for (var x = 0; x < ChunkSection.Size; x++)
                    {
                        ushort block = section.Get(x, y, z);
                        deflate.WriteByte((byte)(block & 0xFF));
                        deflate.WriteByte((byte)(block >> 8));
                    }
                }
            }

            return output.ToArray();
        }

        public static ChunkColumn Deserialize(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length < 5)
            {
                throw new InvalidDataException("区块数据长度不足，文件可能已损坏。");
            }

            byte version = data[0];
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"不支持的区块格式版本 {version}，当前版本为 {FormatVersion}。");
            }

            uint mask = ReadUInt32(data, 1);
            var column = new ChunkColumn();

            using var input = new MemoryStream(data, 5, data.Length - 5);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);

            var buffer = new byte[ChunkSection.Volume * 2];

            for (var i = 0; i < VoxelCoords.SectionCount; i++)
            {
                if ((mask & (1u << i)) == 0)
                {
                    continue;
                }

                ReadExactly(deflate, buffer);

                ChunkSection section = column.GetOrCreateSection(i);
                var offset = 0;
                for (var y = 0; y < ChunkSection.Size; y++)
                for (var z = 0; z < ChunkSection.Size; z++)
                for (var x = 0; x < ChunkSection.Size; x++)
                {
                    var block = (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
                    offset += 2;
                    section.Set(x, y, z, block);
                }
            }

            return column;
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                int chunk;
                try
                {
                    chunk = stream.Read(buffer, read, buffer.Length - read);
                }
                catch (InvalidDataException exception)
                {
                    throw new InvalidDataException("区块数据已损坏，无法解压。", exception);
                }

                if (chunk == 0)
                {
                    throw new InvalidDataException("区块数据被截断，文件可能已损坏。");
                }

                read += chunk;
            }
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset]
                          | (data[offset + 1] << 8)
                          | (data[offset + 2] << 16)
                          | (data[offset + 3] << 24));
        }
    }
}
