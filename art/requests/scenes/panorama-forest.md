# SCENE-09 群系全景 · Forest 森林

## 用途

群系图鉴五张全景之三：**Forest 森林**（biomes.json id 2，温度 0.4 / 湿度 0.8 /
树密度 30——五种群里最密）。白天、光斑林地、无文字。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 树冠层绿 | `#24491C` `#2F5D24` `#3F7A2E` `#52963B` |
| 树干 | `#4E3B27` `#634C33` |
| 林下草 | `#4A7E2F` `#5D9C3C` |
| 蘑菇 | `#A05242` `#EADDB4` |
| 溪流 | `#3A6FB5` |
| 天空 | `#79A6FF` |

## 视觉描述

- 密集体素橡林：近景树干粗、树冠深，远景渐亮，层层后退
- 树冠用 `leaves` 的深冷绿系（比草地更深更冷，压出层次）
- 林下草地洒着斑驳光斑；树根处一丛红帽蘑菇与一丛褐蘑菇
- 一条窄溪蜿蜒穿过林地；远处树干之间有个狐狸的小剪影
- 天空只露出零星缝隙（密林）

## AI 提示词

```
Voxel pixel art panorama of the Forest biome for a sandbox block game
biome codex, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, layered depth from dark near trees to
lighter distant canopy, no realistic perspective, no painterly shading.

Content: a dense voxel oak forest with thick trunks in front and layered
crowns receding to lighter greens behind, dark cool leaf colors clearly
darker than the grass. On the shady forest floor, patches of dappled
light spots, one cluster of red-cap mushrooms and one cluster of brown
mushrooms at the trunk roots, a narrow brook winding through, and a small
fox silhouette between distant trunks. Only small slivers of blue sky
show through the canopy.

Palette: leaf greens #24491C #2F5D24 #3F7A2E #52963B, trunks #4E3B27
#634C33, forest grass #4A7E2F #5D9C3C, mushrooms #A05242 #EADDB4, brook
#3A6FB5, sky slivers #79A6FF.

No text, no letters, no logo, no UI, no watermark, no humans, no
buildings, no photorealism, no 3D render, no blur, no smooth gradients,
no vignette.
```

## 负面提示词

```
text, letters, logo, UI, watermark, humans, buildings, photorealism, 3D
render, blur, smooth gradients, vignette, autumn colors, conifer forest
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 树冠绿比林下草**更深更冷**（`leaves` 铁律），层次拉开
- [ ] 树要密（树密度 30，五群系之最），不能画成稀树草原
- [ ] 全是阔叶橡树形态，不出现针叶（那是 Snow/Mountains 的事）
- [ ] 无文字、无人形
