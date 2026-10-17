using System.Collections.Generic;
using System.IO;
using TapTapGameJam.Core;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 圆形小地图：把 <see cref="GridManager.MapState"/> 烘成一张黑白贴图当底图，再把所有
    /// <see cref="MiniMapMarker"/> 按世界位置摆上去。墙白地黑，分辨率 = 网格尺寸（1 像素 = 1 格）。
    ///
    /// ============================ 取景 ============================
    /// 以玩家为圆心的"雷达窗"：
    ///   圆心   = 玩家在地面上的位置（所以玩家永远在小地图正中心）
    ///   北方向 = 相机 forward 投影到地面的方向（小地图上方 = 你正在看的方向）
    ///   比例   = 相机能看到多大一块地面，小地图就显示多大一块
    ///            ppu = 圆半径像素 / 相机可见地面半径
    /// 这三条合起来就是"把相机俯视图转成一个圆"。不想跟相机缩放的话，把
    /// _maxViewRadius/_minViewRadius 设成同一个值即可。
    ///
    /// ============================ 层级约定 ============================
    /// 预制体里是这两层，代码在 Awake 里会强制校正（不校正整套映射就是歪的）：
    ///   Minimap   —— Image(圆形) + Mask，showMaskGraphic 关掉，只负责裁剪
    ///   └ MapRoot —— 每帧绕屏幕法线旋转，所有内容都挂在它下面
    ///      ├ MapImage      RawImage，黑白底图（跟着 MapRoot 一起转）
    ///      ├ PlayerPointer Image，玩家朝向指针（位置固定在圆心，只改旋转）
    ///      └ 运行时创建    每个 MiniMapMarker 一个 Image（每帧反向转 -yaw，始终正面朝上）
    /// 遮罩必须用 <b>Mask</b>（stencil）而不是 RectMask2D：后者按屏幕 AABB 裁剪，内容一转就露角。
    ///
    /// ============================ 坐标系 ============================
    /// MapRoot 的子节点统一用"世界轴向偏移 × ppu"来摆，<b>不</b>自己算旋转：
    ///     localPos = (世界X - 圆心X, 世界Z - 圆心Z) * ppu
    /// 即 local +X ← 世界 +X、local +Y ← 世界 +Z，旋转全部交给 MapRoot。
    /// 设 MapRoot 的 localRotation = Euler(0, 0, +相机yaw)，子节点 (dx, dz) 就会被转到
    ///     (dx·cosyaw - dz·sinyaw,  dx·sinyaw + dz·cosyaw) = (偏移·相机right, 偏移·相机forward)
    /// 正好是"相机前方朝屏幕上方"。推导见 <see cref="LateUpdate"/> 里的注释。
    ///
    /// 只有"底图"和"位置"用这个旋转；图标自己的 localRotation 反向转回 -yaw，
    /// 所以在屏幕上永远是正的，不会被地图带着一起转。
    /// </summary>
    [DisallowMultipleComponent]
    // 排在 CinemachineBrain 之后（brain 在 LateUpdate 里才把相机摆到最终姿态），
    // 保证读到的是这一帧最终的相机朝向，而不会整体晚一帧、转视角时小地图"拖影"。
    [DefaultExecutionOrder(2000)]
    public class MiniMapUI : MonoBehaviour
    {
        public static MiniMapUI Instance { get; private set; }

        #region 场景引用

        [Header("层级（留空自动向下找）")]
        [Tooltip("所有内容挂在这个节点下，每帧绕屏幕法线旋转。会被强制铺满 Minimap 且 pivot 在中心")]
        [SerializeField] private RectTransform _mapRoot;
        [Tooltip("黑白底图，必须是 RawImage（不是 Image），直接吃运行时生成的贴图")]
        [SerializeField] private RawImage _mapImage;
        [Tooltip("玩家朝向指针。挂在 MapRoot 下，位置固定在圆心，代码只改它的旋转。可以不配")]
        [SerializeField] private Image _playerPointer;

        [Header("跟随目标")]
        [Tooltip("小地图圆心 + 指针朝向的来源。留空自动找场景里的 PlayerMovement")]
        [SerializeField] private Transform _player;
        [Tooltip("决定小地图的北方向和缩放。留空取 Camera.main")]
        [SerializeField] private Camera _camera;

        #endregion

        #region 参数

        [Header("缩放")]
        [Tooltip("圆心到可见边缘的半径占 MapRoot 宽度一半的比例。圆形底图四周有留白时调小")]
        [SerializeField, Range(0.3f, 1f)] private float _visibleRadiusRatio = 1f;
        [Tooltip("小地图最多显示多大的地面半径（世界单位）。太大时每格不到 1 像素，会糊成一片")]
        [SerializeField, Min(1f)] private float _maxViewRadius = 30f;
        [Tooltip("小地图最少显示多大的地面半径。相机贴到玩家头顶时防止比例爆掉")]
        [SerializeField, Min(0.5f)] private float _minViewRadius = 3f;

        [Header("图标")]
        [Tooltip("marker 没配 sprite 时用的兜底图标")]
        [SerializeField] private Sprite _defaultIcon;

        [Header("底图配色")]
        [Tooltip("墙的颜色")]
        [SerializeField] private Color _wallColor = Color.white;
        [Tooltip("地面的颜色。想要线稿感就把 alpha 调成 0，只留墙")]
        [SerializeField] private Color _floorColor = Color.black;

        [Header("底图采样")]
        [Tooltip("Point = 一格一个清晰的方块（像素风）；Trilinear = 柔和但偏糊。两者都带 mipmap，缩小时都不会闪")]
        [SerializeField] private FilterMode _mapFilter = FilterMode.Point;

        #endregion

        // ---- 烘焙状态 ----
        private Texture2D _mapTex;
        private int _bakedWallVersion = int.MinValue;

        // ---- 图标：每个 MiniMapMarker 一张 Image，本类创建 / 摆放 / 回收 ----
        private readonly Dictionary<MiniMapMarker, Image> _icons = new Dictionary<MiniMapMarker, Image>();
        private readonly List<MiniMapMarker> _stale = new List<MiniMapMarker>();

        private GridManager Grid => GridManager.Instance;

        #region 生命周期

        private void Awake()
        {
            Instance = this;

            ResolveReferences();

            if (_mapRoot == null || _mapImage == null)
            {
                Debug.LogError("[MiniMapUI] 缺少 MapRoot 或 MapImage（RawImage），小地图已关闭。", this);
                enabled = false;
                return;
            }

            // 整套映射依赖这几个约定，代码顶掉而不是信任 Inspector，免得配歪了出现"整体偏一点点"这种问题
            MakeStretch(_mapRoot);
            MakeCenterAnchored(_mapImage.rectTransform);
            if (_playerPointer != null)
            {
                MakeCenterAnchored(_playerPointer.rectTransform);
                _playerPointer.rectTransform.anchoredPosition = Vector2.zero;
                _playerPointer.raycastTarget = false;
            }

            // uvRect 必须是整张贴图，被 Inspector 改过就会只显示一个角
            _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            _mapImage.raycastTarget = false;

            if (_defaultIcon == null)
            {
                Debug.LogWarning("[MiniMapUI]:没有配置兜底图标，没填 sprite 的 marker 不会显示。", this);
            }
        }

        private void Start()
        {
            // 有些物体的 Awake 比本类晚（比如后生成的玩家），这里再兜一次
            if (_player == null) ResolveReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_mapTex != null)
            {
                Destroy(_mapTex);
            }

            foreach (KeyValuePair<MiniMapMarker, Image> pair in _icons)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _icons.Clear();
        }

        private void ResolveReferences()
        {
            if (_mapRoot == null)
            {
                // 约定：MiniMapUI 挂在 Minimap 节点上，内容容器是它第一个子 RectTransform
                RectTransform self = transform as RectTransform;
                if (self != null && self.childCount > 0)
                {
                    _mapRoot = self.GetChild(0) as RectTransform;
                }
            }

            if (_mapImage == null && _mapRoot != null)
            {
                _mapImage = _mapRoot.GetComponentInChildren<RawImage>(true);
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_player == null)
            {
                PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
                if (playerMovement != null)
                {
                    _player = playerMovement.transform;
                }
            }
        }

        #endregion

        #region 每帧摆放

        private void LateUpdate()
        {
            GridManager grid = Grid;
            if (grid == null || _mapImage == null || _mapRoot == null)
            {
                return;
            }

            // 墙体变了才重烘，平时一帧只比较一个 int
            if (grid.WallVersion != _bakedWallVersion)
            {
                BakeMapTexture(grid);
            }

            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerMovement>()?.transform;
                if (_player == null)
                {
                    return;
                }
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Vector3 playerPos = _player.position;
            Vector2 center = new Vector2(playerPos.x, playerPos.z);

            // ---- 1. 旋转 ----
            // yaw 取相机 forward 在地面的投影方向（0 = 世界 +Z = 小地图的"北"）。
            // 不用 transform.eulerAngles.y：相机能俯到 89°，正好挨着万向锁，欧拉角分解不稳；
            // 而 forward 的 x/z 两个分量都被 cos(pitch) 同比例缩放，Atan2 的比值是稳的。
            Vector3 forward = _camera.transform.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            SetLocalRotation(_mapRoot, Quaternion.Euler(0f, 0f, yaw));

            // ---- 2. 比例 ----
            float ppu = PixelsPerUnit(center);

            // ---- 3. 底图 ----
            // 用"世界轴向偏移 × ppu"摆，方向和旋转都交给 MapRoot
            Vector2 gridCenter = grid.WorldOrigin + grid.WorldSpan * 0.5f;
            RectTransform mapRect = _mapImage.rectTransform;
            SetSizeDelta(mapRect, grid.WorldSpan * ppu);              // 不拉伸：格子始终是正方形
            SetAnchoredPosition(mapRect, (gridCenter - center) * ppu);

            // ---- 4. 图标 ----
            // 位置每帧按世界坐标重算（和相机有没有动无关，蟑螂一直在走）；朝向反向转回正立
            SyncIcons(center, ppu, yaw);

            // ---- 5. 玩家指针 ----
            // 指针表达的是玩家的世界朝向，而不是任何图标的通用规则：地图整体已经被转了 +yaw，
            // 所以指针自己的 localRotation 取 -玩家yaw，合成到屏幕上就正好指示世界朝向。
            if (_playerPointer != null)
            {
                RectTransform pointerRect = _playerPointer.rectTransform;
                SetAnchoredPosition(pointerRect, Vector2.zero); // 圆心 = 玩家位置
                float playerYaw = Mathf.Atan2(_player.forward.x, _player.forward.z) * Mathf.Rad2Deg;
                SetLocalRotation(pointerRect, Quaternion.Euler(0f, 0f, -playerYaw));
            }
        }

        /// <summary>
        /// 像素/世界单位 = 圆半径像素 / 相机可见的地面半径。
        /// 用竖直 FOV，得到的是内接在屏幕里的那个圆，正好配圆形小地图。
        /// </summary>
        private float PixelsPerUnit(Vector2 center)
        {
            float radiusPx = _mapRoot.rect.width * 0.5f * _visibleRadiusRatio;
            if (radiusPx <= 0.5f)
            {
                // 布局还没跑完 / 节点尺寸是 0，先给个不会除零的值
                radiusPx = Mathf.Max(_mapRoot.rect.height, 1f) * 0.5f;
            }

            Vector3 camPos = _camera.transform.position;
            float distance = new Vector3(camPos.x - center.x, camPos.y - _player.position.y, camPos.z - center.y).magnitude;
            float viewRadius = distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            return radiusPx / Mathf.Clamp(viewRadius, _minViewRadius, _maxViewRadius);
        }

        #endregion

        #region 黑白底图烘焙

        /// <summary>
        /// 把 MapState 写成 Texture2D：1 像素 = 1 格，墙白地黑。
        ///
        /// 只有在 <see cref="GridManager.WallVersion"/> 变化时调用，一次几十万像素、毫秒级，
        /// 平时完全不占开销。
        /// </summary>
        private void BakeMapTexture(GridManager grid)
        {
            int w = grid.width;
            int h = grid.height;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (_mapTex != null && (_mapTex.width != w || _mapTex.height != h))
            {
                Destroy(_mapTex);
                _mapTex = null;
            }

            if (_mapTex == null)
            {
                // mipChain = true 是必须的：这张图每帧都在被旋转 + 缩小时重采样，
                // 没有 mip 的话 1 像素宽的墙线会抖成一团摩尔纹。
                // linear = false：这是颜色不是数据，和 GridVisualizer 那张信息素图相反。
                _mapTex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
                {
                    name = "MiniMapBake",
                    filterMode = _mapFilter,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0,
                    hideFlags = HideFlags.DontSave,
                };
            }

            // 采样方式 / 贴图引用每次都顺手校准：改了 Inspector 之后重烘一次就能生效，不用重建贴图
            _mapTex.filterMode = _mapFilter;
            if (_mapImage.texture != _mapTex)
            {
                _mapImage.texture = _mapTex;
            }

            Color32 wall = _wallColor;
            Color32 floor = _floorColor;
            var pixels = new Color32[w * h];
            int wallCount = 0;

            // 行序：贴图第 0 行（v = 0）对应格子 y = 0，也就是世界 Z 最小的那条边。
            // 这和 GridManager 的约定（格子 y 沿世界 +Z 增长）一致，也和 UI 里 v = 0 在下方一致，
            // 所以【不需要翻转】——转置或上下翻都会让地图和场景对不上。
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    bool isWall = grid.MapState[x, y] == TileState.TileWall;
                    pixels[row + x] = isWall ? wall : floor;
                    if (isWall)
                    {
                        wallCount++;
                    }
                }
            }

            _mapTex.SetPixels32(pixels);
            _mapTex.Apply(true, false); // 必须 true，否则 mipmap 不会重新生成

            _bakedWallVersion = grid.WallVersion;
            Debug.Log($"[MiniMapUI] 底图烘焙完成：{w}x{h}，墙 {wallCount} 格 / 共 {w * h} 格");
        }

        /// <summary>改了 Inspector 里的配色 / 网格尺寸之后手动重烘一次。</summary>
        [ContextMenu("重新烘焙黑白底图")]
        private void RebakeFromMenu()
        {
            if (Grid != null)
            {
                _bakedWallVersion = int.MinValue;
                BakeMapTexture(Grid);
            }
        }

        /// <summary>调试用：把烘好的黑白图导成 PNG，直接看行序、朝向对不对。写到工程根目录。</summary>
        [ContextMenu("导出黑白图 PNG（调试）")]
        private void ExportMapPng()
        {
            if (Grid == null)
            {
                Debug.LogWarning("[MiniMapUI] 没有 GridManager，导不出底图。");
                return;
            }

            // 编辑器里 MapState 可能还是空的：先按当前墙体重扫一遍再烘
            Grid.TraceMapStateFromWall();
            BakeMapTexture(Grid);

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "minimap.png"));
            File.WriteAllBytes(path, _mapTex.EncodeToPNG());
            Debug.Log($"[MiniMapUI] 已导出黑白图：{path}");
        }

        #endregion

        #region 图标

        /// <summary>
        /// 让 _icons 和 <see cref="MiniMapMarker.All"/> 对齐：
        /// 新出现的 marker 建一张 Image，位置 / 朝向 / 图标 / 颜色每帧同步；注销掉的隐藏或销毁。
        ///
        /// 位置<b>每帧</b>按世界坐标重算 —— 不能只在相机变化时刷，蟑螂一直在走。
        /// 但所有写入都做了"值没变就不写"的判断：RectTransform 和 Graphic 的赋值会把 Canvas 标脏
        /// 触发重建，几十个图标静止时白刷一遍很浪费。
        /// </summary>
        private void SyncIcons(Vector2 center, float ppu, float yaw)
        {
            IReadOnlyList<MiniMapMarker> all = MiniMapMarker.All;

            // 图标要"始终正面朝上"：MapRoot 已经转了 +yaw，子节点自己反向转回来。
            // 位置不需要补偿 —— anchoredPosition 是相对 MapRoot 的局部坐标，父级旋转会自动作用在
            // 位置上，所以局部坐标继续直接用"世界轴向偏移 × ppu"。
            Quaternion upright = Quaternion.Euler(0f, 0f, -yaw);

            for (int i = 0; i < all.Count; i++)
            {
                MiniMapMarker marker = all[i];
                if (marker == null)
                {
                    continue;
                }

                if (!_icons.TryGetValue(marker, out Image icon))
                {
                    icon = CreateIcon(marker);
                    if (icon == null)
                    {
                        continue;
                    }

                    _icons.Add(marker, icon);
                }

                Vector3 worldPos = marker.transform.position;
                RectTransform iconRect = icon.rectTransform;

                // 位置：每帧重算，把世界坐标投到"以玩家为圆心"的小地图坐标上
                SetAnchoredPosition(iconRect, (new Vector2(worldPos.x, worldPos.z) - center) * ppu);

                // 尺寸按 marker 自己的配置
                SetSizeDelta(iconRect, new Vector2(marker.size, marker.size));

                // 朝向：抵消掉地图旋转，图标在屏幕上永远正立
                SetLocalRotation(iconRect, upright);

                Sprite sprite = marker.sprite != null ? marker.sprite : _defaultIcon;
                if (icon.sprite != sprite)
                {
                    icon.sprite = sprite;
                }

                if (icon.color != marker.color)
                {
                    icon.color = marker.color;
                }

                bool visible = sprite != null;
                if (icon.enabled != visible)
                {
                    icon.enabled = visible;
                }
            }

            // 回收。分两种情况，别一律销毁：
            //   · marker 还在、只是被 SetActive(false)（蟑螂回收进池是常态）→ 只隐藏图标，留着复用，
            //     否则每进出一次池就 new/ Destroy 一个 GameObject；
            //   · marker 真被销毁 → Unity 的 == null 判空依然成立，此时才销毁图标。
            _stale.Clear();
            foreach (KeyValuePair<MiniMapMarker, Image> pair in _icons)
            {
                if (pair.Key == null)
                {
                    _stale.Add(pair.Key);
                }
                else if (!pair.Key.isActiveAndEnabled && pair.Value != null)
                {
                    pair.Value.enabled = false;
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                if (_icons.TryGetValue(_stale[i], out Image icon) && icon != null)
                {
                    Destroy(icon.gameObject);
                }

                _icons.Remove(_stale[i]);
            }
        }

        private Image CreateIcon(MiniMapMarker marker)
        {
            var go = new GameObject($"MiniMapIcon_{marker.name}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_mapRoot, false);

            var icon = go.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false; // 第一帧还没配好 sprite，先别显示默认白块

            MakeCenterAnchored(icon.rectTransform);
            icon.rectTransform.sizeDelta = Vector2.one * marker.size;

            // 指针要压在图标上面
            if (_playerPointer != null)
            {
                _playerPointer.transform.SetAsLastSibling();
            }

            return icon;
        }

        #endregion

        #region RectTransform 约定

        // ---- 带变更检查的赋值 ----
        // RectTransform / Graphic 的每次赋值都会把 Canvas 标脏并触发重建，哪怕写进去的值没变。
        // 小地图每帧都在写几十个图标，静止时这些"空写"会白白重建整个 Canvas，所以统一走这几个 helper。

        private static void SetAnchoredPosition(RectTransform rect, Vector2 value)
        {
            if (rect.anchoredPosition != value)
            {
                rect.anchoredPosition = value;
            }
        }

        private static void SetSizeDelta(RectTransform rect, Vector2 value)
        {
            if (rect.sizeDelta != value)
            {
                rect.sizeDelta = value;
            }
        }

        private static void SetLocalRotation(RectTransform rect, Quaternion value)
        {
            if (rect.localRotation != value)
            {
                rect.localRotation = value;
            }
        }

        /// <summary>铺满父节点 + pivot 在中心。MapRoot 必须这样，否则旋转会绕着一个歪的中心转。</summary>
        private static void MakeStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 锚点/pivot 都在中心。底图和图标必须这样：
        /// 代码用 anchoredPosition 表达"世界点偏移"，这只有在 pivot 与锚点都在中心时才对得上。
        /// （sizeDelta 由代码每帧写，这里不碰。）
        /// </summary>
        private static void MakeCenterAnchored(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        #endregion
    }
}
