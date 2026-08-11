# 一次性脚本：生成里程碑-3 全部物品 / 配方 / 新方块 JSON + 占位 missing 贴图
# 用法：python3 tools/scripts/gen-gameplay-assets.py （仓库根跑）

import json
import os
import struct
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLOCKS_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "blocks")
ITEMS_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "items")
RECIPES_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "recipes")
BLOCK_TX_DIR = os.path.join(BLOCKS_DIR, "textures")
ITEM_TX_DIR = os.path.join(ROOT, "Assets", "Art", "Items")

os.makedirs(ITEMS_DIR, exist_ok=True)
os.makedirs(RECIPES_DIR, exist_ok=True)
os.makedirs(ITEM_TX_DIR, exist_ok=True)


def make_missing_png(path, size, color=(160, 32, 240)):
    w = h = size
    r, g, b = color
    raw = b"\x00" + bytes([r, g, b]) * w
    raw = raw * h
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    idat = zlib.compress(raw)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


# 占位贴图
make_missing_png(os.path.join(BLOCK_TX_DIR, "missing.png"), 32)
make_missing_png(os.path.join(ITEM_TX_DIR, "missing.png"), 16)

# 新方块 JSON
NEW_BLOCKS = [
    ("planks",     "木板",       1000, "planks"),
    ("log",        "原木",       1001, "log-side", "log-top"),
    ("leaves",     "树叶",       1002, "leaves"),
    ("sapling",    "树苗",       1003, "sapling"),
    ("crafting_table", "工作台", 1004, "crafting_table-top", "crafting_table-side"),
    ("iron_door",  "铁门",       1005, "iron_door"),
    ("lever",      "拉杆",       1006, "lever"),
    ("redstone_dust", "红石粉",  1007, "redstone_dust"),
]

def block_json(item_id, name, nid, *tex):
    if len(tex) == 1:
        textures = {"all": tex[0]}
    elif len(tex) == 2:
        textures = {"top": tex[1], "bottom": tex[0], "side": tex[0]}
    else:
        textures = {"all": tex[0]}
    return {
        "id": item_id,
        "displayName": name,
        "numericId": nid,
        "textures": textures,
        "solid": True,
        "opaque": True,
        "lightEmission": 0,
        "hardness": 2.0,
    }

for entry in NEW_BLOCKS:
    fid, name, nid, *tex = entry
    with open(os.path.join(BLOCKS_DIR, f"{fid}.json"), "w", encoding="utf-8") as f:
        json.dump(block_json(fid, name, nid, *tex), f, ensure_ascii=False, indent=2)

# 物品 JSON
ITEMS = [
    # 资源类
    ("log",          "原木",          1000, 64, None, None, "log"),
    ("plank",        "木板",          1001, 64, None, None, "plank"),
    ("stick",        "木棍",          1002, 64, None, None, "stick"),
    ("cobblestone",  "圆石",          1003, 64, None, None, "cobblestone"),
    ("iron_ingot",   "铁锭",          1004, 64, None, None, "iron_ingot"),
    ("diamond",      "钻石",          1005, 64, None, None, "diamond"),
    ("netherite_ingot", "下界合金锭", 1006, 64, None, None, "netherite_ingot"),
    ("coal",         "煤炭",          1007, 64, None, None, "coal"),
    ("raw_porkchop", "生猪排",        1008, 64, None, 3, "raw_porkchop"),
    ("wool",         "羊毛",          1009, 64, None, None, "wool"),
    ("rotten_flesh", "腐肉",          1010, 64, None, 4, "rotten_flesh"),
    ("beet",         "甜菜",          1011, 64, None, 1, "beet"),
    ("mung_bean",    "绿豆",          1012, 64, None, 1, "mung_bean"),
    ("bowl",         "碗",            1013, 64, None, None, "bowl"),
    # bedrock 作为合成材料（bedrock 方块也保留）
    ("bedrock",      "基岩碎片",      1014, 1,  None, None, "bedrock"),

    # 食物（汤）
    ("beet_soup",    "甜菜汤",        1020, 1, None, 6, "beet_soup"),
    ("mung_bean_soup", "绿豆汤",      1021, 1, None, 8, "mung_bean_soup"),
    ("bowl_of_water", "水碗",         1022, 1, None, 0, "bowl_of_water"),

    # 武器
    ("wooden_sword",    "木剑",       1100, 1, 4,    None, "wooden_sword", True, 1),
    ("stone_sword",     "石剑",       1101, 1, 5.5,  None, "stone_sword", True, 2),
    ("iron_sword",      "铁剑",       1102, 1, 10,   None, "iron_sword", True, 3),
    ("diamond_sword",   "钻石剑",     1103, 1, 19,   None, "diamond_sword", True, 4),
    ("netherite_sword", "下界合金剑", 1104, 1, 24,   None, "netherite_sword", True, 5),
    ("bedrock_sword",   "基岩剑",     1105, 1, 50,   None, "bedrock_sword", True, 6),

    # 家具
    ("table",       "桌子",           1200, 64, None, None, "table"),
    ("chair",       "椅子",           1201, 64, None, None, "chair"),
    ("office_desk", "办公桌",         1202, 64, None, None, "office_desk"),
    ("laptop",      "笔记本电脑",     1203, 1,  None, None, "laptop"),
    ("notebook",    "笔记本",         1204, 64, None, None, "notebook"),
    ("keyboard",    "键盘",           1205, 64, None, None, "keyboard"),
    ("mouse",       "鼠标",           1206, 64, None, None, "mouse"),
    ("globe",       "地球仪",         1207, 64, None, None, "globe"),
    ("hacker_pc",   "黑客电脑",       1208, 1,  None, None, "hacker_pc"),

    # 战斗
    ("arrow",       "箭",             1300, 64, None, None, "arrow"),

    # 镐 / 斧 / 锹（plan-3b）
    ("wooden_pickaxe",  "木镐",      1400, 1, 2, None, "wooden_pickaxe", True, 1),
    ("stone_pickaxe",   "石镐",      1401, 1, 3, None, "stone_pickaxe", True, 2),
    ("iron_pickaxe",    "铁镐",      1402, 1, 4, None, "iron_pickaxe", True, 3),
    ("diamond_pickaxe", "钻石镐",    1403, 1, 5, None, "diamond_pickaxe", True, 4),
    ("netherite_pickaxe", "下界合金镐", 1404, 1, 6, None, "netherite_pickaxe", True, 5),
    ("bedrock_pickaxe", "基岩镐",    1405, 1, 99, None, "bedrock_pickaxe", True, 99),
    ("wooden_axe",  "木斧",          1410, 1, 7, None, "wooden_axe", True, 1),
    ("stone_axe",   "石斧",          1411, 1, 9, None, "stone_axe", True, 2),
    ("iron_axe",    "铁斧",          1412, 1, 9, None, "iron_axe", True, 3),
    ("wooden_shovel", "木锹",        1420, 1, 2, None, "wooden_shovel", True, 1),
    ("stone_shovel",  "石锹",        1421, 1, 3, None, "stone_shovel", True, 2),
    ("iron_shovel",   "铁锹",        1422, 1, 4, None, "iron_shovel", True, 3),

    # 红石 / 杂项（plan-3b）
    ("redstone",         "红石",       1500, 64, None, None, "redstone"),
    ("string_",          "线",         1501, 64, None, None, "string"),    # id 避免与 C# 关键字冲突
    ("gunpowder",        "火药",       1502, 64, None, None, "gunpowder"),
    ("bone",             "骨头",       1503, 64, None, None, "bone"),
    ("skull",            "骷髅头颅",   1504, 64, None, None, "skull"),

    # 交易 / 附魔（plan-3c）
    ("emerald",          "绿宝石",     1600, 64, None, None, "emerald"),
    ("book",             "书",         1601, 64, None, None, "book"),
    ("enchanted_book",   "附魔书",     1602, 1,  None, None, "enchanted_book"),
    ("lapis",            "青金石",     1603, 64, None, None, "lapis"),
]


def item_json(fid, name, nid, max_stack, dmg, heal, tex, is_tool=False, ml=0):
    d = {
        "id": fid,
        "displayName": name,
        "numericId": nid,
        "maxStack": max_stack,
        "texture": tex,
    }
    if dmg is not None:
        d["attackDamage"] = dmg
    if heal is not None:
        d["healAmount"] = heal
    if is_tool:
        d["isTool"] = True
        d["miningLevel"] = ml
    return d


for entry in ITEMS:
    fid, name, nid, ms, dmg, heal, tex, *rest = entry
    is_tool = bool(rest[0]) if len(rest) > 0 else False
    ml = rest[1] if len(rest) > 1 else 0
    with open(os.path.join(ITEMS_DIR, f"{fid}.json"), "w", encoding="utf-8") as f:
        json.dump(item_json(fid, name, nid, ms, dmg, heal, tex, is_tool, ml), f, ensure_ascii=False, indent=2)

# 配方 JSON
RECIPES = [
    ("log_to_planks",    "pocket",    1, 1, ["log"],
     {"item": "plank", "count": 4}, False),

    ("planks_to_sticks", "inventory", 2, 2, ["plank", "_", "_", "plank"],
     {"item": "stick", "count": 4}, False),
    ("planks_to_bowl",   "inventory", 2, 2, ["plank", "plank", "plank", "_"],
     {"item": "bowl", "count": 4}, False),

    ("planks_to_crafting_table", "workbench", 3, 3,
     ["plank", "plank", "_", "plank", "plank", "_", "_", "_", "_"],
     {"item": "crafting_table", "count": 1}, False),

    ("wooden_sword_recipe",    "workbench", 1, 3, ["plank", "plank", "stick"],
     {"item": "wooden_sword", "count": 1}, True),
    ("stone_sword_recipe",     "workbench", 1, 3, ["cobblestone", "cobblestone", "stick"],
     {"item": "stone_sword", "count": 1}, True),
    ("iron_sword_recipe",      "workbench", 1, 3, ["iron_ingot", "iron_ingot", "stick"],
     {"item": "iron_sword", "count": 1}, True),
    ("diamond_sword_recipe",   "workbench", 1, 3, ["diamond", "diamond", "stick"],
     {"item": "diamond_sword", "count": 1}, True),
    ("netherite_sword_recipe", "workbench", 1, 3, ["netherite_ingot", "netherite_ingot", "stick"],
     {"item": "netherite_sword", "count": 1}, True),
    ("bedrock_sword_recipe",   "workbench", 1, 3, ["bedrock", "bedrock", "stick"],
     {"item": "bedrock_sword", "count": 1}, True),

    ("beet_soup_recipe",    "inventory", 2, 2, ["beet", "bowl"],
     {"item": "beet_soup", "count": 1}, False),
    ("mung_bean_soup_recipe", "inventory", 2, 2, ["mung_bean", "bowl"],
     {"item": "mung_bean_soup", "count": 1}, False),

    # 镐 / 斧 / 锹（plan-3b）：1x3 pattern
    ("wooden_pickaxe_recipe",  "workbench", 1, 3, ["plank", "plank", "plank", "stick", "_", "stick"],
     {"item": "wooden_pickaxe", "count": 1}, True),
    ("stone_pickaxe_recipe",   "workbench", 1, 3, ["cobblestone", "cobblestone", "cobblestone", "stick", "_", "stick"],
     {"item": "stone_pickaxe", "count": 1}, True),
    ("iron_pickaxe_recipe",    "workbench", 1, 3, ["iron_ingot", "iron_ingot", "iron_ingot", "stick", "_", "stick"],
     {"item": "iron_pickaxe", "count": 1}, True),
    ("diamond_pickaxe_recipe", "workbench", 1, 3, ["diamond", "diamond", "diamond", "stick", "_", "stick"],
     {"item": "diamond_pickaxe", "count": 1}, True),
    ("netherite_pickaxe_recipe", "workbench", 1, 3, ["netherite_ingot", "netherite_ingot", "netherite_ingot", "stick", "_", "stick"],
     {"item": "netherite_pickaxe", "count": 1}, True),
    ("bedrock_pickaxe_recipe", "workbench", 1, 3, ["bedrock", "bedrock", "bedrock", "stick", "_", "stick"],
     {"item": "bedrock_pickaxe", "count": 1}, True),

    ("wooden_axe_recipe",  "workbench", 1, 3, ["plank", "plank", "plank", "stick", "_", "stick"],
     {"item": "wooden_axe", "count": 1}, False),
    ("stone_axe_recipe",   "workbench", 1, 3, ["cobblestone", "cobblestone", "cobblestone", "stick", "_", "stick"],
     {"item": "stone_axe", "count": 1}, False),
    ("iron_axe_recipe",    "workbench", 1, 3, ["iron_ingot", "iron_ingot", "iron_ingot", "stick", "_", "stick"],
     {"item": "iron_axe", "count": 1}, False),

    ("wooden_shovel_recipe", "workbench", 1, 3, ["plank", "_", "stick"],
     {"item": "wooden_shovel", "count": 1}, True),
    ("stone_shovel_recipe",  "workbench", 1, 3, ["cobblestone", "_", "stick"],
     {"item": "stone_shovel", "count": 1}, True),
    ("iron_shovel_recipe",   "workbench", 1, 3, ["iron_ingot", "_", "stick"],
     {"item": "iron_shovel", "count": 1}, True),

    # 红石基础合成
    ("redstone_dust_to_redstone", "pocket", 1, 1, ["redstone_dust"],
     {"item": "redstone", "count": 1}, False),
]

for entry in RECIPES:
    rid, tier, w, h, pat, out, shaped = entry
    d = {
        "id": rid,
        "tier": tier,
        "width": w,
        "height": h,
        "pattern": pat,
        "shaped": shaped,
        "output": out,
    }
    with open(os.path.join(RECIPES_DIR, f"{rid}.json"), "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)

print("OK")
print(f"  blocks:  {len(NEW_BLOCKS)}")
print(f"  items:   {len(ITEMS)}")
print(f"  recipes: {len(RECIPES)}")
