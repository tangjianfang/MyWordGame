# milestone-11 实施计划 · 第 3 波（终局内容）

> REQUIRED SUB-SKILL: superpowers:subagent-driven-development。前置读主计划 Global Constraints。基线：dotnet **894** / EditMode **1536**（2026-08-18 集成点③）。
> 注意：存在并行的「av 音频/视频赛道」会话——MobManager/WorldBootstrap/TitleScreen 等文件可能有它的在途改动，冲突时等待重试、绝不覆盖他人 hunks。

## Task W3-1：通用放置路由（关键缺口，③发现）

`BlockInteraction` 放置仍是 m3 占位（恒放 Stone）——家具/床/箱子/附魔台/门全放不进世界。做：手持物品带 `blockId` → 放对应方块（读 ItemDefinition，家具/附魔台/箱子/床/门全走它）；床双格放置接 BedSystem.PlaceBed 既有 API；贴图占位兼容。测试：每类物品放置断言 + 床头脚两格 + 指针门。

## Task W3-2：村庄结构生成

`WorldGenerator` 平原/森林低密度确定性村庄：3-5 楼房（木板/原木/玻璃，照树特征模式纯函数 + 世界坐标哈希选点，结构蓝图数组化）；村民 spawn 偏向村庄半径（MobManager TickSpawn 加结构查询，可后置集成）。测试：同 seed 同村、不穿水/悬崖、村民绑定。

## Task W3-3：机元守卫 Boss + 下界合金链

召唤：y<16 放 4 机元矿石方块图腾右键（BlockInteraction 加分支）→ Boss 生成。Boss：机元守卫（模型 json 紫金机甲 2.5 格高、血 60、三招：冲撞/范围震荡波/召唤 2 骷髅，MobAI 新 kind 或复用扩展）。掉落：netherite_ingot ×2-3 + 附魔书（带编码）。netherite 工具已有配方物品——补 netherite 装备升级配方（netherite_ingot + 铁件 → netherite 件）。测试：召唤条件/Boss AI 三招/掉落链。

## Task W3-4：氛围——天气/云/粒子

雨雪（Snow 群系雪、其余雨：半透明 IMGUI/粒子层 + 音 av 已备可选）、云层（天空盒上方白色面片缓慢平移）、粒子（挖掘碎屑方块色 4-6 粒、爆炸 fx-explosion 帧序列播放、附魔光柱 magic-enchant-column）。全部纯视觉不碰逻辑。测试：天气状态机（TimeOfDay 派生确定性）/粒子池复用无每帧 new。

## Task W3-5：和平模式 + 收尾杂项

设置面板（SettingsPanelUi 共用实例）加「和平模式」开关：怪物不生成（MobManager gate）+ 死亡不掉落（ItemDrops 保留）；持久化 PlayerPrefs 照三滑条先例。杂项：楼梯方块评估报告（不实现，写给终审）。

## 集成点 ④（终审）

双链全绿 → 全分支终审（superpowers:requesting-code-review 模式）→ 一次修复波 → CLAUDE.md 更新至 m11 现状 → 给孩子的验收剧本（终版）→ milestone 收官 commit。
