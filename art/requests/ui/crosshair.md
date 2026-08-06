# U-01 准星

## 用途

屏幕正中央的瞄准标记，指示当前射线拾取的方向。**始终叠加在世界画面之上**，
因此必须在明亮的天空和黑暗的洞穴中都清晰可见。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **需要**，大部分区域全透明 |
| 颜色数 | 3 色（白、深灰描边、透明） |

## 构图

一个居中的**十字准星**，四臂等长：

| 项 | 值 |
| --- | --- |
| 臂长 | 从中心向外 5 – 11 像素（即每臂长 7 像素） |
| 臂宽 | 2 像素 |
| 中心空缺 | 中央 3 × 3 像素区域**留空**（全透明） |
| 描边 | 白色十字外围包 1 像素 `#1A1A1A` 深灰描边 |

**中心必须留空**——实心中心会挡住玩家正在瞄准的目标。

**描边不能省**——纯白准星在雪地、沙滩、天空背景下会完全消失，
深灰描边是保证任何背景下都可见的关键。

## 调色板

| 用途 | HEX | Alpha |
| --- | --- | --- |
| 准星主体 | `#FFFFFF` | 255 |
| 描边 | `#1A1A1A` | 255 |
| 背景 | — | 0 |

## AI 提示词

```
A pixel art crosshair icon for a first-person game HUD, 512x512, on a fully
transparent background, designed to be downscaled to 32x32 pixel art.

Shape: a simple symmetrical plus-shaped crosshair centered in the image. Four equal
arms pointing up, down, left and right. The very center is EMPTY (transparent) —
the arms do not meet in the middle, leaving a small square gap.

Style: crisp hard-edged pixel art, no anti-aliasing, no blur, no glow.
The crosshair is pure white with a thin dark outline around it for contrast
against any background.

Colors: white #FFFFFF for the arms, dark gray #1A1A1A for the 1-pixel outline.
Everything else fully transparent.

The arms are thin, the whole crosshair occupies roughly the middle two thirds of
the image with generous transparent margin around it.

No circle, no dot in the center, no scope, no reticle markings, no numbers,
no text, no watermark, no background color, no gradient, no glow, no shadow.
```

## 负面提示词

```
filled center, center dot, circle, ring, scope, sniper reticle, range markings,
numbers, text, watermark, background color, opaque background, gradient, glow,
bloom, shadow, blur, anti-aliasing, 3D render, colored crosshair, red, green
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到纯白 + `#1A1A1A` 两色
3. **背景 Alpha 严格置 0**，清除所有半透明边缘像素
4. 核对中央 3 × 3 为全透明
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 中央 3 × 3 像素全透明
- [ ] 四臂长度与宽度完全对称（水平翻转、垂直翻转后与原图一致）
- [ ] 白色主体外有完整 1 像素深灰描边，无缺口
- [ ] 除主体与描边外，其余像素 Alpha 均为 0
- [ ] 无任何半透明像素（Alpha 只能是 0 或 255）
- [ ] 分别放在纯白与纯黑背景上，都清晰可辨
