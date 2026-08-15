using System.Collections.Generic;
using System.IO;
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

            if (type == ConditionType.SurviveNight)
            {
                // SurviveNight 无条件参数：itemId/count 写了也被忽略，规范化为 0/1（HUD 显示 0/1）
                return new QuestCondition { Type = type, ItemId = 0, RequiredCount = 1 };
            }

            if (dto.ItemId <= 0)
            {
                throw new InvalidDataException(
                    $"任务 {questId} 的 condition.itemId 必须是已注册物品 id（>0），实际 {dto.ItemId}：{jsonPath}");
            }
            if (dto.Count < 1)
            {
                throw new InvalidDataException(
                    $"任务 {questId} 的 condition.count 必须 ≥ 1，实际 {dto.Count}：{jsonPath}");
            }
            return new QuestCondition { Type = type, ItemId = dto.ItemId, RequiredCount = dto.Count };
        }

        private static ConditionType ParseConditionType(string name, string questId, string jsonPath)
        {
            switch (name)
            {
                case "ObtainItem": return ConditionType.ObtainItem;
                case "CraftItem": return ConditionType.CraftItem;
                case "SmeltItem": return ConditionType.SmeltItem;
                case "SurviveNight": return ConditionType.SurviveNight;
                default:
                    throw new System.ArgumentException(
                        $"任务 {questId} 的 condition.type 非法：{name}（合法的有 ObtainItem/CraftItem/SmeltItem/SurviveNight）：{jsonPath}");
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
        }
    }
}
