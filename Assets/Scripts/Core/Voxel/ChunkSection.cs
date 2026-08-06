using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 16³ 的方块存储单元，采用局部调色板 + 位打包，随不同方块数量自适应升位。
    /// </summary>
    public sealed class ChunkSection
    {
        public const int Size = 16;
        public const int Volume = Size * Size * Size;
        public const ushort AirId = 0;

        private const int BitsPerLong = 64;

        private ushort[] _palette = { AirId };
        private readonly Dictionary<ushort, int> _paletteLookup = new Dictionary<ushort, int> { { AirId, 0 } };
        private int _paletteCount = 1;

        // 位宽为 0 表示整个 section 同一种方块，此时不分配存储数组
        private int _bitsPerEntry;
        private ulong[] _data;
        private int _nonAirCount;

        public bool IsUniform => _bitsPerEntry == 0;

        public bool IsEmpty => _nonAirCount == 0;

        public int PaletteCount => _paletteCount;

        public int BitsPerEntry => _bitsPerEntry;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int x, int y, int z) => (y << 8) | (z << 4) | x;

        public ushort Get(int x, int y, int z)
        {
            Validate(x, nameof(x));
            Validate(y, nameof(y));
            Validate(z, nameof(z));
            return _palette[ReadEntry(Index(x, y, z))];
        }

        public void Set(int x, int y, int z, ushort blockId)
        {
            Validate(x, nameof(x));
            Validate(y, nameof(y));
            Validate(z, nameof(z));

            int index = Index(x, y, z);
            ushort previous = _palette[ReadEntry(index)];
            if (previous == blockId)
            {
                return;
            }

            int paletteIndex = GetOrAddPaletteIndex(blockId);
            WriteEntry(_data, _bitsPerEntry, index, paletteIndex);

            if (previous == AirId)
            {
                _nonAirCount++;
            }
            else if (blockId == AirId)
            {
                _nonAirCount--;
            }
        }

        private static void Validate(int value, string name)
        {
            if ((uint)value >= Size)
            {
                throw new ArgumentOutOfRangeException(name, value, $"坐标必须落在 [0, {Size}) 内。");
            }
        }

        /// <summary>只使用能整除 64 的位宽，保证单个条目不会跨越 ulong 边界。</summary>
        private static int RequiredBits(int paletteCount)
        {
            if (paletteCount <= 1) return 0;
            if (paletteCount <= 2) return 1;
            if (paletteCount <= 4) return 2;
            if (paletteCount <= 16) return 4;
            if (paletteCount <= 256) return 8;
            return 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadEntry(int index)
        {
            if (_bitsPerEntry == 0)
            {
                return 0;
            }

            int entriesPerLong = BitsPerLong / _bitsPerEntry;
            int offset = (index % entriesPerLong) * _bitsPerEntry;
            ulong mask = (1UL << _bitsPerEntry) - 1UL;
            return (int)((_data[index / entriesPerLong] >> offset) & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteEntry(ulong[] data, int bitsPerEntry, int index, int value)
        {
            int entriesPerLong = BitsPerLong / bitsPerEntry;
            int slot = index / entriesPerLong;
            int offset = (index % entriesPerLong) * bitsPerEntry;
            ulong mask = (1UL << bitsPerEntry) - 1UL;
            data[slot] = (data[slot] & ~(mask << offset)) | (((ulong)value & mask) << offset);
        }

        private int GetOrAddPaletteIndex(ushort blockId)
        {
            if (_paletteLookup.TryGetValue(blockId, out int existing))
            {
                return existing;
            }

            int required = RequiredBits(_paletteCount + 1);
            if (required > _bitsPerEntry)
            {
                Grow(required);
            }

            if (_paletteCount == _palette.Length)
            {
                Array.Resize(ref _palette, _palette.Length * 2);
            }

            int newIndex = _paletteCount;
            _palette[_paletteCount++] = blockId;
            _paletteLookup[blockId] = newIndex;
            return newIndex;
        }

        private void Grow(int newBits)
        {
            var newData = new ulong[Volume / (BitsPerLong / newBits)];

            // 位宽为 0 时全部条目都是调色板 0 号，新数组的零值即为正确结果，无需搬运
            if (_bitsPerEntry > 0)
            {
                for (var i = 0; i < Volume; i++)
                {
                    WriteEntry(newData, newBits, i, ReadEntry(i));
                }
            }

            _data = newData;
            _bitsPerEntry = newBits;
        }
    }
}
