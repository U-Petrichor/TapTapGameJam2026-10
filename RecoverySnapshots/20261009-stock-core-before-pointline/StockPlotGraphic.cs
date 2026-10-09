using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 独立的绘图子层由 RectMask2D 裁剪，超出纵轴的历史价格保持真实坐标。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockPlotGraphic : MaskableGraphic
    {
        private StockChart _chart;
        private StockPlotGraphic() { useLegacyMeshGeneration = false; }
        public void Bind(StockChart chart) { _chart = chart; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_chart != null) _chart.PopulatePrices(vh, rectTransform.rect);
        }
    }
}
