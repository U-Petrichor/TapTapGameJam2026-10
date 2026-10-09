using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockPointPlotGraphic : MaskableGraphic
    {
        private StockPointChart _chart;

        private StockPointPlotGraphic() => useLegacyMeshGeneration = false;

        public void Bind(StockPointChart chart)
        {
            _chart = chart;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_chart != null) _chart.PopulatePrices(vh, rectTransform.rect);
        }
    }
}
