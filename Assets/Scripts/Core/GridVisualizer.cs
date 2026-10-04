using System;
using Unity.Collections;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 把 GridManager 的四路信息素打包成一张 RGBA32 贴图交给 shader 显示。
    ///
    /// 约定：和 GridManager 挂在同一个物体上，那个物体就是网格本身（Plane 或 Quad，
    /// 材质用 Jam/PheromoneOverlay）。世界范围完全由 GridManager 的 WorldOrigin / WorldSpan
    /// 决定，Visualizer 只负责原样转发给 shader，不做任何自己的坐标假设。
    ///
    /// 纹理尺寸 = 网格尺寸，1 个像素 = 1 个格子；格子之间的过渡由 GPU 双线性过滤完成。
    /// 通道顺序：R = PFood，G = PHome，B = PPlayer，A = PDanger。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class GridVisualizer : MonoBehaviour
    {
        [Header("数据源（留空 = 取同物体上的 GridManager）")]
        public GridManager grid;

        [Header("显示开关")]
        public bool showPheromones = true;
        public bool showGridLines = true;

        /// <summary>
        /// 信息素刷新（更新贴图）间隔，单位是 FixedUpdate 的次数。
        /// </summary>
        private const int PheromoneFreshFrameInterval = 10;

        private int _pheromoneFrameCounter;
        
        static readonly int PheroTexId   = Shader.PropertyToID("_PheroTex");
        static readonly int GridOriginId = Shader.PropertyToID("_GridOriginWS");
        static readonly int GridSpanId   = Shader.PropertyToID("_GridSpanWS");
        static readonly int GridSizeId   = Shader.PropertyToID("_GridSize");
        static readonly int ShowPheroId  = Shader.PropertyToID("_ShowPheromones");
        static readonly int ShowGridId   = Shader.PropertyToID("_ShowGridLines");

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        Texture2D _tex;

        void Start()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (grid == null)
            {
                // 约定：GridManager 就在这个物体上
                grid = GetComponent<GridManager>();
            }

            if (grid == null)
            {
                grid = GridManager.Instance; // 兜底
            }

            if (grid == null)
            {
                Debug.LogError("[GridVisualizer] 找不到 GridManager，已关闭。", this);
                enabled = false;
                return;
            }

            EnsureTexture();
        }

        void OnDestroy()
        {
            if (_tex != null)
            {
                Destroy(_tex);
            }
        }

        private void FixedUpdate()
        {

            if (++_pheromoneFrameCounter < PheromoneFreshFrameInterval)
            {
                return;
            }

            _pheromoneFrameCounter = 0;

            grid.EnsureFootprint();
            EnsureTexture();
            PackPheromones();

            _mpb.SetVector(GridOriginId, new Vector4(grid.WorldOrigin.x, grid.WorldOrigin.y, 0f, 0f));
            _mpb.SetVector(GridSpanId, new Vector4(grid.WorldSpan.x, grid.WorldSpan.y, 0f, 0f));
            _mpb.SetVector(GridSizeId, new Vector4(grid.width, grid.height, 0f, 0f));
            _mpb.SetFloat(ShowPheroId, showPheromones ? 1f : 0f);
            _mpb.SetFloat(ShowGridId, showGridLines ? 1f : 0f);
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 网格尺寸变了（运行时改了 width / height）就重建贴图，保证始终 1 像素 = 1 格子。
        /// </summary>
        void EnsureTexture()
        {
            if (_tex != null && _tex.width == grid.width && _tex.height == grid.height)
            {
                return;
            }

            if (_tex != null)
            {
                Destroy(_tex);
            }

            // linear = true：这张贴图装的是数据不是颜色，不能走 sRGB
            _tex = new Texture2D(grid.width, grid.height, TextureFormat.RGBA32, false, true)
            {
                name = "PheromoneField",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave,
            };

            _mpb.SetTexture(PheroTexId, _tex);
        }

        /// <summary>
        /// 四路信息素按这一帧的全局最大值归一化，写进纹理的 RGBA 四个通道。
        /// </summary>
        void PackPheromones()
        {
            int w = grid.width;
            int h = grid.height;
            float[,] food = grid.PFood;
            float[,] home = grid.PHome;
            float[,] player = grid.PPlayer;
            float[,] danger = grid.PDanger;

            // 1) 找四路里的全局最大值，作为这一帧归一化的分母
            float max = 0f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float f = food[x, y];   if (f > max) max = f;
                    float g = home[x, y];   if (g > max) max = g;
                    float p = player[x, y]; if (p > max) max = p;
                    float d = danger[x, y]; if (d > max) max = d;
                }
            }

            float inv = max > 1e-6f ? 1f / max : 0f;

            // 2) 写入。纹理第 y 行 ←→ 格子 y，第 x 列 ←→ 格子 x
            //    （贴图 v=0 那一行就是 WorldZ 最小的那一行，和 WorldOrigin 的定义一致）
            NativeArray<Color32> raw = _tex.GetRawTextureData<Color32>();
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    raw[row + x] = new Color32(
                        ToByte(food[x, y] * inv),
                        ToByte(home[x, y] * inv),
                        ToByte(player[x, y] * inv),
                        ToByte(danger[x, y] * inv));
                }
            }

            _tex.Apply(false, false); // 保留 CPU 副本，下一帧继续写
        }

        static byte ToByte(float v01)
        {
            return (byte)(Mathf.Clamp01(v01) * 255f);
        }
    }
}
