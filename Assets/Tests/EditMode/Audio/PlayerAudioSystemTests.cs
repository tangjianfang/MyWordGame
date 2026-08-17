#if UNITY_EDITOR
// PlayerAudioSystem 是 nice-to-have：AudioClip 缺失时不能阻断游戏（debug.LogWarning 后跳过即可）。
// 这两个 fixture 验证缺失 clip 的两条路径（footstep / place），break 走同一条 PlayClip，足够覆盖。
// m9 B1 fix1 追加挂载顺序的结构断言（见 MountOrder 测试注释）。
// av W1-6 追加 11 个事件音 + 静态 Instance 契约。
using System.Reflection;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Audio
{
    [TestFixture]
    public class PlayerAudioSystemTests
    {
        [Test]
        public void PlayFootstep_DoesNotThrowWhenClipMissing()
        {
            var go = new GameObject("PlayerAudio_FootstepTest");
            try
            {
                var audio = go.AddComponent<PlayerAudioSystem>();
                // 不挂 clip，调 PlayFootstep 应不抛
                Assert.DoesNotThrow(() => audio.PlayFootstep(),
                    "footstep clip 缺失时也不应抛异常（nice-to-have 不能阻断游戏）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayPlace_DoesNotThrowWhenClipMissing()
        {
            var go = new GameObject("PlayerAudio_PlaceTest");
            try
            {
                var audio = go.AddComponent<PlayerAudioSystem>();
                Assert.DoesNotThrow(() => audio.PlayPlace(),
                    "place clip 缺失时也不应抛异常（nice-to-have 不能阻断游戏）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// m9 B1 fix1：挂载顺序结构断言。PlayerController.Awake 与 BlockInteraction.Bind
        /// 都在回调里<b>一次性缓存</b> _audio 引用（之后不再重查）——PlayerAudioSystem
        /// 必须先于两者挂到宿主上（WorldBootstrap 步骤 7.5），否则 footstep/place/break
        /// 全哑（B1 首版把它挂在 10.5、晚于两处缓存点的真实 bug）。
        /// 本测试按修正后的顺序复现「先挂音频 → 再挂玩家组件」并反射检查两处缓存
        /// 均非 null。EditMode 驱动不了整个 WorldBootstrap.Awake（会生成整个世界），
        /// 故 WorldBootstrap 侧的顺序由挂载点注释 + 本契约测试共同守。
        /// </summary>
        [Test]
        public void MountOrder_PlayerComponentsCacheAudio_WhenAudioMountedFirst()
        {
            var go = new GameObject("PlayerAudio_OrderTest");
            try
            {
                // 修正后的顺序：音频先挂（EditMode 下 AddComponent 不触发各自 Awake，顺序即挂载序）
                go.AddComponent<PlayerAudioSystem>();

                // 消费方一：PlayerController.Awake 里 GetComponent 缓存
                var player = go.AddComponent<PlayerController>();
                var playerAwake = typeof(PlayerController).GetMethod(
                    "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Assume.That(playerAwake, Is.Not.Null, "PlayerController 应有私有 Awake");
                playerAwake.Invoke(player, null);
                var playerAudio = typeof(PlayerController).GetField(
                    "_audio", BindingFlags.Instance | BindingFlags.NonPublic);
                Assume.That(playerAudio, Is.Not.Null, "PlayerController 应有 _audio 字段");
                Assert.That(playerAudio.GetValue(player), Is.Not.Null,
                    "音频先挂时 PlayerController.Awake 应缓存到引用——否则 footstep 全哑");

                // 消费方二：BlockInteraction.Bind 里 GetComponentInParent 缓存
                var interaction = go.AddComponent<BlockInteraction>();
                interaction.Bind(world: null, registry: null, views: null, parent: go.transform);
                var interactionAudio = typeof(BlockInteraction).GetField(
                    "_audio", BindingFlags.Instance | BindingFlags.NonPublic);
                Assume.That(interactionAudio, Is.Not.Null, "BlockInteraction 应有 _audio 字段");
                Assert.That(interactionAudio.GetValue(interaction), Is.Not.Null,
                    "音频先挂时 BlockInteraction.Bind 应缓存到引用——否则 place/break 全哑");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// av W1-6：11 个事件音在 clip 缺失时都应静默跳过（EditMode 无资源是常态，
        /// 不能阻断游戏）。一个 Assert.DoesNotThrow 把 11 条都跑一遍。
        /// </summary>
        [Test]
        public void EventSounds_DoesNotThrowWhenClipMissing()
        {
            var go = new GameObject("PlayerAudio_EventsTest");
            try
            {
                var audio = go.AddComponent<PlayerAudioSystem>();
                Assert.DoesNotThrow(() =>
                {
                    audio.PlayEat(); audio.PlayHurt(); audio.PlayDie(); audio.PlayPickup();
                    audio.PlayCraft(); audio.PlayDoorOpen(); audio.PlayDoorClose();
                    audio.PlayHoeTill(); audio.PlayPlant(); audio.PlayHarvest();
                    audio.PlayToolBreak();
                }, "11 个事件音在 clip 缺失时都应静默跳过（EditMode 无资源是常态）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// av W1-6：Awake 应设置静态 Instance——合成 UI / RedstoneSystem 等不在玩家宿主链上的
        /// 系统（如 CraftingPocketUi / CraftingFurnaceUi）靠它取音效，不需走玩家身上的 _audio 缓存。
        /// </summary>
        [Test]
        public void Instance_SetOnAwake()
        {
            var go = new GameObject("PlayerAudio_InstanceTest");
            try
            {
                var audio = go.AddComponent<PlayerAudioSystem>();
                var awake = typeof(PlayerAudioSystem).GetMethod(
                    "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Assume.That(awake, Is.Not.Null, "PlayerAudioSystem 应有私有 Awake");
                awake.Invoke(audio, null);
                Assert.That(PlayerAudioSystem.Instance, Is.EqualTo(audio),
                    "Awake 应设置静态 Instance");
            }
            finally
            {
                Object.DestroyImmediate(go);
                // 反射置 null（Instance setter 是 private）
                typeof(PlayerAudioSystem).GetProperty(
                    "Instance", BindingFlags.Static | BindingFlags.Public)
                    .SetValue(null, null);
            }
        }
    }
}
#endif