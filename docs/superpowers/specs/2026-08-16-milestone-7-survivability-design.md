# milestone-7 设计：生存可用性三修

> 日期：2026-08-16
> 状态：已通过设计评审（父子共同开发项目）
> 前置：milestone-6 已完成，dotnet 463 / EditMode 683 全绿
> 诊断报告：`.superpowers/m7-diagnosis.md`（三项反馈全部有 file:line 证据）

## 背景与问题

实机试玩反馈 3 个生存级问题（诊断已定位根源）：

| # | 反馈 | 根源 |
| --- | --- | --- |
| 1 | 没有退出游戏的流程 | 全游戏无玩家退出入口（`UiScreenshotOnArg.cs:93` 是唯一 Application.Quit 调用者，开发工具） |
| 2 | 挖一会儿就「倒计时→死亡→复活」循环 | 复活点=死亡位置（`DeathScreenUi.cs:86`）+ 僵尸永不消失、昼夜追击（射程 8 格、2 伤/秒，`Mob.cs:60`）——无武器 10 秒死、原地复活被同一只僵尸再杀、14 秒一循环；次因：`Eat()` 零调用（食物吃不了）→ ~8.4 分钟必饿死一次 |
| 3 | 挖完看不到掉落物 | B8 规划的 `ItemDropView` 从未创建（`ItemDropEntity.cs:15-17` 注释留位）——掉落物纯数据无视觉；拾取半径 1.5m < 挖掘射程 5m，远挖的掉落物默默躺着 |

## 目标

孩子能活着玩下去：死不了循环、饿了有办法、走时能退出、挖了看得见。

## 非目标（YAGNI）

- 死亡掉落物品/经验（死了不掉东西，保持宽容）
- 僵尸 AI 大改（只调平衡参数 + despawn，不改状态机）
- 掉落物物理（不做抛物线/弹跳，只做悬浮旋转视觉 + 吸附动画）
- 床/设置重生点（重生固定回世界出生点）
- 设置菜单扩展（退出按钮进帮助菜单，不做独立暂停菜单树）

## 1. 安全重生（修死亡循环主结构）

- **重生点改世界出生点**：`DeathScreenUi` 复活逻辑（`:86` 一带）从「死亡位置原地复活」改为回到 `WorldBootstrap` 的出生点（`SurfaceHeightAt(0,0)+2`，出生点常量共享，不做床）
- **3 秒无敌帧**：复活后 `PlayerController` 3 秒内 `TakeDamage` 直接忽略（`InvincibleUntil` 时间戳；UI 提示「无敌 3 秒」小字或不提示均可——做了提示更友好）
- 饿死在出生点的边角：出生点僵尸也可能游荡过来——无敌帧 + 僵尸平衡（下节）共同兜住

## 2. 僵尸平衡（修死亡循环的威胁侧）

三个参数级调整（`Mob.cs` / `MobManager.cs` / `MobAI.cs`）：

| 项 | 现值 | 改为 | 理由 |
| --- | --- | --- | --- |
| 追击射程 AttackRange | 8 格 | **4 格** | 8 格在视野外白打；4 格贴近才咬 |
| 昼夜追击 | 永远追 | **白天不追**（进入 wander，夜恢复追） | Minecraft 语义；白天被追无处可逃体验极差 |
| despawn | 无 | **距玩家 >40 格移除** | 永不消失 → 僵尸越积越多围出生点 |
| ChaseRadius | 32 | **20** | 32 格隔着大半屏就锁人 |

白天判定复用 `MobManager.IsNightPhase`（m5 A1 单一真源）。

## 3. 吃食物（修饥饿死循环——打通 Eat()）

- **交互**：当前选中 hotbar 槽是食物（`ItemDefinition.HealAmount` 或新增 `FoodValue > 0` 判定——读现有 items JSON 的字段，porkchop 有 healAmount: 3）时，**按右键吃掉 1 个**：饥饿 +恢复值（上限钳制）、物品 -1
- 与挖/放不冲突：右键当前是放方块——**食物优先**：选中槽是食物时右键=吃（不放方块）；非食物=放方块。UI 提示：hotbar 选中食物时准星下方小字「右键食用」
- `PlayerContext.HungerSystem` 侧补 `Eat(int foodValue)` 调用路径（方法存在但零调用——打通它）；饥饿恢复同时回少量饱和
- m5 A2 后饥饿伤害走 `PlayerContext.Health`——吃饱即止血，循环解除

## 4. 退出游戏（修流程缺失）

- **帮助菜单（H）加「保存并退出」按钮**：点击 → `SaveLoadService.SaveNow(async:false)` 同步落盘 → `Application.Quit()`
- 按钮放设置页底部（红色系文字「保存并退出游戏」），点击后 0.5s 延迟退出（让「已保存」提示可见）
- Alt+F4/窗口关闭仍走 `OnApplicationQuit`（既有）；HelpMenuUi 的按键表补「Alt+F4 直接退出（自动存档）」说明行

## 5. 掉落物视觉 + 拾取增强（修挖掘反馈）

- **ItemDropView**（新建 `Assets/Scripts/Unity/Items/ItemDropView.cs`）：每个 `PlayerContext.ItemDrops` 实体一个小方块（0.25 格）悬浮 + 缓慢旋转 + 上下浮动；材质用 `UrpMaterialFactory.CreateLit`（物品主色——贴图取不到就用品红占位色的暗化版，别再纯品红）
- **视图生命周期**：`PlayerContext.ItemDrops` 增删时同步建/毁视图（一个薄的 `ItemDropViewRegistry`，模式抄 `ChunkViewRegistry` 的注册表思路但简单得多——每帧 diff 或增删钩子）
- **拾取增强**：拾取半径 1.5m → **2.5m**；进入 2.5m 后掉落物**飞向玩家**（0.2s 插值），到位即入包——视觉上「吸过来」替代无声消失
- 数量到账即刷新 hotbar（既有每帧读库存，天然满足）

## 6. 测试与验收

**自动化**：
- dotnet：僵尸参数/despawn 判定（Core 可测部分）、Eat 路径（HungerSystem 恢复+钳制）、无敌帧忽略伤害
- EditMode：重生点回到出生点、食物右键路由（食物优先于放方块）、退出按钮触发同步保存（注入假 Quit）、ItemDropViewRegistry 增删同步、拾取半径/飞向插值参数
- 基线 dotnet 463 / EditMode 683 只增不减

**实机验收剧本**：
1. 玩 10 分钟不死循环：白天僵尸不追、夜里被追能逃开（4 格射程）、远离后僵尸消失
2. 饿了：选中猪肉右键吃，饥饿回升
3. 死一次（故意）：复活回出生点 + 3 秒无敌，不再连环死
4. 挖土：看到小方块掉出来飘着，走近被吸进包、数量即时 +1
5. H → 保存并退出 → 重进，进度无损（m4/m6 回归）

## 与既有约束的关系

- Core 零 UnityEngine（僵尸参数/Eat/无敌判定在 Core；View/交互在 Unity 层）
- 复用单一真源：昼夜判定走 `IsNightPhase`、伤害走 `PlayerController.TakeDamage`、材质走 `UrpMaterialFactory`、退出保存走 `SaveLoadService.SaveNow(async:false)`
- 注释/断言中文；数据不变更（僵尸参数是 C# 常量，不进 JSON——保持 m3 现状风格）
