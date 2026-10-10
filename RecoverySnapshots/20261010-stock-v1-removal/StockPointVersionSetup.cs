using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StockPointVersionSetup
{
    [MenuItem("Tools/MiniGames/使用炒股点线版")]
    public static void EnablePointVersion()
    {
        Configure(true);
    }

    [MenuItem("Tools/MiniGames/恢复炒股连续版")]
    public static void EnableContinuousVersion()
    {
        Configure(false);
    }

    private static void Configure(bool pointVersion)
    {
        var panels = Object.FindObjectsByType<MiniGamePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length == 0) throw new System.InvalidOperationException("场景中没有 MiniGamePanelUI。");
        var panel = panels[0];
        var root = FindChild(panel.transform, "TradeStocks");
        if (root == null) throw new System.InvalidOperationException("找不到 TradeStocks 小游戏根节点。");
        var chartRect = FindChild(root, "PriceChart");
        var status = FindChild(root, "Status")?.GetComponent<TMPro.TMP_Text>();
        var result = FindChild(root, "Result")?.GetComponent<TMPro.TMP_Text>();
        var buy = FindChild(root, "BuyOne")?.GetComponent<Button>();
        var sell = FindChild(root, "SellAll")?.GetComponent<Button>();
        var continuousGame = root.GetComponent<StockGame>() ?? Undo.AddComponent<StockGame>(root.gameObject);
        var continuousChart = chartRect?.GetComponent<StockChart>();
        var pointGame = root.GetComponent<StockPointGame>();
        var pointChartObject = FindChild(root, "PointLineChart");
        var pointChart = pointChartObject?.GetComponent<StockPointChart>();

        if (chartRect == null || status == null || result == null || buy == null || sell == null || continuousChart == null)
            throw new System.InvalidOperationException("炒股面板缺少必要的图表、文本或按钮引用。");

        if (pointVersion)
        {
            if (pointChart == null)
            {
                pointChartObject = CreatePointChartObject(chartRect);
                pointChart = Undo.AddComponent<StockPointChart>(pointChartObject.gameObject);
            }
            if (pointGame == null) pointGame = Undo.AddComponent<StockPointGame>(root.gameObject);
            pointGame.Configure(pointChart, status, result, buy, sell);
            continuousGame.enabled = false;
            continuousChart.enabled = false;
            continuousChart.gameObject.SetActive(false);
            pointGame.enabled = true;
            pointChart.enabled = true;
            pointChart.gameObject.SetActive(true);
            pointChart.color = continuousChart.color;
            pointChart.raycastTarget = false;
            panel.RegisterContent(3, pointGame);
        }
        else
        {
            if (pointGame != null) pointGame.enabled = false;
            if (pointChart != null)
            {
                pointChart.enabled = false;
                pointChart.gameObject.SetActive(false);
            }
            continuousGame.enabled = true;
            continuousChart.enabled = true;
            continuousChart.gameObject.SetActive(true);
            continuousGame.Configure(continuousChart, status, result, buy, sell);
            panel.RegisterContent(3, continuousGame);
        }

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(root.gameObject);
        if (chartRect != null) EditorUtility.SetDirty(chartRect.gameObject);
        if (panel.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log(pointVersion
            ? "已切换到炒股点线版：开局 1 个点，达到 13 个后横向压缩。请保存场景。"
            : "已恢复炒股连续版。请保存场景。", panel);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static RectTransform CreatePointChartObject(Transform continuousChart)
    {
        var go = new GameObject("PointLineChart", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create point-line chart");
        var rect = (RectTransform)go.transform;
        var source = (RectTransform)continuousChart;
        rect.SetParent(source.parent, false);
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.pivot = source.pivot;
        rect.localScale = source.localScale;
        rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
        return rect;
    }
}
