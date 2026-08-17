using System;
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;

namespace MyWorld.Core.Farming
{
    /// <summary>孕期到点的幼崽记录：Unity 侧（集成点②）拿去按 Position 刷 Mob、按 Scale 缩小。</summary>
    public readonly struct NewbornRecord
    {
        public readonly MobKind Kind;
        public readonly Float3 Position;
        /// <summary>幼崽缩放（固定 <see cref="BreedingSystem.BabyScale"/> = 0.5）。</summary>
        public readonly float Scale;

        public NewbornRecord(MobKind kind, Float3 position, float scale)
        {
            Kind = kind;
            Position = position;
            Scale = scale;
        }
    }

    /// <summary>
    /// 喂食繁殖系统（m11 W1-6）：同种成年两只各吃一份对应食物 → 立即配对 →
    /// 孕期 <see cref="PregnancySeconds"/> 30s → 产一条幼崽记录（scale 0.5）→
    /// 注册的幼崽 <see cref="BabyGrowSeconds"/> 600s 长大。
    /// <para>
    /// <b>零随机数</b>：全部走绝对时间阈值（照 <see cref="MyWorld.Core.Player.HungerSystem"/> 的
    /// Tick 计时语义），replay 天然可复现。Core 只管数据与计时：实体刷出、缩放视觉、
    /// 「手持食物右键喂」的交互接线都在集成点② 由 Unity 侧完成。
    /// </para>
    /// <para>
    /// 配对时机：第二只被喂的瞬间立刻配对（位置取喂食时记录的父母位置中点），
    /// 发情窗口 <see cref="FedWindowSeconds"/> 内没等到同伴则作废（照 MC 恋爱模式的节奏）。
    /// </para>
    /// </summary>
    public sealed class BreedingSystem
    {
        /// <summary>发情窗口：喂食后维持这么久，等同伴入局（秒）。</summary>
        public const float FedWindowSeconds = 30f;

        /// <summary>孕期：配对成功到产崽的间隔（秒）。</summary>
        public const float PregnancySeconds = 30f;

        /// <summary>幼崽长大耗时（秒）——10 个游戏日档位的幼儿期。</summary>
        public const float BabyGrowSeconds = 600f;

        /// <summary>幼崽缩放（长大前固定半大）。</summary>
        public const float BabyScale = 0.5f;

        /// <summary>配对半径：两只发情个体相距不超过这么远才能凑一对（米）。</summary>
        public const float PairRadius = 8f;

        private sealed class FedRecord
        {
            public MobKind Kind;
            public Float3 Position;
            public float ExpiresAt;
        }

        private sealed class Pregnancy
        {
            public MobKind Kind;
            public Float3 Position; // 出生点 = 配对时父母位置的中点
            public float DueAt;
        }

        private sealed class BabyRecord
        {
            public float BornAt;
        }

        private readonly Dictionary<int, FedRecord> _fed = new Dictionary<int, FedRecord>();
        private readonly List<Pregnancy> _pregnancies = new List<Pregnancy>();
        private readonly Dictionary<int, BabyRecord> _babies = new Dictionary<int, BabyRecord>();
        private readonly List<NewbornRecord> _newborns = new List<NewbornRecord>();
        private readonly List<int> _grown = new List<int>();

        // 配对/到期扫描的复用暂存（Tick 每帧跑，不 new）
        private readonly List<int> _scratchExpired = new List<int>();
        private readonly List<Pregnancy> _scratchDue = new List<Pregnancy>();
        private readonly List<int> _scratchGrown = new List<int>();

        /// <summary>系统内部时钟（秒），由 <see cref="Tick"/> 推进。</summary>
        public float ClockSeconds { get; private set; }

        /// <summary>当前发情中的实体 id（Unity 侧画爱心提示用）。</summary>
        public IReadOnlyCollection<int> FedEntityIds => _fed.Keys;

        /// <summary>kind 是否可繁殖：被动生物名单（村民走交易线、敌对生物不进）。</summary>
        public static bool CanBreed(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                case MobKind.Sheep:
                case MobKind.Rabbit:
                case MobKind.Fox:
                case MobKind.Deer:
                case MobKind.Panda:
                case MobKind.Penguin:
                case MobKind.Goat:
                case MobKind.Raccoon:
                case MobKind.Hamster:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// kind 对应的饲料 itemId（全部是已注册物品，守卫测试逐个校验）。
        /// 分组照 MC 惯例折算到本项目已有食物：草食系（牛/羊/鹿/山羊）吃小麦，
        /// 鸡形类（鸡/兔/仓鼠）吃麦种，杂食系（猪/狐狸/浣熊/熊猫/企鹅）吃甜菜或绿豆。
        /// 不可繁殖的 kind 返回 null。
        /// </summary>
        public static string FeedItemFor(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Cow:
                case MobKind.Sheep:
                case MobKind.Deer:
                case MobKind.Goat:
                    return "wheat";
                case MobKind.Chicken:
                case MobKind.Rabbit:
                case MobKind.Hamster:
                    return "seeds_wheat";
                case MobKind.Pig:
                case MobKind.Fox:
                case MobKind.Raccoon:
                case MobKind.Panda:
                    return "beet";
                case MobKind.Penguin:
                    return "mung_bean";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 喂食入口（Unity 右键接线②调）：饲料匹配且该实体可繁殖、非幼崽、未在发情 → 记入发情名单。
        /// 若同种另一只发情个体在配对半径内 → 立即配对（双方退出发情，起一条孕期）。
        /// 返回 false 表示这次喂食无效（调用方不应扣食物）。
        /// </summary>
        public bool TryFeed(int entityId, MobKind kind, Float3 position, string itemId)
        {
            if (!CanBreed(kind)) return false;
            if (itemId != FeedItemFor(kind)) return false;
            if (_babies.ContainsKey(entityId)) return false; // 幼崽吃了不算数
            if (_fed.ContainsKey(entityId)) return false; // 已发情，重复喂不叠加

            // 找同种、半径内的发情同伴配对（插入序扫描，确定性）。先记下同伴 id，
            // 出循环再删——枚举中改集合是隐患，哪怕紧跟 return
            int mateId = -1;
            FedRecord mateRecord = null;
            foreach (KeyValuePair<int, FedRecord> pair in _fed)
            {
                if (pair.Value.Kind != kind) continue;
                if (DistanceSquared(pair.Value.Position, position) > PairRadius * PairRadius) continue;

                mateId = pair.Key;
                mateRecord = pair.Value;
                break;
            }

            if (mateRecord != null)
            {
                _fed.Remove(mateId);
                _pregnancies.Add(new Pregnancy
                {
                    Kind = kind,
                    Position = Midpoint(mateRecord.Position, position),
                    DueAt = ClockSeconds + PregnancySeconds,
                });
                return true;
            }

            _fed[entityId] = new FedRecord
            {
                Kind = kind,
                Position = position,
                ExpiresAt = ClockSeconds + FedWindowSeconds,
            };
            return true;
        }

        /// <summary>
        /// 计时推进（每帧）：清过期发情、结算到点孕期（进新生名单）、结算长大幼崽。
        /// </summary>
        public void Tick(float dtSeconds)
        {
            ClockSeconds += dtSeconds;

            // 1. 发情窗口过期
            if (_fed.Count > 0)
            {
                _scratchExpired.Clear();
                foreach (KeyValuePair<int, FedRecord> pair in _fed)
                {
                    if (ClockSeconds >= pair.Value.ExpiresAt)
                    {
                        _scratchExpired.Add(pair.Key);
                    }
                }
                foreach (int id in _scratchExpired)
                {
                    _fed.Remove(id);
                }
                _scratchExpired.Clear();
            }

            // 2. 孕期到点 → 幼崽记录（等 Unity 侧 TakeNewborns 后自行注册幼崽实体）
            if (_pregnancies.Count > 0)
            {
                _scratchDue.Clear();
                foreach (Pregnancy pregnancy in _pregnancies)
                {
                    if (ClockSeconds >= pregnancy.DueAt)
                    {
                        _scratchDue.Add(pregnancy);
                    }
                }
                foreach (Pregnancy due in _scratchDue)
                {
                    _newborns.Add(new NewbornRecord(due.Kind, due.Position, BabyScale));
                }
                foreach (Pregnancy due in _scratchDue)
                {
                    _pregnancies.Remove(due);
                }
                _scratchDue.Clear();
            }

            // 3. 幼崽长大
            if (_babies.Count > 0)
            {
                _scratchGrown.Clear();
                foreach (KeyValuePair<int, BabyRecord> pair in _babies)
                {
                    if (ClockSeconds - pair.Value.BornAt >= BabyGrowSeconds)
                    {
                        _scratchGrown.Add(pair.Key);
                    }
                }
                foreach (int id in _scratchGrown)
                {
                    _babies.Remove(id);
                    _grown.Add(id);
                }
                _scratchGrown.Clear();
            }
        }

        /// <summary>取走已到点的幼崽记录（发完即清，不重复发）。Unity 侧拿到后刷 Mob 并 <see cref="RegisterBaby"/>。</summary>
        public IReadOnlyList<NewbornRecord> TakeNewborns()
        {
            if (_newborns.Count == 0) return Array.Empty<NewbornRecord>();

            NewbornRecord[] batch = _newborns.ToArray();
            _newborns.Clear();
            return batch;
        }

        /// <summary>把 Unity 侧刷出的幼崽实体登进系统（出生时刻 = 当前时钟）。</summary>
        public void RegisterBaby(int entityId, MobKind kind)
        {
            if (!CanBreed(kind)) return;
            _babies[entityId] = new BabyRecord { BornAt = ClockSeconds };
        }

        /// <summary>该实体是否仍是幼崽（Unity 侧按它维持 scale 0.5）。</summary>
        public bool IsBaby(int entityId) => _babies.ContainsKey(entityId);

        /// <summary>取走已长大的幼崽实体 id（发完即清）。Unity 侧恢复 scale 1。</summary>
        public IReadOnlyList<int> TakeGrownBabies()
        {
            if (_grown.Count == 0) return Array.Empty<int>();

            int[] batch = _grown.ToArray();
            _grown.Clear();
            return batch;
        }

        private static Float3 Midpoint(Float3 a, Float3 b)
            => new Float3((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f, (a.Z + b.Z) * 0.5f);

        private static float DistanceSquared(Float3 a, Float3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
