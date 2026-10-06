#if UNITY_EDITOR
// Task D4 → m8 A2: MobView 视觉回归测试。
// m8 A2 起 MobView 按 MobModels.Build(kind) 部位表拼装五生物（部位 cube + 腿铰链），
// 本文件断言：
//   1) 拼装结构：子物体数 == 部位表部位数，逐部位名都能找到（五生物 TestCase）
//   2) 腿铰链手法：枢轴在腿顶、cube 几何下移半高补偿（brief 模板）
//   3) host cube Renderer 禁用（部位表全权负责视觉；旧三类单 cube 路径保持点亮）
//   4) SetWalkPhase：腿局部旋转 ∈ [-20°, 20°]、对角反向（LegPhase 差 π）、非腿不动
//   5) 朝向：LateUpdate 面朝移动方向（模型约定面朝 +Z），速度归零保持原朝向
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链跑纯 Core 测试时跳过，Unity EditMode 链跑。
// 视觉/AI 角色测试统一走 Unity EditMode，namespace 与同目录 MobViewPerKindTests 一致。
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobVisualTests
    {
        // ---------- 1) 拼装结构 ----------

        /// <summary>
        /// m8 A2：拼装后每个部位恰好一个直接子物体（腿是枢轴、其余是 cube 本体），
        /// 子物体数 == MobModels.Build(kind).Length，且逐部位按名可寻。
        /// </summary>
        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void Setup_AssemblesOneChildPerPart(MobKind kind)
        {
            var go = new GameObject("AssembleTest_" + kind);
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(kind);
                var parts = MobModels.Build(kind);
                Assert.That(go.transform.childCount, Is.EqualTo(parts.Length),
                    kind + " 子物体数应等于部位表部位数 " + parts.Length);
                foreach (var part in parts)
                {
                    Assert.That(go.transform.Find(part.Name), Is.Not.Null,
                        kind + " 部位 " + part.Name + " 应有同名直接子物体");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 猪应按部位表拼装 7 个部位（body+head+snout+四腿，spec §1 表逐部位求和）。
        /// 旧断言（Body+Head 两 cube）是 m5 双段造型的结构，m8 A2 起由部位表全权负责。
        /// </summary>
        [Test]
        public void SpawnPig_ProducesMesh()
        {
            var go = new GameObject("PigTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Pig);
                int cubeCount = 0;
                foreach (Transform child in go.transform) cubeCount++;
                Assert.That(cubeCount, Is.EqualTo(MobModels.Build(MobKind.Pig).Length),
                    "猪应按部位表拼装 7 个部位（子物体数 = 部位数）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 僵尸是五生物里唯一的人形怪物：头部位中心 y=1.7（部位表「竖身体 + 方头」）。
        /// 旧断言 body.localScale.y > 1.5 依赖 m5 的 host 缩放手法，m8 A2 起部件不再
        /// 单独拉高，改断言头部位置 > 1.5 守住「远高于猪（头 0.6）」的人形身高辨识点。
        /// </summary>
        [Test]
        public void SpawnZombie_ProducesTallMesh()
        {
            var go = new GameObject("ZombieTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Zombie);
                var head = go.transform.Find("head");
                Assert.That(head, Is.Not.Null, "僵尸应有 head 部位子物体");
                Assert.That(head.localPosition.y, Is.GreaterThan(1.5f),
                    "僵尸头部位应高于 1.5（人形身高，猪头才 0.6）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- 2) 腿铰链手法 ----------

        /// <summary>
        /// 腿枢轴在腿顶（髋部）：pivot.localPosition == LocalPosition + up×半高，
        /// cube 几何下移半高补偿回原位、localScale == 部位尺寸——
        /// 绕枢轴转 X 轴才是「摆腿」而不是「绕腿中心原地蹭」（brief 模板核心手法）。
        /// </summary>
        [Test]
        public void Setup_LegPivotAtTopOfLeg_MeshOffsetDownHalfHeight()
        {
            var go = new GameObject("LegPivotTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Pig);
                foreach (var part in MobModels.Build(MobKind.Pig))
                {
                    if (!part.IsLeg) continue;
                    var pivot = go.transform.Find(part.Name);
                    Assert.That(pivot, Is.Not.Null, "猪腿 " + part.Name + " 应有枢轴子物体");
                    Vector3 expectedPivot = part.LocalPosition + Vector3.up * (part.Size.y * 0.5f);
                    Assert.That(pivot.localPosition, Is.EqualTo(expectedPivot).Within(0.0001f),
                        part.Name + " 枢轴应在腿顶（LocalPosition + up×半高）");
                    var mesh = pivot.Find(part.Name + "Mesh");
                    Assert.That(mesh, Is.Not.Null,
                        part.Name + " 枢轴下应有 " + part.Name + "Mesh cube（几何下移补偿）");
                    Assert.That(mesh.localPosition, Is.EqualTo(Vector3.down * (part.Size.y * 0.5f)).Within(0.0001f),
                        part.Name + " 的 cube 应下移半高补偿，几何中心回到原部位位置");
                    Assert.That(mesh.localScale, Is.EqualTo(part.Size).Within(0.0001f),
                        part.Name + " 的 cube 缩放应等于部位尺寸");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- 3) host cube Renderer 禁用 ----------

        /// <summary>
        /// host cube 的 Renderer 应禁用：拼装部位已覆盖 host 体积，双份渲染只会重合
        /// （消灭重合渲染）。旧三类单 cube 路径 host 就是本体，保持点亮不倒退。
        /// </summary>
        [Test]
        public void Setup_HostCubeRenderer_DisabledForAssembledKind_LegacyStaysEnabled()
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var legacyHost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                MobView.Attach(host, Mob.Create(6, new MyWorld.Core.Math.Float3(0f, 1f, 0f)));   // Pig
                MobView.Attach(legacyHost, Mob.Create(1, new MyWorld.Core.Math.Float3(0f, 1f, 0f))); // Passive 旧路径
                Assert.That(host.GetComponent<Renderer>().enabled, Is.False,
                    "Pig 拼装后 host cube Renderer 应禁用（部位表全权负责视觉）");
                Assert.That(legacyHost.GetComponent<Renderer>().enabled, Is.True,
                    "旧 Passive 单 cube 路径 host 就是本体，Renderer 应保持点亮");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(legacyHost);
            }
        }

        // ---------- 4) SetWalkPhase 腿摆动 ----------

        /// <summary>
        /// 相位扫过 [0, 2π]：所有腿的局部旋转 X 角都落在 [-20°, 20°]
        /// （sin 幅值 ×20°，不会越界；用 Mathf.DeltaAngle 折算欧拉角回 (-180,180]）。
        /// </summary>
        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        public void SetWalkPhase_PhaseSweep_AllLegsWithinTwentyDegrees(MobKind kind)
        {
            var go = new GameObject("SweepTest_" + kind);
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(kind);
                foreach (var part in MobModels.Build(kind))
                {
                    if (!part.IsLeg) continue;
                    var pivot = go.transform.Find(part.Name);
                    for (float phase = 0f; phase < Mathf.PI * 2f; phase += 0.3f)
                    {
                        view.SetWalkPhase(phase);
                        float angle = Mathf.DeltaAngle(0f, pivot.localRotation.eulerAngles.x);
                        Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(20.01f),
                            kind + " 腿 " + part.Name + " 在 phase=" + phase.ToString("F2") +
                            " 摆角应在 ±20°内，实际 " + angle.ToString("F2") + "°");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 对角步态：phase=π/2 时 sin(+0)=1、sin(+π)=-1——
        /// legFL/legBR（同相 0）摆 +20°，legFR/legBL（反相 π）摆 -20°，对角同幅反向。
        /// </summary>
        [Test]
        public void SetWalkPhase_PigLegs_DiagonallyOppositeAtQuarterCycle()
        {
            var go = new GameObject("DiagonalTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Pig);
                view.SetWalkPhase(Mathf.PI * 0.5f);
                float fl = LegAngle(go, "legFL");
                float fr = LegAngle(go, "legFR");
                float bl = LegAngle(go, "legBL");
                float br = LegAngle(go, "legBR");
                Assert.That(fl, Is.EqualTo(20f).Within(0.1f), "legFL（相 0）应摆 +20°");
                Assert.That(br, Is.EqualTo(20f).Within(0.1f), "legBR（相 0）应与 legFL 同相 +20°");
                Assert.That(fr, Is.EqualTo(-20f).Within(0.1f), "legFR（相 π）应摆 -20°");
                Assert.That(bl, Is.EqualTo(-20f).Within(0.1f), "legBL（相 π）应与 legFR 同相 -20°");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 非腿部位不受 phase 影响：body/head/snout 的位置与旋转在扫相位前后完全不变
        /// （摆动只作用于腿枢轴）。
        /// </summary>
        [Test]
        public void SetWalkPhase_NonLegPartsStayStill()
        {
            var go = new GameObject("NonLegTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Pig);
                var before = new System.Collections.Generic.Dictionary<string, Vector3>();
                var beforeRot = new System.Collections.Generic.Dictionary<string, Quaternion>();
                foreach (var part in MobModels.Build(MobKind.Pig))
                {
                    if (part.IsLeg) continue;
                    var t = go.transform.Find(part.Name);
                    before[part.Name] = t.localPosition;
                    beforeRot[part.Name] = t.localRotation;
                }

                for (float phase = 0f; phase < Mathf.PI * 2f; phase += 0.37f)
                {
                    view.SetWalkPhase(phase);
                }

                foreach (var part in MobModels.Build(MobKind.Pig))
                {
                    if (part.IsLeg) continue;
                    var t = go.transform.Find(part.Name);
                    Assert.That(t.localPosition, Is.EqualTo(before[part.Name]).Within(0.00001f),
                        "非腿部位 " + part.Name + " 的位置不应随 phase 变化");
                    Assert.That(t.localRotation, Is.EqualTo(beforeRot[part.Name]),
                        "非腿部位 " + part.Name + " 的旋转不应随 phase 变化");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 村民长袍到脚、无独立腿（spec §1）：SetWalkPhase 应是 no-op，
        /// 全部部位旋转保持 identity。
        /// </summary>
        [Test]
        public void SetWalkPhase_VillagerWithoutLegs_IsNoOp()
        {
            var go = new GameObject("VillagerNoOpTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Villager);
                view.SetWalkPhase(1.23f);
                foreach (Transform child in go.transform)
                {
                    Assert.That(child.localRotation, Is.EqualTo(Quaternion.identity),
                        "村民无腿，部位 " + child.name + " 的旋转不应被 SetWalkPhase 改动");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- 5) 朝向 ----------

        /// <summary>
        /// 拼装模型约定面朝 +Z：LateUpdate 应把 transform 转到面朝移动方向
        /// （按速度 LookRotation，水平面内）。速度归零时保持原朝向不闪转。
        /// </summary>
        [Test]
        public void LateUpdate_FacesVelocityHorizontally_AndKeepsHeadingWhenIdle()
        {
            var go = new GameObject("FacingTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Mob = Mob.Create(9, new MyWorld.Core.Math.Float3(0f, 0f, 0f)); // Zombie
                view.Setup(MobKind.Zombie);

                view.Mob.Velocity = new MyWorld.Core.Math.Float3(0f, 0f, 2f);
                InvokeLateUpdate(view);
                AssertForward(go.transform, new Vector3(0f, 0f, 1f),
                    "朝 +Z 移动时应面朝 +Z（模型面朝 +Z 约定）");

                view.Mob.Velocity = new MyWorld.Core.Math.Float3(2f, 0f, 0f);
                InvokeLateUpdate(view);
                AssertForward(go.transform, new Vector3(1f, 0f, 0f),
                    "朝 +X 移动时应面朝 +X");

                // 站定（速度归零）：保持原朝向，不闪转回 identity
                Quaternion before = go.transform.rotation;
                view.Mob.Velocity = default;
                InvokeLateUpdate(view);
                Assert.That(go.transform.rotation, Is.EqualTo(before),
                    "速度归零时应保持原朝向（站定不闪转）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 逐分量断言朝向（容差 0.001）：LookRotation 的 90° 旋转含 sqrt(0.5) 的浮点误差，
        /// NUnit 的 Within 不对 Vector3 逐分量生效，直接 EqualTo 会假失败。
        /// </summary>
        private static void AssertForward(Transform t, Vector3 expected, string message)
        {
            Vector3 f = t.forward;
            Assert.That(Mathf.Abs(f.x - expected.x), Is.LessThan(0.001f),
                message + "（X 分量，实际朝向 " + f.ToString("F4") + "）");
            Assert.That(Mathf.Abs(f.y - expected.y), Is.LessThan(0.001f),
                message + "（Y 分量，实际朝向 " + f.ToString("F4") + "）");
            Assert.That(Mathf.Abs(f.z - expected.z), Is.LessThan(0.001f),
                message + "（Z 分量，实际朝向 " + f.ToString("F4") + "）");
        }

        private static float LegAngle(GameObject go, string legName)
        {
            var pivot = go.transform.Find(legName);
            Assert.That(pivot, Is.Not.Null, "应找到腿枢轴 " + legName);
            return Mathf.DeltaAngle(0f, pivot.localRotation.eulerAngles.x);
        }

        private static void InvokeLateUpdate(MobView view)
        {
            // EditMode 下 MonoBehaviour 生命周期方法不会自动跑，反射直调
            var method = typeof(MobView).GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "MobView 应有私有 LateUpdate 方法");
            method.Invoke(view, null);
        }

        [Test]
        public void LateUpdate_常态帧_不再逐部位写MPB()
        {
            // 评审 02#6：旧实现每帧对每部位 GetPropertyBlock/SetPropertyBlock
            //（24 mob×8 部位 ≈384 次/帧 native 调用且破坏 SRP 合批）——常态（无闪无引信）
            // 部位色不变必须零写入；受击红闪首帧写一次后同样跳过
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var view = MobView.Attach(host, Mob.Create(6, new MyWorld.Core.Math.Float3(0f, 1f, 0f))); // Pig
                view.LateUpdate(); // 首帧全写（哨兵初始化）
                int afterFirst = view.MpbWritesForTests;

                view.LateUpdate(); // 常态第二帧：零写入
                view.LateUpdate();
                Assert.That(view.MpbWritesForTests - afterFirst, Is.EqualTo(0),
                    "常态帧（无受击无引信）不再逐部位写 MPB（评审 02#6 核心断言）");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
