using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// 透明水面 mesh 简单实现：每帧扫世界 [0..63] × [0..63] 范围，
    /// 把水方块顶部画成一个半透明 quad。
    /// plan-3b 再升级为 MeshRenderer + 波纹。
    /// </summary>
    public sealed class WaterRenderer : MonoBehaviour
    {
        public int ScanRadius = 32;
        public int ScanHeightMax = 100;
        public Material WaterMaterial;

        private void Start()
        {
            if (WaterMaterial == null)
            {
                // 透明蓝
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    WaterMaterial = new Material(shader);
                    WaterMaterial.color = new Color(0.2f, 0.4f, 0.8f, 0.6f);
                    WaterMaterial.SetFloat("_Surface", 1);
                    WaterMaterial.SetFloat("_Blend", 0);
                    WaterMaterial.SetFloat("_Alpha", 0.6f);
                    WaterMaterial.renderQueue = 3000;
                }
            }
        }

        private void Update()
        {
            // 简化：plan-3a 只设材质，渲染交给 ChunkSectionView 的水方块透明度（Opaque=false）
            // plan-3b 再加动态水面 plane
        }
    }
}
