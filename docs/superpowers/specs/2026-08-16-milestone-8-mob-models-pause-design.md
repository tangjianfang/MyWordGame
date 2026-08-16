# milestone-8 设计：生物拼装造型 + Esc 暂停菜单

> 日期：2026-08-16
> 状态：已通过设计评审（父子共同开发项目）
> 前置：milestone-7 已完成，dotnet 473 / EditMode 718 全绿

## 背景与问题

实机试玩两条反馈：

| # | 反馈 | 现状 |
| --- | --- | --- |
| 1 | 生物形状看不懂、不像动物（村民/动物） | `MobView` 只有 Body+Head 两个染色方块（m5 A3 只做了染色没做造型） |
| 2 | 退出不直观——想要「按一下」就到的退出菜单 | m7 A4 的退出按钮藏在 H 帮助菜单设置页，用户没找到 |

用户已确认方向：**m8 方块拼装造型（Minecraft 式），m9 再上贴图皮肤**（结构预留）；退出走 **Esc 暂停菜单**。

## 目标

1. 五种生物一眼认出是什么（猪牛鸡僵尸村民）
2. Esc 一按：游戏暂停、居中菜单（继续/设置/保存退出）

## 非目标（YAGNI）

- 贴图皮肤/UV（m9——m8 结构预留：材质工厂已支持 mainTexture，换贴图不改结构）
- 生物动画系统（只做腿摆动一个动效）
- 尾巴/翅膀/耳朵等次级部位（腿/鼻/角/冠/臂够了）
- 被动生物的眼睛（贴图阶段再画）

## 1. 生物拼装造型

### MobModels 部位表（Core 可测 + Unity 拼装）

新建 `Assets/Scripts/Unity/Rendering/MobModels.cs`：

```csharp
public readonly struct MobPart
{
    public readonly string Name;      // "body"/"head"/"legFL"...（调试可见）
    public readonly Vector3 Size;     // 部位尺寸（格）
    public readonly Vector3 LocalPosition;
    public readonly Color Color;      // 经 UrpMaterialFactory 缓存染色
    public readonly bool IsLeg;       // 腿：参与行走摆动
    public readonly float LegPhase;   // 摆动相位（对角步态 0/π）
}
public static class MobModels
{
    public static MobPart[] Build(MobKind kind);   // 纯静态表，可 EditMode 断言
}
```

### 五生物拼装定义（部位坐标以「脚底中心为原点」，整体高度 0.7–1.8 格）

| 生物 | 部位（在旧 Body+Head 色基础上） | 辨识点 |
| --- | --- | --- |
| 猪 | 横身体 0.9×0.6×0.6 + 头 0.5³（前端）+ **鼻 0.2×0.15×0.1（头前突出）** + 4 短腿 0.15×0.3 | 矮胖+粉鼻，体高 ~0.9 |
| 牛 | 横身体 1.0×0.7×0.7 + 头 0.5³ + **双角 0.1×0.1×0.15 两根（头顶）** + 4 腿 0.2×0.5 | 高大+角，体高 ~1.3 |
| 鸡 | 小竖身体 0.4×0.5×0.5 + 头 0.3³ + **黄嘴 0.15×0.1×0.1 + 红冠 0.2×0.1×0.15** + 2 细腿 0.08×0.3 | 最小+红冠，体高 ~0.8 |
| 僵尸 | 人形：竖身体 0.5×0.75×0.3 + 头 0.4³ + **双臂 0.2×0.2×0.7 前伸（水平指向前）** + 2 腿 0.2×0.75 | 前伸双臂，体高 ~1.8 |
| 村民 | 人形长袍：竖身体 0.5×1.2×0.3（下到脚）+ 头 0.4³ + **双臂抱胸（两段小臂斜叠身前）** | 长袍到脚无独立腿，体高 ~1.7 |

- 部位颜色沿用 m5 A3 的 Body/Head 色 + 少量新增部位色（猪鼻深粉 `#C87880`、牛角灰白 `#D8D0C0`、鸡嘴黄 `#D9A03D`、鸡冠红 `#C03028`）
- 旧三类（Passive/Hostile/Neutral 的 creeper 等）：保持现有两方块，不倒退不重做（表里给保底两部位定义）

### MobView 改造

- `MobView` 按表拼装（`CreatePrimitive` + `UrpMaterialFactory.CreateLit` 缓存材质，模式抄 `PlayerVisual` 的 10-cube 组装）；**删旧 Body/Head 两 cube 硬编码**与 `MobManager.cs` 的 host cube 重合渲染（m5 遗留 M3——host cube 禁用 Renderer，部位表全权负责视觉）
- **朝向**：部位表定义为「面朝 +Z」；MobView 已有移动朝向逻辑则对齐到 +Z，没有则按速度向量 `LookRotation`
- **腿摆动**：`IsLeg` 部位绕顶部枢轴摆动 `±20° × sin(walkPhase + LegPhase)`；`walkPhase` 由 `Mob` 的实际位移驱动（移动才摆、站定归零）；枢轴用子 GameObject 偏移（cube localPosition 上移半个身长 + 几何下移补偿——经典 Unity 铰链手法）

### 贴图预留（m9 的地基）

部位表结构不改就能上贴图：m9 只给 `MobPart` 加 `TextureName`，材质工厂 `CreateLit(color, texture)` 重载——本里程碑不做，但拼装结构评审时确认「部位独立可贴图」。

## 2. Esc 暂停菜单

新建 `Assets/Scripts/Unity/UI/PauseMenuUi.cs`：

- **Esc 开关**（打开时 H 菜单若开着先关）；死亡画面显示时 Esc 让位（`DeathScreenUi` 可见则不弹）
- **居中三按钮**：继续游戏 / 设置 / 保存并退出
  - 继续：关菜单恢复
  - 设置：展开 m7 H 菜单同款三滑条（灵敏度/音量/FOV，PlayerPrefs 复用——**抽公共 `SettingsPanelUi` 组件**，H 菜单与暂停菜单共用，不复制代码）
  - 保存并退出：复用 m7 A4 的保存-退出状态机（同步保存→成功 0.5s 退出；失败红字可重试）——同样抽公共方法或直接调用 HelpMenuUi 的公开接口
- **真暂停**：打开时 `Time.timeScale = 0f`（生物/昼夜/饥饿/熔炉全停——`PlayerController.Tick` 走 `Time.deltaTime` 自然停；`MobManager.Update` 依赖 timeScale 同停，需核实没有用 `unscaledDeltaTime` 的路径）；关闭恢复 `1f`；退出保存时保持 0（防止保存期间被打死）
- **指针门统一**：PauseMenuUi 登记进 m6 终审的指针门（菜单开=解锁可见）；`InputLocked` 语义复用（暂停时挖/放不响应）
- `Application.Quit` 前 `timeScale` 恢复与否不影响退出（进程结束）

## 3. 测试与验收

**自动化**：
- EditMode：`MobModelsTests`（每 MobKind 部位数 ≥5（旧三类 ≥2）、全部含 head、腿部位 LegPhase ∈ {0, π}、部位在合理包围盒内、无重叠超半）；`MobViewTests` 扩展（拼装后子物体数 = 部位数、host cube Renderer 禁用）；腿摆动角度钳制 ±20°；`PauseMenuTests`（Esc 开关/死亡让位/timeScale 0↔1/指针门登记/设置面板复用不重复建/保存退出复用）
- dotnet 基线 473 / EditMode 基线 718 只增不减
- visual-smoke：`--ui-shot` 序列加第 5 张 `ui-pause.png`（Esc 菜单截图，像素断言居中面板存在）

**实机验收**：
1. 远远一眼说出「那是猪/牛/鸡/僵尸/村民」——孩子盲测
2. 走动时腿在摆、站定不动
3. 按 Esc：画面停住（云不动、怪不动）→ 居中菜单 → 设置能调 → 保存退出 → 重进无损
4. 死亡画面按 Esc 不弹暂停

## 与既有约束的关系

- Core 零 UnityEngine（部位表在 Unity 层，MobKind 是 Core 枚举——引用方向合法）
- 材质走 UrpMaterialFactory 缓存（同色部位共享实例）；注释/断言中文
- 指针门/设置面板/保存退出全部复用既有组件，不建第二套
