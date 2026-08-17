# m10 批量 · 6 张装备物品贴图（程序占位）

## 背景

milestone-10 C1（装备属性）从零建了六件装备物品：

| 物品 | id | 属性（gearBonus） |
| --- | --- | --- |
| 金剑 | `gold_sword` | 攻击 +1（走 attackDamage=5，不写 gearBonus） |
| 金镐 | `gold_pickaxe` | 同上（toolTier=2 / 耐久 32） |
| 夏季合金剑 | `summer_alloy_sword` | `moveSpeed +0.05`（+5%） |
| 夏季合金镐 | `summer_alloy_pickaxe` | 同上 |
| 机元剑 | `machine_essence_sword` | `maxHealth +2` |
| 机元镐 | `machine_essence_pickaxe` | 同上 |

它们的 `texture` 名在 `items/textures/` 没有 PNG，做出来拿在手里会显示
品红占位块（m6 批 / m10 A1 fix1 同款问题）。金剑/金镐是实机验收剧本第 3 步
（铁镐挖到金矿 → 做金剑）的直接产物，必须先有图。

本批全部为**程序占位**（`art/scripts/gen_m10_gear_placeholders.py` 确定性生成），
不是正式美术。正式图到位后按本文件调色板逐张替换即可。

## 通用规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255，透明背景） |
| 生成方式 | ASCII 形状 + 整数哈希铺色（与 m6 批 / M10-C 同模式） |
| 保留色 | 不出现 `#FF00FF` |

## 造型与色系

剑 / 镐各共用一套剪影（对角挥出的刃 + 护手木柄 / 拱形镐头 + 斜柄），
材料按各自矿石掉落物图标（M10-C 批）的色系上色——图标即属性提示，
孩子看到颜色就该想到「这把剑给我什么」：

| 名称 | 剪影 | 主色（刃/镐头） | 受光 | 暗边 | 握柄 |
| --- | --- | --- | --- | --- | --- |
| gold_sword / gold_pickaxe | 剑 / 镐 | 金 `#E8C04A`/`#C09A34` | `#F7E08A`/`#FFEFA8` | `#A8842E` | 木棕 `#634C33` |
| summer_alloy_sword / summer_alloy_pickaxe | 剑 / 镐 | 青蓝 `#4A9AB8`/`#35758E` | `#78C2DC`/`#9AD8EA` | `#2A5F75` | 木棕 `#634C33` |
| machine_essence_sword / machine_essence_pickaxe | 剑 / 镐 | 紫灰 `#7A5A9A`/`#5E4478` | `#9C82BC`/`#B9A2D4` | `#4A3560` | 木棕 `#634C33` |

六张与既有六系剑/镐并排要能一眼分队：金最亮最黄、合金偏蓝、机元紫而发光；
握柄统一木棕（与既有木/石/铁/钻/下界合金/基岩剑镐一致）。

## 后处理

无（程序直出硬边像素，不经过 AI 与洋红键控链路）。

## 验收

- 6 张全部存在：`items/*.json` 引用与 `items/textures/*.png` 双向差集为空；
  EditMode `ItemTextureFileTests`（已把六项纳入 CoveredItems）不回归
- 16×16、32 位 RGBA、Alpha 仅 0 / 255、透明像素 RGB=000000
- 确定性：重复运行生成脚本，PNG 逐字节一致
