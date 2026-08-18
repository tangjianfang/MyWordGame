# 物品定义文件格式

这个目录里每个 `.json` 文件定义一种物品（材料/食物/工具/装备）。**加新物品不需要改任何代码**：
复制一个现有文件，改个名字和贴图，再把贴图 PNG 放进 `textures/` 就行。

## 两个完整的例子

普通材料（最小集）：

```json
{
  "id": "lapis",
  "displayName": "青金石",
  "numericId": 1603,
  "maxStack": 64,
  "texture": "lapis"
}
```

带耐久与装备加成的工具（m10 全量字段）：

```json
{
  "id": "iron_pickaxe",
  "displayName": "铁镐",
  "numericId": 1402,
  "maxStack": 1,
  "texture": "iron_pickaxe",
  "attackDamage": 4,
  "isTool": true,
  "miningLevel": 3,
  "toolTier": 3,
  "maxDurability": 250,
  "gearBonus": { "stat": "defense", "amount": 1 }
}
```

## 字段说明

| 字段 | 类型 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| `id` | 字符串 | **是** | — | 唯一标识，只用小写字母和下划线。**跨表引用（recipes / mobs/drop_tables / blocks/drops / quests）都写它**，引用了没注册的 id 加载即抛 `InvalidDataException` |
| `displayName` | 字符串 | 否 | 同 `id` | 显示给玩家看的名字，可以用中文 |
| `numericId` | 整数 | 否 | 自动分配 | 见下方「关于 numericId」；存档里实际存的是它，**定了就别改** |
| `maxStack` | 整数 | 否 | `64` | 单格堆叠上限。工具/装备填 `1` |
| `texture` | 字符串 | 否 | `"missing"` | 贴图名，对应 `textures/` 目录下的文件名，**不带 `.png` 后缀**（如 `"lapis"` 对应 `textures/lapis.png`） |
| `blockId` | 字符串 | 否 | 无关联 | **m11 W1-5 新引入，W3-1 起被放置路由消费**：物品与其「方块形态」的关联声明（如 `chair` ↔ `chair_block`）。写了它的物品右键放置落对应方块并扣 1 个物品（床走双格摆法、门贴地两格，见 BlockInteraction 放置路由）；不写 = 放置回落 placeBlockId 占位、不扣物品。挖掉那个方块掉回哪个物品由 `blocks/drops/block_drops.json` 的条目对齐（1:1 掉回——放置扣 1、挖掉还 1，不做无限复制机）。加载器解析进 `ItemDefinition.BlockId` 但不做跨表校验，悬空引用（写了但 blocks 注册表里没有）由真数据守卫测试 `BlockInteractionPlaceRoutingTests` 抓 |
| `attackDamage` | 小数 | 否 | 不可作武器 | 主手挥击伤害。不写（null）= 该物品不能当武器挥击 |
| `range` | 整数 | 否 | `0` | **m13 W3 新增**：远程武器射程（米）。弓 60 / 火枪 25 是合理量级；写 0 或不写 = 不可作为远程武器（近战/材料）。弹道飞行距离 `>= range` 时强制消亡——`ProjectileEntity.Range` 在 Tick 末段判定后 `State = Dead`。负数加载即抛（与 `toolTier` / `maxDurability` 同态度）。弓保留重力抛物线（既有行为），火枪走 `ProjectileEntity.IsStraightLine = true` 直射无重力——由宿主 `BlockInteraction.TryFireMusket` 在发射时设。写太远（数百米）不强制拦——手感问题不是硬卡 |
| `healAmount` | 小数 | 否 | 不可食用 | 右键吃掉后经 `HungerSystem.Eat` 回的饥饿值。不写或 0 = 不是食物，右键照常放方块 |
| `isTool` | 布尔 | 否 | `false` | 是否归类为工具（剑/镐/斧/锹）。用于耐久条与方块采集加速判定 |
| `miningLevel` | 整数 | 否 | `0` | 挖掘档位：0=手 / 1=木 / 2=石 / 3=铁 / 4=钻石 / 5=下界合金 / 6=基岩。**所有工具都可以写**，与 `toolTier` 分工不同，别混用 |
| `toolTier` | 整数 | 否 | `0` | **m10 A3 镐门槛等级，只有镐类物品写这个字段**：0 徒手 / 1 木镐 / 2 石镐 / 3 铁镐 / 4 钻石镐。与 blocks/*.json 的 `minToolTier` 同尺度，`BlockGating.CanDrop` 拿两者比大小——镐等级不够时挖得掉方块但**不掉落**。负数加载即抛 |
| `maxDurability` | 整数 | 否 | `0` | **m10 B1 耐久上限**：能承受的消耗次数（挖一个方块 / 挥击一次都算 1）。**镐类必须显式声明**（有守卫测试逐文件查），剑/斧/锹 m10 暂不启用（保持 0）。取值 **0..255**——存档 Metadata 只有 8 位存上限，MC 原值如钻石镐 1561 放不下，按比例缩写（钻镐写 255）。0 = 无耐久概念（永不磨损）；负数或 >255 加载即抛 |
| `gearBonus` | 对象 | 否 | 无加成 | **m10 C1 手持装备加成**（手持该物品即生效、切走即失效）：`stat` 只认 `defense`（铁系，受伤减伤点数）/ `moveSpeed`（夏季合金系，+0.05 = +5% 移速）/ `maxHealth`（机元系，生命上限 +2 点），写错词表加载即抛；`amount` 必须 > 0，写了 gearBonus 却给 0/负数也抛。**金系的攻击加成不走这里**——直接叠在 `attackDamage` 上 |
| `armorPart` | 字符串 | 否 | 非盔甲 | **m11 W2-1 盔甲部位**：只认 `helmet` / `chest` / `legs` / `boots`（写错词表加载即抛），写了就是「盔甲」、可进玩家穿戴栏 4 槽（头/胸/腿/脚，只收对应部位）。与 `gearBonus` 正交：盔甲通常两者都写（部位 + 材料属性——铁系四件 defense 1/2/2/1，合金四件 moveSpeed 0.02/0.03/0.03/0.02，机元四件 maxHealth 1/2/2/1）；金系盔甲不写 gearBonus（金的加成是攻击，走手持 `attackDamage`，盔甲无攻击属性）。**穿戴后属性从穿戴槽生效**（手持源 m10 语义保留，双源并存） |

## 关于 numericId

物品在存档里是按数字存的，`numericId` 就是这个数字。

- 既有物品显式指定了编号（1000+ 段），与 `BlockRegistry` 的数值空间互不干扰
- **新加的物品可以不写这个字段**，系统会从 1000 起按 `id` 字母顺序自动分配，
  跳过已被显式编号占用的数字

自动分配按字母排序，所以不管文件读取顺序如何，同一批物品得到的 ID 永远一样。

## m10 的两条硬约束（改镐/装备 JSON 前先读）

- **耐久上限唯一来源**：`maxDurability` 只从这张表读，首次消耗时才落进存档 Metadata
  编码（之前视为满耐久）。运行时代码**没有第二条写上限的路**——占位附魔（X 键附魔台）
  也不改写它（m10 终审修钉住）
- **升级件（`*_plus`）**：`toolTier` / `maxDurability` 必须与基底件一致（升级只升加成，
  `gearBonus.amount` / `attackDamage` 翻倍），且**满耐久**才能当合成材料（防洗耐久）

## 改完怎么验证

在项目根目录运行：

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

`ItemDatabaseTests` 会检查所有定义文件能否正确解析、ID 有没有冲突、
`toolTier` / `maxDurability` / `gearBonus` 取值是否合法；
`BlockDropsTests` 等真实加载器集成测试会抓住悬空的 itemId 跨表引用；
镐类必显式声明 `toolTier` / `maxDurability` 由 `BlockGatingTests` 的
`RealItems_EveryPickaxe_*` 守卫逐文件检查。打错字会立刻报错，不用等进游戏。

## 常见错误

| 现象 | 原因 |
| --- | --- |
| 报「未注册的物品 id」 | 别的表（配方/掉落/任务）引用了这个 id，但这里没有对应文件 |
| 报「物品 numericId 重复」 | 两个文件用了同一个数字 |
| 报「toolTier 必须 ≥ 0」 | 填了负数——0 已经是「视同徒手」，没有更低的档 |
| 报「maxDurability 必须在 0..255」 | 填了 MC 原值（如钻石镐 1561）——Metadata 只有 8 位，按比例缩写 |
| 报「gearBonus.stat 只认 defense / moveSpeed / maxHealth」 | stat 词表写错 |
| 报「gearBonus.amount 必须 > 0」 | 不想要加成就整个删掉 `gearBonus` 字段 |
| 报「不是合法的 JSON」 | 多半是少了逗号，或者多了个尾随逗号 |
