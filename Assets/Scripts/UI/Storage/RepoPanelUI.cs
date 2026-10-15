using TapTapGameJam.Items;
using TapTapGameJam.Player;
using Unity.AppUI.UI;
using UnityEngine;

namespace TapTapGameJam.UI
{
    public class RepoPanelUI
    {
        private const int GridColumns = 5;
        private const int GridRows = 6;

        /// <summary>单格边长，物品在仓库中不显示格子、只显示sprite、且尺寸自适应缩放</summary>
        private const float CellSize = 150f;

        /// <summary>格与格的间隙</summary>
        private const float CellSpacing = 3f;

        /// <summary>格心间距（block 内相邻格的距离也是它）</summary>
        private const float CellPitch = CellSize + CellSpacing;

        
        [Header("网格 / 物料")]
        [Tooltip("仓库网格根节点，应当是 512x512（100*5 + 3*4），pivot 在左上角 (0,0)。格子由代码生成在这里")]
        [SerializeField] private RectTransform _gridRoot;

        [Tooltip("物品提示框根节点。有东西可显示时才显示")]
        [SerializeField] private RectTransform _itemInfoRoot;

        [Tooltip("物品标记点：手持物停在这里。放在提示框中心")]
        [SerializeField] private RectTransform _itemSpritePivot;

        [Tooltip("拖动中的块临时挂到这里，保证它在最上层。")]
        [SerializeField] private RectTransform _dragLayer;
        
        [SerializeField] private PlayerRepo _repo;
        
        
        // TODO: 和BagPanelUI 一样的生成Grid，注册开关事件
        // 背包开关事件和Repo开关是独立事件

        public void OnRepoEvent(bool isOpen)
        {
            
        }
        
        public void OpenRepo()
        {
            // TODO: 和背包一样打开时为所有已有物品生成BagItemBlock
        }
        
        // 和背包的区别：
        // 每个格子放一个物品，而不是像背包一样一个物品占多个格子。
        // BagItemBlock放入背包时，应该隐藏格子、只显示sprite，且根据占格的BoundingBox，设置它在仓库格子中的位置和大小（要使Boundbox恰好塞满格子，且中心在格子中心）。
        // 注意BagItemBlock的中心并不是BoundBox中心，这里需要算
        // 从背包中取出时


    }
}