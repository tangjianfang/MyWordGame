using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 交易村民渲染（m8 终审修 I-1）：统一走 <see cref="MobAssembly"/> 部位表拼装的
    /// 长袍村民（body+head+双臂抱胸+长鼻），与 MobView 刷的村民同一副面孔，
    /// 消灭「同一物种两副面孔」的双形态分叉。旧单 cube（0.6×1.8×0.6 整块职业色）路径删除。
    /// <para>
    /// 职业辨识信号从「整块染色」改为「染袍」：部位表里底色 == 村民长袍色
    /// （<see cref="UrpMaterialFactory.MobBodyColor"/>）的部位（body/armLower/armUpper）
    /// 染职业色，头/鼻保持部位表肤色——穿什么衣服看职业，脸永远是同一张村民脸。
    /// 同步 Position；受伤红闪涂满全身（对齐 MobView 语义，闪完自动还原职业袍色）。
    /// </para>
    /// </summary>
    public sealed class VillagerView : MonoBehaviour
    {
        public Villager Villager;

        // m8 终审修拼装态：MobAssembly 产物句柄 + 职业色专染的袍部位清单
        private AssembledMob _assembled;
        private Renderer[] _robeRenderers;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public static VillagerView Attach(GameObject host, Villager villager)
        {
            var view = host.AddComponent<VillagerView>();
            view.Villager = villager;
            view.BuildAssembled();
            view.ApplyRobeColor(ProfessionColor(villager.Profession));
            return view;
        }

        /// <summary>
        /// 职业袍色（沿用旧单 cube 时代的职业配色，玩家辨识习惯不断档）。
        /// </summary>
        private static Color ProfessionColor(VillagerProfession profession)
        {
            return profession switch
            {
                VillagerProfession.Farmer => new Color(0.55f, 0.4f, 0.2f),
                VillagerProfession.Librarian => new Color(0.45f, 0.35f, 0.5f),
                VillagerProfession.Blacksmith => new Color(0.3f, 0.3f, 0.35f),
                _ => new Color(0.6f, 0.5f, 0.4f),
            };
        }

        /// <summary>
        /// 长袍村民拼装：与 MobView 共用 <see cref="MobAssembly.Assemble"/>
        /// （部位 cube + 腿枢轴 + host Renderer 禁用）。袍部位按「部位表底色 == 村民长袍色」
        /// 识别（当前是 body/armLower/armUpper 三个）——部位表后续加袍部件时自动跟进，
        /// 头/鼻这类肤色部位天然排除。
        /// </summary>
        private void BuildAssembled()
        {
            _assembled = MobAssembly.Assemble(transform, MobKind.Villager);
            Color robe = UrpMaterialFactory.MobBodyColor(MobKind.Villager);
            var robes = new List<Renderer>();
            for (int i = 0; i < _assembled.PartRenderers.Length; i++)
            {
                if (_assembled.PartBaseColors[i] == robe) robes.Add(_assembled.PartRenderers[i]);
            }
            _robeRenderers = robes.ToArray();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        /// <summary>职业袍色：只染长袍部位（body + 双臂），头/鼻不动。</summary>
        public void ApplyRobeColor(Color c)
        {
            if (_robeRenderers == null) return;
            foreach (var r in _robeRenderers) SetInstanceColor(r, c);
        }

        /// <summary>涂满全部部位（受伤红闪用，对齐 MobView 的「红闪涂满全身」语义）。</summary>
        public void ApplyColor(Color c)
        {
            if (_assembled == null) return;
            foreach (var r in _assembled.PartRenderers) SetInstanceColor(r, c);
        }

        private void SetInstanceColor(Renderer r, Color c)
        {
            if (r == null) return;
            // EditMode 测试里 AddComponent 不触发 Awake，_block 可能为 null → 懒初始化
            if (_block == null) _block = new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (Villager == null) return;
            transform.position = new Vector3(Villager.Position.X, Villager.Position.Y, Villager.Position.Z);
            // 受伤红闪涂满全身；平时袍部位保持职业色（旧版闪完不还原职业色的隐患一并修掉）。
            // 头/鼻不动——Assemble 时已按部位表色染好，无需每帧重设。
            if (Villager.HitFlashTimer > 0) ApplyColor(Color.red);
            else ApplyRobeColor(ProfessionColor(Villager.Profession));
        }
    }
}
