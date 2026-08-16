using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Items
{
    /// <summary>
    /// m7 B1：<see cref="PlayerContext.ItemDrops"/> → <see cref="ItemDropView"/> 的同步注册表，
    /// 修「挖完看不到掉落物」——掉落实体（B7）一直存在，只是从没有过视觉。
    /// <para>
    /// 每帧 diff：列表新出现的实体建视图，消失的（拾取入包 / 读档整体替换 / 清空）毁视图。
    /// 模式抄 <see cref="MyWorld.Unity.Rendering.ChunkViewRegistry"/> 的注册表思路，但掉落物
    /// 量级 ≤60（挖一会儿最多几十个），全量 diff 就够，不需要脏标记 / 分帧预算。
    /// </para>
    /// <para>Update 自驱动；EditMode 测试不自动跑 Update，手动调 <see cref="SyncNow"/> 步进。</para>
    /// </summary>
    public sealed class ItemDropViewRegistry : MonoBehaviour
    {
        private readonly Dictionary<ItemDropEntity, ItemDropView> _views =
            new Dictionary<ItemDropEntity, ItemDropView>();

        // 复用的 scratch 集合：每帧 diff 不分配新容器（量级小，但 Update 每帧跑）
        private readonly HashSet<ItemDropEntity> _liveScratch = new HashSet<ItemDropEntity>();
        private readonly List<ItemDropEntity> _deadScratch = new List<ItemDropEntity>();

        private PlayerContext _context;
        private Transform _parent;

        /// <summary>当前建着的视图数（测试 / 调试用）。</summary>
        public int ViewCount => _views.Count;

        /// <summary>绑定数据源与视图挂载点（世界根节点）。Bind 前 SyncNow 是 no-op。</summary>
        public void Bind(PlayerContext context, Transform parent)
        {
            _context = context;
            _parent = parent;
        }

        private void Update() => SyncNow();

        /// <summary>diff 一帧：新实体建视图、消失 / 已拾空的实体毁视图。
        /// 拾空壳（Content=null 还留在列表里）同样毁视图——正常拾取路径会同步 RemoveAt，
        /// 这里兜底「壳不渲染」。</summary>
        public void SyncNow()
        {
            if (_context == null) return;
            var drops = _context.ItemDrops;

            // 1) 建新：还没视图的活实体
            foreach (var drop in drops)
            {
                if (drop == null || drop.Content == null) continue;
                if (_views.ContainsKey(drop)) continue;
                _views[drop] = ItemDropView.Create(_parent, drop, _context.Items);
            }

            // 2) 毁旧：不在当前列表里（拾取移除 / 读档 Clear+重灌）或已拾空的实体
            _liveScratch.Clear();
            foreach (var drop in drops)
            {
                if (drop != null && drop.Content != null) _liveScratch.Add(drop);
            }

            _deadScratch.Clear();
            foreach (var pair in _views)
            {
                if (!_liveScratch.Contains(pair.Key)) _deadScratch.Add(pair.Key);
            }
            foreach (var drop in _deadScratch)
            {
                DestroyView(drop);
            }
        }

        /// <summary>按实体取视图（测试 / 调试用）。没有对应视图返回 false。</summary>
        public bool TryGetView(ItemDropEntity drop, out ItemDropView view)
        {
            return _views.TryGetValue(drop, out view);
        }

        /// <summary>场景卸载时把视图一并清掉——实体列表是 PlayerContext 的，不跟着本组件走，
        /// 不主动清会留下一地父节点已销毁的孤儿视图。</summary>
        private void OnDestroy()
        {
            _deadScratch.Clear();
            _deadScratch.AddRange(_views.Keys);
            foreach (var drop in _deadScratch)
            {
                DestroyView(drop);
            }
        }

        private void DestroyView(ItemDropEntity drop)
        {
            if (!_views.TryGetValue(drop, out var view)) return;
            _views.Remove(drop);
            if (view != null)
            {
                // 编辑器非播放态下 Destroy 不生效，必须走 DestroyImmediate（与 ChunkViewRegistry 同款）
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
            }
        }
    }
}
