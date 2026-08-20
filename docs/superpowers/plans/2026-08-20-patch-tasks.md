# 2026-08-20 补丁任务计划（docs review 产出 · 稍后执行）

> 来源：三份根文档（README.md / CLAUDE.md / AGENTS.md）统一更新至 m13 W5 基线后的 review（commit `f7140ac`）
> 基线：dotnet **974/974** + EditMode **1732/1732** 双链全绿（2026-08-20 实测）
> 性质：小修补丁集合 + m12 重启指引。**本计划只立项不执行**——按任务卡顺序择期落地。
> 铁律：全程照 CLAUDE.md；测试双链只增不减；子代理不 commit。

## 背景（为什么有这批补丁）

review 实证发现两类遗留：

1. **文档与实现漂移**：m12 计划（`2026-08-18-milestone-12.md`）里部分"已入库"表述与仓库实际不符（codex 美术未入库）；m13 验收剧本把 SHIFT+click 合成修复误标为"m12 P0"（实为 m13 P0，commit `70f35da`）。
2. **m12 整体未实施**：`DigProgress` / `WorldCatalog` / `PlacementGhostUi` / `AchievementSystem` / `CodexUi` / `PotionSystem` 均未创建——m13 插队先行落地，m12 计划定稿后搁置。三份根文档已标注"计划已定稿、实施待执行"，本计划补上重启路径。

---

## Task 1 · docs 勘误（P0 · 10 分钟 · 独立可做）

**改两份设计文档，不改代码：**

- `docs/superpowers/plans/2026-08-18-milestone-12.md`
  - **第 52 行**：`16 枚徽章图 Assets/Art/codex/badge-*.png 已入库` → **与实际不符**（`Assets/Art/codex/` 为空，2026-08-20 实证）→ 改为「需求已立项、成品未入库（见 Task 3 补缺口）」
  - **第 56 行**：`codex/card-*.png 卡牌美术已入库` → 同上改法
  - 第 60 行（药水 7 瓶 + 乐器贴图已入库）：**核实为真**（`items/textures/` 下 potion-*/bell/drum/flute 共 14 张 png 在仓），**不改**
  - 第 92 行（`--tree 已入库 ≥300/335`）：执行时跑 `python tools/generate_art.py --tree` 核对，数字漂了就更新
- `docs/superpowers/specs/2026-08-19-milestone-13-验收剧本.md`
  - **第 150 行**：`用 m12 P0 修的 SHIFT+click 合成` → 改为「用 **m13 P0** 修的 SHIFT+click 合成（commit `70f35da`）」
- 完成：单独一个 docs commit

## Task 2 · 工程小补（P0 · 5 分钟 · 与 Task 1 同批）

- `.gitignore` 加 `.zcode/`（ZCode 会话产物目录，2026-08-20 出现在 untracked）
- 完成：可并入 Task 1 的 commit 或单独 tools commit

## Task 3 · codex 美术缺口（P1 · m12 W1/W2 的前置）

`Assets/Art/codex/` 为空——16 枚成就徽章 + 图鉴卡牌（生物/矿石/植物三栏）成品未入库，**阻塞 m12 W1/W2 的 UI 实现**：

- 优先走 art 管线正式生成：`art/requests/codex/` 下需求文档已有（m11 A4 批 44 份含 16 徽章 + 卡牌卡框），跑 `generate_art.py --only codex --n 4 --jobs 4` → `postprocess_art.py` → 验收 → 入库
- 若 API 配额/时间紧：走 **程序占位快路径**（`art/scripts/*.py` 确定性哈希生成）先保证差集为空，标注「程序占位，待正式美术替换」
- 入库后更新 `art/README.md` 索引状态为「已入库」
- 完成：art commit + 索引更新

## Task 4 · EditMode 复核（P1 · 半小时）

W5 两个 commit（`76f8cec` / `5e4c357`，08-20 22:02）**晚于**最近一次 EditMode 结果文件（`unity-test-results.xml`，08-20 21:57）——现行 1732/1732 数字大概率含 W5 改动（当时工作树已改未提交）但**未 100% 实证**：

- 跑一次 EditMode 批处理（命令见 CLAUDE.md「常用命令」；先确认无其他 Unity 实例）
- 结果仍是 1732/1732 → 在本计划此任务打勾即可，根文档不用动
- 数字变了 → 同步改三份根文档的基线行（README/CLAUDE/AGENTS 各一处）

## Task 5 · m12 重启指引（P2 · 主体工程，择期启动）

m12 计划本体不重复抄写——**执行时照 `docs/superpowers/plans/2026-08-18-milestone-12.md` 走**（三大根因 + P0 挖掘计时/放置手感 + P1 世界管理 + 第 1 波 W1-W4 成就/图鉴/药水乐器/水生飞行）。重启前先对表四处漂移：

1. **基线漂移**：m12 plan 写于 dotnet 933 时代，现为 **974**；执行时出口标准按 974 只增不减
2. **已完成项剔除**：m13 P0 已修合成 SHIFT+click（勿重做）；P2 美术批 W5 已部分入仓（27 张缺省 + 6 实体美化），W4 美术子项执行前先盘 `--tree` 差集
3. **numericId 现状**：物品 1607/1608 已被 musket/bullet 占用、1700/1701 为早期物品（crafting_table/redstone_dust）——m12 药水/乐器物品注册走 **1609-1699 段顺延**；方块最大 1062，新方块从 1063/1064 起
4. **楼梯贴图在仓未注册**：`blocks/textures/stairs-{bricks,planks,stone}.png` 已入库但无方块 JSON——m12+ 若做楼梯方块只需补 JSON（数据驱动，不动 C#）

启动时建议：标签沿用 m12 复活或编 m14，由主会话定；SDD 工作区照惯例 `.superpowers/sdd/`。

---

## 出口标准

1. Task 1-2 完成（docs 勘误 + .gitignore），各自有 commit
2. Task 3 完成（codex 美术差集为空，art/README.md 索引更新）
3. Task 4 完成（EditMode 复核记录写回本文件，数字变动则三份根文档同步）
4. Task 5 排期确认并启动（m12 plan 对表修订后再派工）
5. 全程 dotnet 974 / EditMode 1732 只增不减；每个收口按铁律跑 `build-and-run.sh` 出包、验收剧本标注 exe mtime、三份根文档同步更新
