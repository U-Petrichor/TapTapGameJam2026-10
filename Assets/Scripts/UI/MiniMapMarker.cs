using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 挂在场景里任何一个<b>世界物体</b>上，该物体就会出现在小地图上。
    ///
    /// 【分工】这个类只提供"数据 + 生命周期"，那张图标（Image）由 <see cref="MiniMapUI"/>
    /// 统一创建、摆放、回收。所以不要在 Prefab 上自己挂 Image —— 世界物体上挂 UI 的
    /// Image 是渲染不出来的（它需要 Canvas 里的 RectTransform）。
    /// 图标跟随物体位置（每帧重算），并且<b>始终正面朝上</b>：既不会被地图旋转带着转，
    /// 也不会跟着物体的朝向转。玩家朝向由 MiniMapUI 的指针单独处理。
    ///
    /// 【注册方式】静态表 + OnEnable/OnDisable：
    ///   · 物体被 SetActive(false)（食物被吃空、蟑螂回收进池）时图标自动消失，不用手写 Unregister；
    ///   · 不依赖"MiniMapUI 和 marker 谁先 Awake"，两边没有任何初始化顺序要求；
    ///   · 标记物体自己销毁时 OnDisable 也会被调用。
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniMapMarker : MonoBehaviour
    {
        [Tooltip("在小地图上显示的图标。留空则用 MiniMapUI 上配置的兜底图标")]
        public Sprite sprite;

        [Tooltip("图标颜色")]
        public Color color = Color.white;

        [Tooltip("图标尺寸")]
        public float size = 20.0f;
        
        // ---- 静态注册表 ----
        // 注意这里必须给 Unity 的 Color 字段一个默认值：Color 的默认构造函数是 (0,0,0,0)，
        // 即全透明，Inspector 里不手动设颜色的话一个图标都看不见。

        private static readonly List<MiniMapMarker> _all = new List<MiniMapMarker>();

        /// <summary>当前所有启用的 marker。<see cref="MiniMapUI"/> 每帧遍历它来同步图标。</summary>
        public static IReadOnlyList<MiniMapMarker> All => _all;

        private void OnEnable()
        {
            // Contains 只是防止脚本被重复启用时塞两条，几十个 marker 的开销可忽略
            if (!_all.Contains(this))
            {
                _all.Add(this);
            }
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }
    }
}
