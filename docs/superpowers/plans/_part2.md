
## Task 4: 放置位置与合法性

**Files:**
- Create: `Assets/Scripts/Core/Player/BlockPlacement.cs`
- Create: `Assets/Tests/EditMode/Player/BlockPlacementTests.cs`

- [ ] **Step 1: 先写失败的测试**

```csharp
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>由射线命中推出放置格，并挡住三类非法放置。</summary>
    [TestFixture]
    public class BlockPlacementTests
    {
        private static VoxelRayHit HitAt(int x, int y, int z, int nx, int ny, int nz)
            => new VoxelRayHit { Hit = true, X = x, Y = y, Z = z, NormalX = nx, NormalY = ny, NormalZ = nz };

        /// <summary>玩家站在 (0.5, 64, 0.5)，包围盒占据 x/z 的 [0.2, 0.8]、y 的 [64, 65.8]。</summary>
        private static Aabb Player() => Aabb.FromBottomCenter(new Float3(0.5f, 64f, 0.5f), 0.6f, 1.8f);

        [Test]
        public void Resolve_PlacesAgainstHitFace()
        {
            bool ok = BlockPlacement.TryResolve(HitAt(5, 64, 5, 0, 1, 0), Player(), out int x, out int y, out int z);

            Assert.That(ok, Is.True);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { 5, 65, 5 }),
                "打在方块顶面上，新方块应当放在它正上方");
        }

        [Test]
        public void Resolve_WithoutHit_Fails()
        {
            Assert.That(BlockPlacement.TryResolve(default, Player(), out _, out _, out _), Is.False,
                "射线没打中任何方块时不应当放置");
        }

        [Test]
        public void Resolve_InsidePlayerBox_Fails()
        {
            // 打在玩家脚下那格的顶面 → 目标格正是玩家站的位置
            bool ok = BlockPlacement.TryResolve(HitAt(0, 63, 0, 0, 1, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.False, "不能把方块放到玩家身体里，否则会把自己封住");
        }

        [Test]
        public void Resolve_BesidePlayer_Succeeds()
        {
            // 玩家包围盒 x 只占 [0.2, 0.8]，x = 1 那一列是空的
            bool ok = BlockPlacement.TryResolve(HitAt(2, 64, 0, -1, 0, 0), Player(), out int x, out _, out _);

            Assert.That(ok, Is.True, "紧挨着玩家但不重叠的格子应当能放");
            Assert.That(x, Is.EqualTo(1));
        }

        [TestCase(319, 1, true)]
        [TestCase(-64, -1, false)]
        [TestCase(319, -1, true)]
        public void Resolve_ChecksWorldHeightLimits(int hitY, int normalY, int expected)
        {
            bool ok = BlockPlacement.TryResolve(HitAt(50, hitY, 50, 0, normalY, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.EqualTo(expected),
                "超出世界高度 [-64, 320) 的格子不应当能放");
        }
    }
}
```

- [ ] **Step 2: 实现**

```csharp
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Player
{
    /// <summary>由射线命中结果推出放置位置，并判定这次放置是否合法。</summary>
    public static class BlockPlacement
    {
        public static bool TryResolve(VoxelRayHit hit, Aabb playerBox, out int x, out int y, out int z)
        {
            x = 0;
            y = 0;
            z = 0;

            if (!hit.Hit)
            {
                return false;
            }

            // 沿命中面的法线往外挪一格，就是新方块该占的位置
            x = hit.X + hit.NormalX;
            y = hit.Y + hit.NormalY;
            z = hit.Z + hit.NormalZ;

            if (y < VoxelCoords.MinY || y >= VoxelCoords.MaxY)
            {
                return false;
            }

            // 这一条是必须的：不挡住的话玩家能把自己封进方块里出不来
            return !IntersectsPlayer(x, y, z, playerBox);
        }

        /// <summary>
        /// 格子与玩家包围盒是否重叠。用严格不等号——正好贴面（比如脚底那格的顶面 y = 64
        /// 对上包围盒底面 y = 64）不算重叠，否则站在地上时脚下一圈全都放不了。
        /// </summary>
        private static bool IntersectsPlayer(int x, int y, int z, Aabb box)
        {
            return box.Min.X < x + 1 && box.Max.X > x
                && box.Min.Y < y + 1 && box.Max.Y > y
                && box.Min.Z < z + 1 && box.Max.Z > z;
        }
    }
}
```

- [ ] **Step 3: 跑测试确认变绿，然后提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockPlacementTests"
git add Assets/Scripts/Core/Player Assets/Tests/EditMode/Player
git commit -m "玩家层: 放置位置解算与合法性判定"
```

---

## Task 5: 改一个方块之后哪些区块段变脏

贪心网格生成时会采样邻居来剔除接缝面，所以改一个方块牵连的不只是它所在的段。
这段是纯坐标运算，放 Core 用测试钉死；Unity 侧只负责照单重建。

**Files:**
- Create: `Assets/Scripts/Core/Voxel/DirtySections.cs`
- Create: `Assets/Scripts/Core/Voxel/SectionRef.cs`
- Create: `Assets/Tests/EditMode/Voxel/DirtySectionsTests.cs`

- [ ] **Step 1: 先写失败的测试**

```csharp
using System.Collections.Generic;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    /// <summary>脏段收集：段内、上下边界、区块水平边界、以及角上的组合情况。</summary>
    [TestFixture]
    public class DirtySectionsTests
    {
        private readonly List<SectionRef> _result = new List<SectionRef>();

        private List<SectionRef> Collect(int x, int y, int z)
        {
            DirtySections.Collect(x, y, z, _result);
            return _result;
        }

        [Test]
        public void Collect_InsideSection_ReturnsOnlyItsOwnSection()
        {
            // (8, 72, 8) 在区块 (0,0)、段 8（y ∈ [64, 80)）的正中间
            Assert.That(Collect(8, 72, 8), Is.EqualTo(new[]
            {
                new SectionRef(new ChunkPos(0, 0), 8)
            }), "段内部的方块只牵连它自己所在的段");
        }

        [Test]
        public void Collect_AtSectionBottom_AlsoMarksSectionBelow()
        {
            List<SectionRef> dirty = Collect(8, 64, 8);

            Assert.That(dirty, Has.Count.EqualTo(2));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)),
                "贴着段底面的方块会改变下面那段顶面的可见性");
        }

        [Test]
        public void Collect_AtChunkEdge_AlsoMarksNeighbourChunk()
        {
            List<SectionRef> dirty = Collect(0, 72, 8);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)),
                "贴着区块西边界的方块会改变西侧邻区块东面的可见性");
        }

        [Test]
        public void Collect_AtChunkCorner_MarksAllTouchedNeighbours()
        {
            // (0, 64, 0)：西、北、下三个方向同时贴边
            List<SectionRef> dirty = Collect(0, 64, 0);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 7)));
        }

        [Test]
        public void Collect_NeverReturnsDuplicates()
        {
            Assert.That(Collect(15, 79, 15), Is.Unique, "同一个段不应当出现两次，否则会被重建两遍");
        }

        [Test]
        public void Collect_NeverReturnsSectionOutsideWorld()
        {
            foreach (SectionRef section in Collect(0, VoxelCoords.MinY, 0))
            {
                Assert.That(section.SectionIndex, Is.InRange(0, VoxelCoords.SectionCount - 1),
                    "世界最底层的下方没有段，不应当被收进来");
            }
        }

        [Test]
        public void Collect_ClearsPreviousResult()
        {
            Collect(8, 72, 8);
            List<SectionRef> second = Collect(100, 200, 100);

            Assert.That(second, Has.Count.EqualTo(1),
                "Collect 应当先清空传入的列表，否则连续调用会越积越多");
        }
    }
}
```

- [ ] **Step 2: `SectionRef`**

```csharp
using System;

namespace MyWorld.Core.Voxel
{
    /// <summary>一个区块段的定位：哪根区块列的第几段。</summary>
    public readonly struct SectionRef : IEquatable<SectionRef>
    {
        public readonly ChunkPos Chunk;
        public readonly int SectionIndex;

        public SectionRef(ChunkPos chunk, int sectionIndex)
        {
            Chunk = chunk;
            SectionIndex = sectionIndex;
        }

        public bool Equals(SectionRef other) => Chunk.Equals(other.Chunk) && SectionIndex == other.SectionIndex;

        public override bool Equals(object obj) => obj is SectionRef other && Equals(other);

        public override int GetHashCode() => (Chunk.GetHashCode() * 397) ^ SectionIndex;

        public override string ToString() => $"{Chunk} 段 {SectionIndex}";
    }
}
```

- [ ] **Step 3: `DirtySections`**

```csharp
using System.Collections.Generic;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 改一个方块之后，哪些区块段需要重建网格。
    /// <para>
    /// 不是只有它自己那一段：贪心网格生成时会采样**邻居**来剔除接缝面，所以贴着段边界的
    /// 方块会改变相邻段的面可见性。最多牵连 8 个段（水平 4 个 × 竖直 2 个），
    /// 绝大多数情况只有 1 个——比无脑重建 3×3×3 = 27 个段省得多，连续挖掘时差别明显。
    /// </para>
    /// </summary>
    public static class DirtySections
    {
        /// <summary>结果写进 <paramref name="output"/>（会先清空），避免每次挖掘都分配一个新列表。</summary>
        public static void Collect(int worldX, int worldY, int worldZ, List<SectionRef> output)
        {
            output.Clear();

            if (worldY < VoxelCoords.MinY || worldY >= VoxelCoords.MaxY)
            {
                return;
            }

            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);
            int section = VoxelCoords.WorldToSection(worldY);
            int localY = VoxelCoords.WorldToLocal(worldY);

            // 水平：自己这根，加上贴边时的邻居（最多 3 根，角上是西/北两根 + 自己）
            AddColumn(output, chunkX, chunkZ, section, localY);

            if (localX == 0) AddColumn(output, chunkX - 1, chunkZ, section, localY);
            if (localX == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX + 1, chunkZ, section, localY);
            if (localZ == 0) AddColumn(output, chunkX, chunkZ - 1, section, localY);
            if (localZ == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX, chunkZ + 1, section, localY);
        }

        /// <summary>某根区块列上，本段以及（贴着段上下边界时）相邻的那一段。</summary>
        private static void AddColumn(List<SectionRef> output, int chunkX, int chunkZ, int section, int localY)
        {
            var chunk = new ChunkPos(chunkX, chunkZ);
            output.Add(new SectionRef(chunk, section));

            if (localY == 0 && section > 0)
            {
                output.Add(new SectionRef(chunk, section - 1));
            }
            else if (localY == VoxelCoords.ChunkSize - 1 && section < VoxelCoords.SectionCount - 1)
            {
                output.Add(new SectionRef(chunk, section + 1));
            }
        }
    }
}
```

**注意**：`VoxelCoords.WorldToSection` 与 `WorldToLocal(int worldY)` 这两个方法在写实现前
先确认存在——`VoxelCoords` 里已有的是 `WorldToChunk` / `WorldToLocal(水平)`。
若竖直方向的换算方法名不同，按 `VoxelCoords` 里实际的名字调用，**不要新写一套坐标换算**
（CLAUDE.md：坐标换算只走 `VoxelCoords`）。

- [ ] **Step 4: 跑测试确认变绿，然后提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~DirtySectionsTests"
git add Assets/Scripts/Core/Voxel Assets/Tests/EditMode/Voxel
git commit -m "玩家层: 方块改动后的脏区块段收集"
```

---

## Task 6: 区块段视图的索引与按需重建

里程碑 1 的 `WorldBootstrap` 是"建完就撒手"——`ChunkSectionView` 建出来之后没人再管它。
挖方块要重建、走远了要销毁，都需要一张 (区块, 段) → 视图 的索引。

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs`

Unity 层没有 `dotnet test` 覆盖（`tools/dotnet` 只链接 `Core` 与 `Tests/EditMode`），
所以这个类的验证靠 Task 11 的 `PlayHarness`。写的时候把逻辑压到最薄，
复杂的判断都已经在 Core 里测过了。

- [ ] **Step 1: 实现**

```csharp
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// (区块, 段) → <see cref="ChunkSectionView"/> 的索引。
    /// <para>
    /// 视图的生命周期只有三件事：需要时建出来、脏了重建、走远了销毁。
    /// 空段（一个面都没有）不保留 GameObject——13×13 区块 × 24 段有 4000 多个，
    /// 其中绝大多数是纯空气或纯石头，留着白占场景层级。
    /// </para>
    /// </summary>
    public sealed class ChunkViewRegistry
    {
        private readonly Dictionary<SectionRef, ChunkSectionView> _views =
            new Dictionary<SectionRef, ChunkSectionView>();

        private readonly Transform _parent;
        private readonly World _world;
        private readonly BlockRegistry _registry;
        private readonly BlockMaterialLibrary _materials;

        private readonly List<SectionRef> _dirtyScratch = new List<SectionRef>();

        public ChunkViewRegistry(Transform parent, World world, BlockRegistry registry,
            BlockMaterialLibrary materials)
        {
            _parent = parent;
            _world = world;
            _registry = registry;
            _materials = materials;
        }

        public int ViewCount => _views.Count;

        /// <summary>建好一根区块列上所有非空段的网格。返回真正出了面的段数。</summary>
        public int BuildColumn(ChunkPos chunk)
        {
            if (!_world.TryGetChunk(chunk, out ChunkColumn column))
            {
                return 0;
            }

            var built = 0;
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                if (column.HasSection(section) && Rebuild(new SectionRef(chunk, section)))
                {
                    built++;
                }
            }

            return built;
        }

        /// <summary>改了一个方块之后调用，牵连到的段一并重建。</summary>
        public void MarkBlockChanged(int worldX, int worldY, int worldZ)
        {
            DirtySections.Collect(worldX, worldY, worldZ, _dirtyScratch);
            foreach (SectionRef section in _dirtyScratch)
            {
                Rebuild(section);
            }
        }

        /// <summary>重建一个段。返回它是否还有可见面——没有面的段会被销毁。</summary>
        public bool Rebuild(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                view = ChunkSectionView.Create(_parent, section.Chunk, section.SectionIndex);
                _views[section] = view;
            }

            if (view.Rebuild(_world, _registry, _materials))
            {
                return true;
            }

            // 段被挖空（或本来就被完全包裹）：留着只是白占一个 GameObject
            Destroy(section);
            return false;
        }

        /// <summary>卸载整根区块列的视图。</summary>
        public void UnloadColumn(ChunkPos chunk)
        {
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                Destroy(new SectionRef(chunk, section));
            }
        }

        private void Destroy(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                return;
            }

            _views.Remove(section);
            if (view != null)
            {
                DestroyObject(view.gameObject);
            }
        }

        /// <summary>编辑器非播放态下 Destroy 不生效，必须走 DestroyImmediate。</summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
```

- [ ] **Step 2: 编译确认**

```bash
env ... "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit -logFile compile.log
grep -c "error CS" compile.log
```

（完整的 `env` 前缀见 CLAUDE.md。）预期：0。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Unity/Rendering
git commit -m "玩家层: 区块段视图索引与按需重建"
```

---

## Task 7: 玩家控制器

**Files:**
- Create: `Assets/Scripts/Unity/Player/PlayerController.cs`

- [ ] **Step 1: 实现**

关键点写在注释里，尤其是"输入必须旋转到世界空间"和"相机是子物体不参与碰撞"。

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 输入 → <see cref="PlayerMotor"/> → transform。
    /// <para>
    /// 这个组件刻意很薄：运动解算全在 Core 里（可 `dotnet test` 覆盖），这里只做三件事——
    /// 读输入、把输入按相机朝向旋转到世界空间、把结果写回 transform。
    /// </para>
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private float lookSensitivity = 2.5f;
        [SerializeField] private Float3Inspector spawn = default;

        [Header("运动参数（留空用 Core 的默认值）")]
        [SerializeField] private float walkSpeed = 4.3f;
        [SerializeField] private float jumpSpeed = 8.4f;
        [SerializeField] private float gravity = -28f;

        private PlayerMotorSettings _settings;
        private PlayerState _state;
        private World _world;
        private BlockRegistry _registry;

        private float _yaw;
        private float _pitch;

        public PlayerState State => _state;
        public PlayerMotorSettings Settings => _settings;

        /// <summary>相机所在位置——射线拾取要用，所以公开出去。</summary>
        public Transform Eye => eye;

        /// <summary>由 <c>WorldBootstrap</c> 在世界准备好之后调用。</summary>
        public void Bind(World world, BlockRegistry registry, Float3 spawnPosition)
        {
            _world = world;
            _registry = registry;
            _settings = new PlayerMotorSettings
            {
                WalkSpeed = walkSpeed,
                JumpSpeed = jumpSpeed,
                Gravity = gravity
            };

            _state = PlayerState.AtRest(spawnPosition);
            ApplyToTransform();
        }

        /// <summary>驱动一帧。公开出来是为了让无头验证能手动步进，不必真的进 Play 模式。</summary>
        public void Tick(PlayerInput input, float dt)
        {
            if (_world == null)
            {
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            _state = PlayerMotor.Step(source, _state, input, _settings, dt);
            ApplyToTransform();
        }

        private void Update()
        {
            UpdateLook();
            Tick(ReadInput(), Time.deltaTime);
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // 未锁定指针时不转视角，否则在编辑器里点 UI 会把视角甩飞
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);

            // 身体只转 yaw，俯仰只给眼睛——身体跟着俯仰转的话包围盒会倾斜
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (eye != null)
            {
                eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private PlayerInput ReadInput()
        {
            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            // Core 不知道相机朝哪，旋转在这里做完再传进去
            Vector3 direction = transform.right * right + transform.forward * forward;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return new PlayerInput(direction.x, direction.z,
                Input.GetKey(KeyCode.Space), Input.GetKey(KeyCode.LeftShift));
        }

        private void ApplyToTransform()
        {
            transform.position = new Vector3(_state.Position.X, _state.Position.Y, _state.Position.Z);
            if (eye != null)
            {
                eye.localPosition = new Vector3(0f, _settings.EyeHeight, 0f);
            }
        }
    }
}
```

**`Float3Inspector` 是个坑**：`Float3` 是 Core 的类型，Unity 能不能序列化它取决于它有没有
`[Serializable]` 和公开字段。**实现时先检查 `Core/Math/Float3.cs`**——如果它不是
`[Serializable]`（Core 不能引用 UnityEngine，但 `System.SerializableAttribute` 是可以用的），
就把出生点字段改成 `Vector3`，在 `Bind` 里转换。不要为了序列化去动 Core 的类型。

- [ ] **Step 2: 编译确认，然后提交**

```bash
git add Assets/Scripts/Unity/Player
git commit -m "玩家层: 玩家控制器"
```

---
