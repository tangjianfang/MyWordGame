using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using Newtonsoft.Json;

namespace MyWorld.Core.Quests
{
    /// <summary>
    /// 任务链 JSON 加载器：把 <c>Assets/StreamingAssets/quests/*.json</c> 解析成 <see cref="Quest"/> 数组。
    /// <para>
    /// 模式抄 <c>MobDropTable.Load</c>/<c>MobSpawnRules.Load</c>：File.ReadAllText →
    /// DeserializeObject → 校验 → 构造。写严格——文件不存在抛 <see cref="FileNotFoundException"/>，
    /// 坏 JSON / 空链 / id 重复 / 缺字段抛 <see cref="InvalidDataException"/>，
    /// <c>condition.type</c> 非法抛 <see cref="System.ArgumentException"/>。
    /// Core 不查 ItemDatabase：悬空 itemId 引用由 C2 侧的集成守卫测试负责。
    /// </para>
    /// <para>JSON schema 详见 <c>Assets/StreamingAssets/quests/_format.md</c>。</para>
    /// </summary>
    public static class QuestChainLoader
    {
        /// <summary>加载并校验一条任务链。返回的数组顺序即任务解锁顺序，长度 ≥ 1。</summary>
        public static Quest[] Load(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"找不到任务链 JSON：{jsonPath}", jsonPath);
            }

            string json = File.ReadAllText(jsonPath);
            List<QuestDto> raw;
            try
            {
                raw = JsonConvert.DeserializeObject<List<QuestDto>>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"任务链 JSON 格式错误：{jsonPath}（{ex.Message}）", ex);
            }
            if (raw == null || raw.Count == 0)
            {
                throw new InvalidDataException($"任务链 JSON 解析为空或没有任何任务：{jsonPath}");
            }

            var seenIds = new HashSet<string>();
            var chain = new Quest[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                chain[i] = ParseQuest(raw[i], i, seenIds, jsonPath);
            }
            return chain;
        }

        private static Quest ParseQuest(QuestDto dto, int index, HashSet<string> seenIds, string jsonPath)
        {
            if (dto == null)
            {
                throw new InvalidDataException($"任务链第 {index} 个元素是 null：{jsonPath}");
            }
            if (string.IsNullOrWhiteSpace(dto.Id))
            {
                throw new InvalidDataException($"任务链第 {index} 个任务缺 id：{jsonPath}");
            }
            if (!seenIds.Add(dto.Id))
            {
                throw new InvalidDataException($"任务 id 重复：{dto.Id}（{jsonPath}）");
            }
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                throw new InvalidDataException($"任务 {dto.Id} 缺 name：{jsonPath}");
            }
            if (dto.Condition == null)
            {
                throw new InvalidDataException($"任务 {dto.Id} 缺 condition：{jsonPath}");
            }

            QuestCondition condition = ParseCondition(dto.Id, dto.Condition, jsonPath);
            if (dto.RewardExp < 0)
            {
                throw new InvalidDataException(
                    $"任务 {dto.Id} 的 rewardExp 不能为负：{dto.RewardExp}（{jsonPath}）");
            }

            return new Quest
            {
                Id = dto.Id,
                Name = dto.Name,
                Desc = string.IsNullOrEmpty(dto.Desc) ? string.Empty : dto.Desc,
                Condition = condition,
                RewardExp = dto.RewardExp,
            };
        }

        private static QuestCondition ParseCondition(string questId, ConditionDto dto, string jsonPath)
        {
            ConditionType type = ParseConditionType(dto.Type, questId, jsonPath);

            // ── 三组条件各有各的参数规则（m11 W2-4 起 12 类）────────────────────
            // 无条件参数组：只看事件类型，itemId/count/kind/weapon 写了也忽略，规范化 0/1（HUD 显示 0/1）
            if (type == ConditionType.SurviveNight || type == ConditionType.SleepInBed
                || type == ConditionType.TillSoil || type == ConditionType.EquipArmorFull
                || type == ConditionType.EnchantItem)
            {
                return new QuestCondition { Type = type, ItemId = 0, RequiredCount = 1 };
            }

            RequireCountAtLeastOne(questId, dto.Count, jsonPath);

            // 物品类条件（含播种）：itemId 必须是已注册物品 id，kind/weapon 不参与
            if (type == ConditionType.ObtainItem || type == ConditionType.CraftItem
                || type == ConditionType.SmeltItem || type == ConditionType.SowSeed)
            {
                if (dto.ItemId <= 0)
                {
                    throw new InvalidDataException(
                        $"任务 {questId} 的 condition.itemId 必须是已注册物品 id（>0），实际 {dto.ItemId}：{jsonPath}");
                }
                return new QuestCondition { Type = type, ItemId = dto.ItemId, RequiredCount = dto.Count };
            }

            // 计数动作类条件（收获/喂食/击杀）：itemId 恒 0（写了忽略），count 累计判定
            if (type == ConditionType.HarvestCrop)
            {
                return new QuestCondition { Type = type, ItemId = 0, RequiredCount = dto.Count };
            }
            if (type == ConditionType.FeedAnimal)
            {
                // kind 可选：写了就必须是合法 MobKind 名，不写 = 不限物种
                MobKind? kind = ParseOptionalKind(dto.Kind, questId, jsonPath);
                return new QuestCondition { Type = type, ItemId = 0, RequiredCount = dto.Count, Kind = kind };
            }
            // KillKind：kind 必填，weapon 可选（"bow"=箭击杀）
            MobKind? killKind = ParseOptionalKind(dto.Kind, questId, jsonPath);
            if (!killKind.HasValue)
            {
                throw new InvalidDataException(
                    $"任务 {questId} 的 condition.type=KillKind 必须写 kind（生物名，如 Skeleton/Creeper）：{jsonPath}");
            }
            string weapon = string.IsNullOrWhiteSpace(dto.Weapon) ? null : dto.Weapon.Trim();
            return new QuestCondition
            {
                Type = type,
                ItemId = 0,
                RequiredCount = dto.Count,
                Kind = killKind,
                Weapon = weapon,
            };
        }

        /// <summary>count 校验：计数类条件一律要求 ≥ 1（写严格，与物品类条件同口径）。</summary>
        private static void RequireCountAtLeastOne(string questId, int count, string jsonPath)
        {
            if (count < 1)
            {
                throw new InvalidDataException(
                    $"任务 {questId} 的 condition.count 必须 ≥ 1，实际 {count}：{jsonPath}");
            }
        }

        /// <summary>
        /// 解析可选的 kind 字段：空/null → null（不限）；写了则必须是合法
        /// <see cref="MyWorld.Core.Entities.MobKind"/> 名（大小写敏感，写严格）。
        /// </summary>
        private static MobKind? ParseOptionalKind(string name, string questId, string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            if (!System.Enum.IsDefined(typeof(MyWorld.Core.Entities.MobKind), name))
            {
                throw new System.ArgumentException(
                    $"任务 {questId} 的 condition.kind 不是合法生物名（MobKind）：{name}：{jsonPath}");
            }
            return (MyWorld.Core.Entities.MobKind)System.Enum.Parse(typeof(MyWorld.Core.Entities.MobKind), name);
        }

        private static ConditionType ParseConditionType(string name, string questId, string jsonPath)
        {
            switch (name)
            {
                case "ObtainItem": return ConditionType.ObtainItem;
                case "CraftItem": return ConditionType.CraftItem;
                case "SmeltItem": return ConditionType.SmeltItem;
                case "SurviveNight": return ConditionType.SurviveNight;
                case "SleepInBed": return ConditionType.SleepInBed;
                case "TillSoil": return ConditionType.TillSoil;
                case "SowSeed": return ConditionType.SowSeed;
                case "HarvestCrop": return ConditionType.HarvestCrop;
                case "FeedAnimal": return ConditionType.FeedAnimal;
                case "EquipArmorFull": return ConditionType.EquipArmorFull;
                case "EnchantItem": return ConditionType.EnchantItem;
                case "KillKind": return ConditionType.KillKind;
                default:
                    throw new System.ArgumentException(
                        $"任务 {questId} 的 condition.type 非法：{name}（合法的有 ObtainItem/CraftItem/SmeltItem/"
                        + "SurviveNight/SleepInBed/TillSoil/SowSeed/HarvestCrop/FeedAnimal/EquipArmorFull/EnchantItem/KillKind）：{jsonPath}");
            }
        }

        // Newtonsoft 反序列化用 DTO：属性名与 JSON 的 camelCase 显式一一对应，不受 resolver 配置影响。
        // 用属性而非字段——字段从不显式赋值会触发 CS0649（TreatWarningsAsErrors 下编译不过）。
        private sealed class QuestDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("desc")] public string Desc { get; set; }
            [JsonProperty("condition")] public ConditionDto Condition { get; set; }
            [JsonProperty("rewardExp")] public int RewardExp { get; set; }
        }

        private sealed class ConditionDto
        {
            [JsonProperty("type")] public string Type { get; set; }
            [JsonProperty("itemId")] public int ItemId { get; set; }
            [JsonProperty("count")] public int Count { get; set; }
            [JsonProperty("kind")] public string Kind { get; set; }
            [JsonProperty("weapon")] public string Weapon { get; set; }
        }
    }
}
