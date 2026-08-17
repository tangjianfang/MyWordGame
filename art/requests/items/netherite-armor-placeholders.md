# m11 W3-3 批量 · 4 张下界合金盔甲物品贴图（程序占位）

## 背景

milestone-11 第 3 波（W3-3 机元守卫 Boss + 下界合金链）注册了四件盔甲物品——
Boss 掉 `netherite_ingot`，与铁件合成升级：

| 物品 | id | 属性（gearBonus） |
| --- | --- | --- |
| 下界合金头盔 | `netherite_helmet` | `defense +2`（铁 1 翻倍） |
| 下界合金胸甲 | `netherite_chest` | `defense +4`（铁 2 翻倍） |
| 下界合金护腿 | `netherite_legs` | `defense +4`（铁 2 翻倍） |
| 下界合金靴子 | `netherite_boots` | `defense +2`（铁 1 翻倍） |

它们的 `texture` 名在 `items/textures/` 没有 PNG，做出来拿在手里会显示
品红占位块（m6 / m10 批同款问题）。本批全部为**程序占位**
（`art/scripts/gen_m11_netherite_armor_placeholders.py` 确定性生成），不是正式美术。

## 通用规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255，透明背景） |
| 生成方式 | ASCII 形状 + 整数哈希铺色（m6/m10 批同模式） |
| 保留色 | 不出现 `#FF00FF` |

## 造型与色系

四件共用四系盔甲的既成剪影（与已入库的 helmet/chest/legs/boots-iron|gold|
summer-alloy|machine-essence 同构）：头盔圆顶+横贯眼缝 / 胸甲肩带+中央核心 /
护腿腰带+双腿分叉 / 双靴前伸。色系取**深紫灰**（比机元 `#7A5A9A` 暗一档——
Boss 紫金机甲家族的深色支），金饰与 `art/requests/entities/machine-guardian.md`
的金饰同源——「Boss 掉的锭打的甲」一眼看出师承：

| 角色 | 色阶 |
| --- | --- |
| 主色 S | `#3F3742` / `#332C38` |
| 受光 L | `#5C4F5C` / `#6A5C6A` |
| 暗边 B | `#241F27` / `#1B171E` |
| 金饰 G（眼缝/胸核/腰带扣） | `#DCAE3A` / `#A87322` |

## 后处理

无（程序直出硬边像素，不经过 AI 与洋红键控链路）。

## 正式美术替换要求

正式图按 iron/gold 系盔甲图标同规格做：32×32、正视单体、深色描边 1px、
透明背景（洋红键控），保持「深紫灰金属 + 金镶边」的家族识别。
替换后把 `art/README.md` 索引表的状态改为「已入库」。
