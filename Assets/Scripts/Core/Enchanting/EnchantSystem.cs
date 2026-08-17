using System;
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;

namespace MyWorld.Core.Enchanting
{
    /// <summary><see cref="EnchantSystem.Enchant"/> 的结果码。</summary>
    public enum EnchantResult
    {
        /// <summary>成功：经验已扣、store 已写入。</summary>
        Ok,

        /// <summary>等级越界（附魔封顶 3 级）或类型不在三种之内。</summary>
        InvalidLevel,

        /// <summary>目标槽是空的。</summary>
        SlotEmpty,

        /// <summary>目标物品不可附魔（既非工具也无攻击力）。</summary>
        NotEnchantable,

        /// <summary>总经验（Current + Level×100）不够支付，整单失败、零副作用。</summary>
        NotEnoughExp,
    }

    /// <summary><see cref="EnchantSystem.Fuse"/> 的结果码。</summary>
    public enum FuseResult
    {
        /// <summary>成功：装备带魔、书已消耗。</summary>
        Ok,

        /// <summary>选中槽不是附魔书。</summary>
        NotHoldingBook,

        /// <summary>背包里没有可附魔装备（书未消耗）。</summary>
        NoEnchantableGear,
    }

    /// <summary>
    /// 装备附魔的存储（m11 W2-2）：<b>槽位 → (类型, 等级, 物品 id)</b> 的独立字典。
    /// <para>
    /// <b>为什么不用 <see cref="ItemStack.Metadata"/>：</b>Metadata 的 16 位全部归耐久
    /// （bits 0-7 剩余 / bits 8-15 上限，m3 预留编码），一个比特都挤不出来——m10 终审修
    /// a1fb425 已经证明「附魔改写工具栈」会破坏「耐久上限唯一来源是 items 表」的不变量
    /// （占位附魔曾把铁镐 250 静默砍到 100）。因此装备附魔只进本字典，装备栈本身一位不动；
    /// 与 <see cref="MyWorld.Core.Persistence.LevelData.PlayerEnchantments"/>
    /// （Dictionary&lt;string,string&gt;，"槽位 → 附魔 id"）经
    /// <see cref="ToSaveDictionary"/> / <see cref="FromSaveDictionary"/> 对齐。
    /// </para>
    /// <para>
    /// 查询带 <b>物品 id 守卫</b>：附魔绑定「槽位 + 当时那件物品」，槽里换了别的东西
    /// 附魔自动失效（防止把镐扔进有锋烈的槽位白捡附魔）。
    /// </para>
    /// </summary>
    public sealed class EnchantStore
    {
        /// <summary>
        /// 运行时默认实例。<see cref="MyWorld.Unity.Gameplay.PlayerContext"/> 是并行波次的热点
        /// 文件挂不了字段，运行时各消费方（融合路由 / 效果查询 / 存档接线）共用这一个；
        /// EditMode 测试请 new 独立实例直注，别碰静态状态。
        /// </summary>
        public static EnchantStore Default { get; } = new EnchantStore();

        private readonly Dictionary<int, Entry> _bySlot = new Dictionary<int, Entry>();

        private readonly struct Entry
        {
            public readonly EnchantmentType Kind;
            public readonly int Level;
            public readonly int ItemId;

            public Entry(EnchantmentType kind, int level, int itemId)
            {
                Kind = kind;
                Level = level;
                ItemId = itemId;
            }
        }

        /// <summary>当前记录的附魔条数（存档往返与「失败零副作用」断言用）。</summary>
        public int Count => _bySlot.Count;

        /// <summary>查询某槽的附魔——槽里的物品必须与记录的 itemId 一致才算数。</summary>
        public bool TryGet(int slotIndex, int itemId, out EnchantmentType kind, out int level)
        {
            if (_bySlot.TryGetValue(slotIndex, out Entry entry) && entry.ItemId == itemId)
            {
                kind = entry.Kind;
                level = entry.Level;
                return true;
            }

            kind = EnchantmentType.None;
            level = 0;
            return false;
        }

        /// <summary>写入/覆盖一条附魔（同一槽再附魔 = 覆盖，v1 单附魔槽模型）。</summary>
        public void Set(int slotIndex, int itemId, EnchantmentType kind, int level)
        {
            _bySlot[slotIndex] = new Entry(kind, level, itemId);
        }

        /// <summary>清掉某槽的附魔（物品被消耗/扔掉时由接线方调用；条目过期不影响正确性，见 TryGet 守卫）。</summary>
        public void Clear(int slotIndex) => _bySlot.Remove(slotIndex);

        /// <summary>
        /// 序列化为 <see cref="MyWorld.Core.Persistence.LevelData.PlayerEnchantments"/> 的形态：
        /// key = 槽位十进制字符串，value = "<c>类型:等级:物品id</c>"（如 "sharpness:3:1402"）。
        /// </summary>
        public Dictionary<string, string> ToSaveDictionary()
        {
            var result = new Dictionary<string, string>();
            foreach (KeyValuePair<int, Entry> pair in _bySlot)
            {
                result[pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    $"{KindName(pair.Value.Kind)}:{pair.Value.Level}:{pair.Value.ItemId}";
            }

            return result;
        }

        /// <summary>
        /// 从存档字典恢复。读容忍：槽位不是数字 / 类型未知 / 等级越界 / 格式坏的行
        /// <b>跳过不炸</b>（旧档没有字段 = null → 空附魔，全新开始）。
        /// </summary>
        public static EnchantStore FromSaveDictionary(Dictionary<string, string> saved)
        {
            var store = new EnchantStore();
            if (saved == null) return store;

            foreach (KeyValuePair<string, string> pair in saved)
            {
                if (!int.TryParse(pair.Key, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int slot)
                    || slot < 0)
                {
                    continue;
                }

                string[] parts = pair.Value?.Split(':');
                if (parts == null || parts.Length != 3)
                {
                    continue;
                }

                if (!TryParseKind(parts[0], out EnchantmentType kind))
                {
                    continue;
                }

                if (!int.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int level)
                    || level < 1 || level > EnchantSystem.MaxLevel)
                {
                    continue;
                }

                if (!int.TryParse(parts[2], System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int itemId)
                    || itemId <= 0)
                {
                    continue;
                }

                store._bySlot[slot] = new Entry(kind, level, itemId);
            }

            return store;
        }

        private static string KindName(EnchantmentType kind)
            => kind switch
            {
                EnchantmentType.Sharpness => "sharpness",
                EnchantmentType.Efficiency => "efficiency",
                EnchantmentType.Unbreaking => "unbreaking",
                _ => "none",
            };

        private static bool TryParseKind(string name, out EnchantmentType kind)
        {
            switch (name)
            {
                case "sharpness":
                    kind = EnchantmentType.Sharpness;
                    return true;
                case "efficiency":
                    kind = EnchantmentType.Efficiency;
                    return true;
                case "unbreaking":
                    kind = EnchantmentType.Unbreaking;
                    return true;
                default:
                    kind = EnchantmentType.None;
                    return false;
            }
        }
    }

    /// <summary>
    /// 附魔系统（m11 W2-2）：三种附魔的经验消耗、效果数值与融合流程。
    /// <para>
    /// <b>数值（spec 第一梯队 #2）</b>：经验消耗 5/级、等级封顶 3；锋利 +1 攻/级、
    /// 效率挖速 ×1.2/级（挖掘时间 ×1/1.2^级）、耐久 +20%/级——耐用期望 ×(1+0.2L)，
    /// <b>按比例减缓扣减而非扩上限</b>：每次磨损以 5/(5+L) 概率生效（整数哈希掷骰），
    /// 耐久上限仍是 items 表 maxDurability 一个来源、上限 255 一分不加
    /// （a1fb425 的教训：「附魔绝不当耐久编码的第三条初始化路径」）。
    /// </para>
    /// <para>
    /// 全部纯函数 + 明确的结果码，Unity 侧效果接线（攻击取值 / 挖掘时间乘数 / 磨损掷骰）
    /// 查 <see cref="AttackBonus"/> / <see cref="DigTimeMultiplier"/> /
    /// <see cref="ShouldWearDurability"/>；存档接线走 <see cref="EnchantStore"/> 的字典往返。
    /// </para>
    /// </summary>
    public static class EnchantSystem
    {
        /// <summary>附魔等级封顶（spec：封顶 3 级）。</summary>
        public const int MaxLevel = 3;

        /// <summary>经验消耗：每级 5 点（1/2/3 级 = 5/10/15）。</summary>
        public const int ExpCostPerLevel = 5;

        /// <summary>锋利：每级 +1 攻击伤害。</summary>
        public const float SharpnessAttackPerLevel = 1f;

        /// <summary>效率：每级挖速 ×1.2（即挖掘时间 ×1/1.2）。</summary>
        public const float EfficiencySpeedPerLevel = 1.2f;

        /// <summary>耐久：每级耐用期望 +20%（磨损概率 1/(1+0.2L)，上限不动）。</summary>
        public const float UnbreakingDurabilityPerLevel = 0.2f;

        /// <summary>目标等级 → 经验消耗（等级越界按封顶 3 算，先经 <see cref="IsValidLevel"/> 拒绝更佳）。</summary>
        public static int ExpCostFor(int level)
            => System.Math.Clamp(level, 1, MaxLevel) * ExpCostPerLevel;

        /// <summary>等级合法（1..3）。</summary>
        public static bool IsValidLevel(int level) => level >= 1 && level <= MaxLevel;

        /// <summary>类型是否是三种可附魔之一（None / LuckOfTheSea 占位不算）。</summary>
        public static bool IsSupportedKind(EnchantmentType kind)
            => kind == EnchantmentType.Sharpness
               || kind == EnchantmentType.Efficiency
               || kind == EnchantmentType.Unbreaking;

        /// <summary>可附魔判定：工具（剑/镐/斧/锹/锄，isTool）或有攻击力的武器。泥土/书/材料一律不行。</summary>
        public static bool IsEnchantable(ItemDefinition def)
            => def != null
               && (def.IsTool || (def.AttackDamage.HasValue && def.AttackDamage.Value > 0f));

        /// <summary>
        /// 扣经验（附魔台路径的唯一扣费通道）。先扣当前级 <see cref="Experience.Current"/>，
        /// 不够就破级：一级换 99（每破一级损耗 1 点，与 m10 占位附魔 UI 的循环同语义）。
        /// 总量不够返回 false 且经验原样。
        /// </summary>
        public static bool TrySpendExperience(ref Experience xp, int amount)
        {
            if (amount <= 0) return true;

            int total = xp.Current + xp.Level * Experience.ExpPerLevel;
            if (total < amount) return false;

            int current = xp.Current;
            int level = xp.Level;
            int remaining = amount;
            while (remaining > 0)
            {
                if (current > 0)
                {
                    int take = System.Math.Min(current, remaining);
                    current -= take;
                    remaining -= take;
                }
                else if (level > 0)
                {
                    level--;
                    current = Experience.ExpPerLevel - 1; // 破级损耗 1 点
                    remaining -= 1;
                }
                else
                {
                    break;
                }
            }

            xp = new Experience(current, level);
            return true;
        }

        /// <summary>
        /// 附魔台路径：给 <paramref name="slotIndex"/> 槽的装备附
        /// (<paramref name="kind"/>, <paramref name="level"/>)。
        /// 成功 = 扣经验 + 写 store，<b>装备栈本身（含 Metadata）一位不动</b>；
        /// 任何一步不过 = 对应结果码，零副作用（不扣经验、不写 store）。
        /// </summary>
        public static EnchantResult Enchant(
            EnchantStore store, PlayerInventory inventory, ref Experience xp,
            ItemDatabase items, int slotIndex, EnchantmentType kind, int level)
        {
            if (store == null || inventory == null || items == null)
            {
                return EnchantResult.NotEnchantable; // 装配缺失：按「目标不可附魔」降级，不抛
            }

            if (!IsValidLevel(level) || !IsSupportedKind(kind))
            {
                return EnchantResult.InvalidLevel;
            }

            ItemStack stack = inventory.GetSlot(slotIndex);
            if (stack.IsEmpty)
            {
                return EnchantResult.SlotEmpty;
            }

            if (!items.TryGetByNumericId(stack.ItemId, out ItemDefinition def) || !IsEnchantable(def))
            {
                return EnchantResult.NotEnchantable;
            }

            if (!TrySpendExperience(ref xp, ExpCostFor(level)))
            {
                return EnchantResult.NotEnoughExp;
            }

            store.Set(slotIndex, stack.ItemId, kind, level);
            return EnchantResult.Ok;
        }

        // ─── 效果查询（Unity 侧接线的读数口） ─────────────────────────────

        /// <summary>锋利：攻击 +1/级；其它类型 0（<see cref="MyWorld.Unity.Combat.CombatController"/> 接线用）。</summary>
        public static float AttackBonus(EnchantmentType kind, int level)
            => kind == EnchantmentType.Sharpness && IsValidLevel(level)
                ? SharpnessAttackPerLevel * level
                : 0f;

        /// <summary>
        /// 效率：挖掘时间 × 1/1.2^级（挖速 ×1.2/级）；其它类型 1
        /// （<see cref="Blocks.BlockGating.BreakSeconds"/> 之后的时间乘数，接线用）。
        /// </summary>
        public static float DigTimeMultiplier(EnchantmentType kind, int level)
            => kind == EnchantmentType.Efficiency && IsValidLevel(level)
                ? 1f / (float)System.Math.Pow(EfficiencySpeedPerLevel, level)
                : 1f;

        /// <summary>
        /// 耐久附魔的磨损掷骰：本次消耗是否真正扣 1 点耐久。
        /// <para>
        /// 非耐久附魔恒 true（调用方零改动的直通路径）；耐久 L 级以
        /// <b>5/(5+L)</b> 概率磨损——期望耐用 = max × (1+0.2L)，即「+20%/级」。
        /// 按比例减缓扣减而非扩上限：上限仍是 items 表的 maxDurability（≤255），
        /// 附魔不改写 <see cref="ItemStack.Metadata"/> 的任何一位。
        /// <paramref name="salt"/> 由调用方给可变量（坐标/帧号等），同 salt 同结果（确定性哈希，
        /// 不持随机数对象，与 <see cref="Blocks.BlockDrops.RollCount"/> 同约定）。
        /// </para>
        /// </summary>
        public static bool ShouldWearDurability(EnchantmentType kind, int level, int salt)
        {
            if (kind != EnchantmentType.Unbreaking || !IsValidLevel(level))
            {
                return true;
            }

            return Hash(salt, 0x57) % (5 + level) < 5;
        }

        /// <summary>
        /// 未鉴定书（Metadata=0，刚从「书+青金石」配方合出）融合时掷附魔类型：
        /// 三选一（锋利/效率/耐久），确定性哈希、同 salt 同结果。
        /// </summary>
        public static EnchantmentType RollBookKind(int salt)
        {
            uint roll = Hash(salt, 0xB00C) % 3;
            return roll switch
            {
                0 => EnchantmentType.Sharpness,
                1 => EnchantmentType.Efficiency,
                _ => EnchantmentType.Unbreaking,
            };
        }

        // ─── 附魔书的 Metadata 编码（只占低 8 位，永不撞耐久位） ──────────

        /// <summary>
        /// 附魔书自带附魔的编码：bits 0-2 类型（锋利=1/效率=2/耐久=3）、bits 3-4 等级（1..3）、
        /// bits 5-15 恒 0。书没有耐久概念，但 <see cref="ItemStack.HasDurability"/> 判的是
        /// bits 8-15（耐久上限位）——编码保持高 8 位为零，带魔的书永远不会被误判成
        /// 「有耐久编码」，与耐久机制零重叠。Boss 掉落带等级的附魔书（第 3 波）走这条路。
        /// </summary>
        public static ushort EncodeBook(EnchantmentType kind, int level)
        {
            if (!IsSupportedKind(kind) || !IsValidLevel(level))
            {
                return 0; // 0 = 未鉴定书（融合时掷类型）
            }

            return (ushort)((level << 3) | (int)kind);
        }

        /// <summary>解码书编码（<see cref="EncodeBook"/> 的逆）。0 或格式坏返回 false = 未鉴定书。</summary>
        public static bool TryDecodeBook(ushort metadata, out EnchantmentType kind, out int level)
        {
            kind = (EnchantmentType)(metadata & 0x7);
            level = (metadata >> 3) & 0x3;

            if (metadata == 0 || !IsSupportedKind(kind) || !IsValidLevel(level))
            {
                kind = EnchantmentType.None;
                level = 0;
                return false;
            }

            return true;
        }

        // ─── 融合：手持附魔书右键 → 书消失、装备带魔 ──────────────────────

        /// <summary>
        /// 把 <paramref name="bookSlot"/> 槽的附魔书融合进背包里第一件可附魔装备
        /// （槽位序最小、跳过书自身）。书自带编码（<see cref="TryDecodeBook"/>）则直接用，
        /// 未鉴定书（Metadata=0）掷 <see cref="RollBookKind"/> 且等级为 1——
        /// 高级书留给附魔台（<see cref="Enchant"/>，经验消耗 5/级）与 Boss 掉落。
        /// 融合<b>不扣经验</b>（书的成本已在配方：书 + 青金石）。
        /// </summary>
        public static FuseResult Fuse(
            EnchantStore store, PlayerInventory inventory, ItemDatabase items,
            int bookSlot, int salt, out int targetSlot, out EnchantmentType kind, out int level)
        {
            targetSlot = -1;
            kind = EnchantmentType.None;
            level = 0;

            if (store == null || inventory == null || items == null
                || !items.TryGetById("enchanted_book", out ItemDefinition bookDef))
            {
                return FuseResult.NotHoldingBook; // 物品表没 enchanted_book = 功能整体让位
            }

            ItemStack book = inventory.GetSlot(bookSlot);
            if (book.IsEmpty || book.ItemId != bookDef.NumericId)
            {
                return FuseResult.NotHoldingBook;
            }

            // 找融合目标：背包槽位序最小的一件可附魔装备（书自己那一格除外）
            int found = -1;
            int foundItemId = 0;
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                if (i == bookSlot) continue;
                ItemStack candidate = inventory.GetSlot(i);
                if (candidate.IsEmpty) continue;
                if (!items.TryGetByNumericId(candidate.ItemId, out ItemDefinition def)
                    || !IsEnchantable(def))
                {
                    continue;
                }

                found = i;
                foundItemId = candidate.ItemId;
                break;
            }

            if (found < 0)
            {
                return FuseResult.NoEnchantableGear;
            }

            if (TryDecodeBook(book.Metadata, out kind, out level))
            {
                // 带编码的书：类型与等级都用编码的（不掷骰）
            }
            else
            {
                kind = RollBookKind(salt);
                level = 1;
            }

            store.Set(found, foundItemId, kind, level);
            inventory.TryRemoveOne(bookSlot); // maxStack=1：整格清空
            targetSlot = found;
            return FuseResult.Ok;
        }

        /// <summary>
        /// 确定性整数哈希（Knuth 乘 + xorshift，与 <see cref="Blocks.BlockDrops.RollCount"/>
        /// 同思路）：两个 int 进、非负 uint 出，同输入必同输出、不持随机状态。
        /// </summary>
        private static uint Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 2654435761u;
                h ^= (uint)b * 40503u;   // 揉入判别数，不同用途（磨损/掷书）互不串台
                h ^= h >> 13;
                h *= 2654435761u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
