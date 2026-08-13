#if UNITY_EDITOR
// PlayerAudioSystem 是 nice-to-have：AudioClip 缺失时不能阻断游戏（debug.LogWarning 后跳过即可）。
// 这两个 fixture 验证缺失 clip 的两条路径（footstep / place），break 走同一条 PlayClip，足够覆盖。
using MyWorld.Unity.Audio;
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
    }
}
#endif