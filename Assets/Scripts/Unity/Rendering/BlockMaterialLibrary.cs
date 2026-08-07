using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 贴图索引 → 材质。贪心网格的 UV 是按格数铺开的（(0,0)..(width,height)），
    /// 所以材质只要开 Repeat 环绕，合并后的大面就能保持单个方块的贴图密度，无需自定义 shader。
    /// </summary>
    public sealed class BlockMaterialLibrary : IDisposable
    {
        private readonly Material[] _materials;
        private readonly Material _missing;
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        private BlockMaterialLibrary(int slotCount, Material missing)
        {
            _materials = new Material[slotCount];
            _missing = missing;
        }

        public int Count => _materials.Length;

        /// <summary>越界或缺贴图时给出醒目的洋红占位材质，让问题在画面上一眼可见而不是静默消失。</summary>
        public Material Get(int textureIndex)
            => textureIndex >= 0 && textureIndex < _materials.Length ? _materials[textureIndex] : _missing;

        public static BlockMaterialLibrary Load(BlockRegistry registry, string textureDirectory)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            Shader shader = FindShader();
            var library = new BlockMaterialLibrary(registry.TextureNames.Count, CreateMissingMaterial(shader));

            for (var slot = 0; slot < registry.TextureNames.Count; slot++)
            {
                string textureName = registry.TextureNames[slot];
                string path = Path.Combine(textureDirectory, textureName + ".png");
                Texture2D texture = library.LoadTexture(path);

                if (texture == null)
                {
                    Debug.LogError($"方块贴图缺失或无法解码：{path}，该贴图改用占位材质。");
                    library._materials[slot] = library._missing;
                    continue;
                }

                library._materials[slot] = new Material(shader)
                {
                    name = textureName,
                    mainTexture = texture
                };
            }

            return library;
        }

        /// <summary>装了 URP 就用 URP 的 Lit，没装则退回内置管线的 Standard，两条路都能跑。</summary>
        private static Shader FindShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit 与内置 Standard 都找不到，无法建立方块材质。");
            }

            return shader;
        }

        private Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                DestroyObject(texture);
                return null;
            }

            // 必须在 LoadImage 之后设置：LoadImage 会按 PNG 重建纹理，之前的采样设置会丢
            // 32×32 像素风要 Point 过滤；关掉 mipmap，否则远处方块会糊成一团
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;

            _textures.Add(texture);
            return texture;
        }

        private static Material CreateMissingMaterial(Shader shader)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta });
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            return new Material(shader) { name = "缺失贴图", mainTexture = texture };
        }

        /// <summary>编辑器里 Destroy 要到帧末才生效，非播放态下必须用 DestroyImmediate。</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public void Dispose()
        {
            foreach (Material material in _materials)
            {
                // 缺贴图的槽位共用 _missing，别重复销毁
                if (material != null && material != _missing)
                {
                    DestroyObject(material);
                }
            }

            if (_missing != null)
            {
                DestroyObject(_missing.mainTexture);
                DestroyObject(_missing);
            }

            foreach (Texture2D texture in _textures)
            {
                DestroyObject(texture);
            }

            _textures.Clear();
        }
    }
}
