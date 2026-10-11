using System.Collections.Generic;
using TapTapGameJam.Coffee;
using TapTapGameJam.Interactables;
using TapTapGameJam.MiniGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TapTapGameJam.UI
{
    // 空面板的默认布局：编辑器菜单可将其保存；运行时只补齐尚未配置的内容。
    // 已配置的图片、尺寸和引用不会被重建。
    public static class MiniGameDefaultSetup
    {
        private static readonly Color Ink = new Color(0.10f, 0.17f, 0.24f);
        private static readonly Color Accent = new Color(0.12f, 0.48f, 0.55f);
        private static readonly Color Paper = new Color(0.93f, 0.95f, 0.94f);

        public static void Ensure(MiniGamePanelUI panel)
        {
            RectTransform parent = null;
            foreach (var rect in panel.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "ContentRoot") { parent = rect; break; }
            if (parent == null)
            {
                var oldClean = panel.GetComponentInChildren<global::CleanGame>(true);
                parent = oldClean != null ? oldClean.transform.parent as RectTransform : panel.transform as RectTransform;
            }
            if (parent == null) return;
            var sample = panel.GetComponentInChildren<TMP_Text>(true);
            TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
            SetupClean(panel, parent, font);
            SetupStock(panel, parent, font);
            SetupCoffee(panel, parent, font);
            SetupPrinter(panel, parent, font);
            SetupPoints(panel);
        }

        private static RectTransform Root(RectTransform parent, string name)
        {
            var root = Rect(parent, name, new Vector2(1100, 600), Vector2.zero);
            root.gameObject.SetActive(false);
            if (root.sizeDelta == new Vector2(100, 100)) root.sizeDelta = new Vector2(1100, 600);
            Image background = root.GetComponent<Image>();
            if (background == null) { background = root.gameObject.AddComponent<Image>(); background.color = Paper; }
            background.raycastTarget = true;
            return root;
        }

        private static void SetupClean(MiniGamePanelUI panel, RectTransform parent, TMP_FontAsset font)
        {
            var root = Root(parent, "CleanGame");
            var game = root.GetComponent<global::CleanGame>() ?? root.gameObject.AddComponent<global::CleanGame>();
            if (!game.IsConfigured)
            {
                Label(root, "Title", "清理垃圾", new Vector2(800, 50), new Vector2(0, 252), font, 32);
                Label(root, "Instruction", "按住鼠标左键滑动 · 擦掉 90% 污渍即可完成", new Vector2(900, 40), new Vector2(0, 205), font, 22);
                var surface = Rect(root, "CleaningSurface", new Vector2(800, 450), new Vector2(0, -40));
                var surfaceImage = surface.GetComponent<Image>() ?? surface.gameObject.AddComponent<Image>();
                surfaceImage.color = new Color(0.80f, 0.85f, 0.78f);
                surfaceImage.raycastTarget = false;
                var stainRect = Rect(surface, "Stain", new Vector2(800, 450), Vector2.zero);
                var stain = stainRect.GetComponent<RawImage>() ?? stainRect.gameObject.AddComponent<RawImage>();
                stain.color = Color.white;
                stain.raycastTarget = true;
                var status = Label(root, "ProgressText", "已清理 0% · 目标 90%", new Vector2(450, 32), new Vector2(0, -274), font, 21);
                var progress = Progress(root, "Progress", new Vector2(160, 12), new Vector2(420, -274));
                game.Configure(stain, status, progress);
            }
            panel.RegisterContent(TapTapGameJam.Types.MiniGameKind.Clean, game);
        }

        private static void SetupStock(MiniGamePanelUI panel, RectTransform parent, TMP_FontAsset font)
        {
            var root = Root(parent, "TradeStocks");
            var game = root.GetComponent<StockPointGame>() ?? root.gameObject.AddComponent<StockPointGame>();
            root.sizeDelta = new Vector2(1100, 600);

            var frame = Rect(root, "StockFrame", new Vector2(1080, 580), Vector2.zero);
            frame.SetAsFirstSibling();
            ConfigureStockPanel(frame, new Color(0.82f, 0.84f, 0.83f), new Color(0.05f, 0.05f, 0.05f), 28f, 6f);

            var title = Label(root, "Title", "股市行情", new Vector2(700, 54), new Vector2(-165, 248), font, 32);
            title.alignment = TextAlignmentOptions.Left;
            title.color = new Color(0.08f, 0.08f, 0.08f);

            var chartPanel = Rect(root, "ChartPanel", new Vector2(760, 405), new Vector2(-115, -5));
            ConfigureStockPanel(chartPanel, new Color(0.43f, 0.43f, 0.43f), new Color(0.28f, 0.28f, 0.28f), 25f, 0f);
            var chartRect = FindChild(chartPanel, "PointLineChart") as RectTransform
                ?? FindChild(root, "PointLineChart") as RectTransform
                ?? Rect(chartPanel, "PointLineChart", new Vector2(720, 365), Vector2.zero);
            if (chartRect.parent != chartPanel) chartRect.SetParent(chartPanel, false);
            chartRect.anchorMin = chartRect.anchorMax = chartRect.pivot = new Vector2(0.5f, 0.5f);
            chartRect.sizeDelta = new Vector2(720, 365);
            chartRect.anchoredPosition = Vector2.zero;
            var chart = chartRect.GetComponent<StockPointChart>() ?? chartRect.gameObject.AddComponent<StockPointChart>();
            chart.enabled = true;
            chart.ConfigureTheme(Color.white, Color.white, new Color(1f, 1f, 1f, 0.08f));
            chart.raycastTarget = false;
            if (chartRect.GetComponent<RectMask2D>() == null) chartRect.gameObject.AddComponent<RectMask2D>();
            chartRect.gameObject.SetActive(true);

            var side = Rect(root, "TradeSidePanel", new Vector2(245, 405), new Vector2(375, -5));
            ConfigureStockPanel(side, new Color(0.67f, 0.68f, 0.67f), Color.clear, 0f, 0f);
            var timerRect = Rect(side, "TimerIcon", new Vector2(120, 145), new Vector2(0, 130));
            foreach (var legacyText in timerRect.GetComponentsInChildren<TMP_Text>(true))
                legacyText.enabled = false;
            var hourglass = timerRect.GetComponent<StockHourglass>() ?? timerRect.gameObject.AddComponent<StockHourglass>();
            var hourglassImage = timerRect.GetComponent<Image>() ?? timerRect.gameObject.AddComponent<Image>();
            hourglassImage.color = Color.white;
            hourglassImage.raycastTarget = false;
#if UNITY_EDITOR
            hourglass.Configure(AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Model/MiniGame/MiniGameConponent/小游戏组件/炒股-随机曲线/沙漏.png"));
#endif

            var fundsPanel = Rect(side, "FundsPanel", new Vector2(220, 76), new Vector2(0, 30));
            ConfigureStockPanel(fundsPanel, new Color(0.12f, 0.12f, 0.12f), Color.clear, 0f, 0f);
            var status = Label(fundsPanel, "Status", "当前资金\n100", new Vector2(210, 70), Vector2.zero, font, 20);
            status.color = Color.white;
            status.alignment = TextAlignmentOptions.Center;

            var result = Label(side, "Result", "剩余 20.0 秒\n持仓 0 股", new Vector2(220, 65), new Vector2(0, -60), font, 17);
            result.color = new Color(0.08f, 0.08f, 0.08f);
            result.alignment = TextAlignmentOptions.Center;

            var buy = Button(side, "BuyOne", "买进", new Vector2(-57, -155), font);
            var sell = Button(side, "SellAll", "卖出", new Vector2(57, -155), font);
            SetButtonStyle(buy, font);
            SetButtonStyle(sell, font);
            game.Configure(chart, status, result, buy, sell, hourglass);
            game.enabled = true;
            panel.RegisterContent(TapTapGameJam.Types.MiniGameKind.StockTrade, game);
            // 兼容已经保存过旧布局的场景，不重建用户的面板。
            foreach (string obsolete in new[]
            {
                "UpperPrice", "LowerPrice", "SellOne", "BuyAll", "Wait",
                "Status", "Result", "BuyOne", "SellAll"
            })
            {
                var child = root.Find(obsolete);
                if (child != null) child.gameObject.SetActive(false);
            }
        }

        public static void EnsureStock(MiniGamePanelUI panel, TMP_FontAsset font)
        {
            var parent = FindChild(panel.transform, "ContentRoot") as RectTransform;
            if (parent == null) parent = panel.transform as RectTransform;
            SetupStock(panel, parent, font);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void SetupCoffee(MiniGamePanelUI panel, RectTransform parent, TMP_FontAsset font)
        {
            var root = Root(parent, "MakeCoffeeGame");
            var game = root.GetComponent<CoffeeGame>() ?? root.gameObject.AddComponent<CoffeeGame>();
            bool threeStepLayout = root.Find("ThreeStepGrinder") != null;
            if (!threeStepLayout)
            {
                // 旧八步面板可能仍保存在场景中；禁用整套旧布局后生成新的三步布局。
                foreach (string legacy in new[] { "Table", "Steps", "Instruction", "Progress" })
                {
                    var old = root.Find(legacy);
                    if (old != null) old.gameObject.SetActive(false);
                }
                Label(root, "Title", "制作咖啡", new Vector2(900, 50), new Vector2(0, 250), font, 32);
                var steps = Label(root, "ThreeStepSteps", "制作步骤", new Vector2(310, 250), new Vector2(-390, 0), font, 21);
                steps.alignment = TextAlignmentOptions.TopLeft;
                var table = Rect(root, "ThreeStepTable", new Vector2(760, 400), new Vector2(150, 0));
                var tableImage = table.GetComponent<Image>() ?? table.gameObject.AddComponent<Image>();
                tableImage.color = new Color(0.86f, 0.89f, 0.86f);
                tableImage.raycastTarget = false;
                var grinder = Icon(table, "ThreeStepGrinder", "研磨机\n（长按）", new Vector2(-250, 85), new Vector2(180, 110), new Color(0.51f, 0.59f, 0.58f), font);
                var grind = grinder;
                var grounds = Icon(table, "ThreeStepGrounds", "粉杯\n已扣在研磨机下", new Vector2(-250, -75), new Vector2(180, 80), new Color(0.42f, 0.30f, 0.21f), font);
                var cupZone = Icon(table, "ThreeStepCup", "中央咖啡杯\n粉杯放到这里", new Vector2(0, -10), new Vector2(210, 150), new Color(0.51f, 0.59f, 0.58f), font);
                var kettle = Icon(table, "ThreeStepKettle", "热水壶\n拖到杯子上方", new Vector2(250, 85), new Vector2(180, 110), new Color(0.28f, 0.43f, 0.61f), font);
                var pourZone = Rect(table, "ThreeStepPourZone", new Vector2(230, 180), new Vector2(0, -5));
                var water = Icon(table, "ThreeStepWaterEffect", "注水中…", new Vector2(0, 75), new Vector2(140, 28), new Color(0.28f, 0.58f, 0.76f), font);
                grinder.GetComponent<Image>().raycastTarget = true;
                cupZone.GetComponent<Image>().raycastTarget = false;
                water.GetComponent<Image>().raycastTarget = false;
                water.gameObject.SetActive(false);
                Label(root, "ThreeStepInstruction", "按住左侧研磨机 → 拖粉杯到中央 → 按住右侧热水壶在杯子上方浇水 2 秒", new Vector2(1050, 40), new Vector2(0, -235), font, 20);
                var progress = Progress(root, "ThreeStepProgress", new Vector2(1000, 14), new Vector2(0, -275));
                game.Configure(grind, grounds, kettle, cupZone, pourZone, water.gameObject, steps, progress);
            }
            panel.RegisterContent(TapTapGameJam.Types.MiniGameKind.MakeCoffee, game);
        }

        // 编辑器可单独补齐打印机，不改动已配置的其他小游戏。
        public static void EnsurePrinter(MiniGamePanelUI panel, TMP_FontAsset font)
        {
            RectTransform parent = null;
            foreach (var rect in panel.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "ContentRoot") { parent = rect; break; }
            if (parent == null) parent = panel.transform as RectTransform;
            SetupPrinter(panel, parent, font);
            RegisterPrinterPoints(panel);
        }

        private static void SetupPrinter(MiniGamePanelUI panel, RectTransform parent, TMP_FontAsset font)
        {
            var root = Root(parent, "PrintFileGame");
            var game = root.GetComponent<PrinterRepairGame>() ?? root.gameObject.AddComponent<PrinterRepairGame>();
            if (!game.IsConfigured)
            {
                root.sizeDelta = new Vector2(1100, 600);
                Label(root, "Title", "敲打修复打印机", new Vector2(900, 50), new Vector2(0, 250), font, 32);
                Label(root, "Instruction", "打印机卡住了！指针进入黄色区域时，按下空格敲打", new Vector2(1000, 45), new Vector2(0, 195), font, 22);
                var printer = Icon(root, "PrinterBody", "打印机", new Vector2(0, 55), new Vector2(320, 150), new Color(0.32f, 0.39f, 0.43f), font);
                printer.GetComponentInChildren<TMP_Text>().rectTransform.anchoredPosition = new Vector2(0, 22);
                var paper = Icon(printer, "Paper", "待打印文件", new Vector2(0, -42), new Vector2(130, 80), Color.white, font);
                paper.GetComponentInChildren<TMP_Text>().color = Ink;
                var slot = Icon(printer, "Slot", "", new Vector2(0, -4), new Vector2(250, 16), Ink, font);
                slot.SetAsLastSibling();
                var light = Icon(printer, "FaultLight", "", new Vector2(130, 48), new Vector2(16, 16), new Color(0.9f, 0.25f, 0.17f), font);
                var bar = Icon(root, "TimingBar", "", new Vector2(0, -125), new Vector2(900, 44), new Color(0.07f, 0.07f, 0.08f), font);
                var zone = Icon(bar, "SuccessZone", "", Vector2.zero, new Vector2(225, 44), new Color(1f, 0.78f, 0.05f), font);
                zone.anchorMin = new Vector2(0.4f, 0); zone.anchorMax = new Vector2(0.65f, 1);
                zone.offsetMin = zone.offsetMax = Vector2.zero;
                var pointer = Icon(bar, "Pointer", "", Vector2.zero, new Vector2(8, 68), Color.white, font);
                pointer.anchorMin = pointer.anchorMax = new Vector2(0, 0.5f);
                var feedback = Label(root, "Feedback", "等待指针进入黄色区域，再按空格", new Vector2(1000, 40), new Vector2(0, -197), font, 22);
                var count = Label(root, "ProgressText", "修复进度：0 / 5", new Vector2(600, 40), new Vector2(0, -240), font, 24);
                var progress = Progress(root, "Progress", new Vector2(900, 12), new Vector2(0, -276));
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.gameObject != root.gameObject) graphic.raycastTarget = false;
                game.Configure(bar, zone, pointer, printer, count, feedback, progress);
            }
            panel.RegisterContent(TapTapGameJam.Types.MiniGameKind.PrinterRepair, game);
        }

        private static void RegisterPrinterPoints(MiniGamePanelUI panel)
        {
            var managers = Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var points = new List<PrinterRepairPoint>();
            foreach (var point in Object.FindObjectsByType<PrinterRepairPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == panel.gameObject.scene)
                {
                    point.ConfigureGameKind(TapTapGameJam.Types.MiniGameKind.PrinterRepair);
                    points.Add(point);
                }
            if (points.Count == 0) return;
            var quest = FindQuest<PrinterRepairQuest>(panel);
            if (quest == null) quest = points[0].gameObject.AddComponent<PrinterRepairQuest>();
            quest.ConfigurePoints(points);
            Register(managers, quest);
        }

        private static void SetupPoints(MiniGamePanelUI panel)
        {
            var scene = panel.gameObject.scene;
            RegisterPrinterPoints(panel);
            var managers = Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var cleanPoints = new List<CleanPoint>();
            foreach (var point in Object.FindObjectsByType<CleanPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameKind(TapTapGameJam.Types.MiniGameKind.Clean); cleanPoints.Add(point); }
            var cleanQuest = FindQuest<CleanQuest>(panel);
            if (cleanPoints.Count > 0)
            {
                if (cleanQuest == null) cleanQuest = cleanPoints[0].gameObject.AddComponent<CleanQuest>();
                cleanQuest.ConfigurePoints(cleanPoints);
                Register(managers, cleanQuest);
            }

            var coffeePoints = new List<MakeCoffeePoint>();
            foreach (var point in Object.FindObjectsByType<MakeCoffeePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameKind(TapTapGameJam.Types.MiniGameKind.MakeCoffee); coffeePoints.Add(point); }
            MakeCoffeePoint coffee = coffeePoints.Count > 0 ? coffeePoints[0] : null;
            var coffeeQuest = FindQuest<MakeCoffeeQuest>(panel);
            if (coffeePoints.Count > 0)
            {
                if (coffeeQuest == null) coffeeQuest = coffeePoints[0].gameObject.AddComponent<MakeCoffeeQuest>();
                coffeeQuest.ConfigurePoints(coffeePoints);
                Register(managers, coffeeQuest);
            }
            var existingStockPoints = Object.FindObjectsByType<StockPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasStock = false;
            foreach (var point in existingStockPoints) if (point.gameObject.scene == scene) hasStock = true;
            if (!hasStock && coffee != null)
            {
                var go = new GameObject("StockPoint");
                go.transform.SetParent(coffee.transform.parent, false);
                go.transform.position = coffee.transform.position + Vector3.right * 3f;
                var collider = go.AddComponent<CapsuleCollider>();
                collider.isTrigger = true; collider.radius = 1.5f; collider.height = 3f;
                go.AddComponent<StockPoint>();
                var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                terminal.name = "StockTerminalPlaceholder";
                terminal.transform.SetParent(go.transform, false);
                terminal.transform.localPosition = Vector3.up * 0.6f;
                terminal.transform.localScale = new Vector3(0.8f, 1.2f, 0.5f);
                var solid = terminal.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(solid); else Object.DestroyImmediate(solid);
            }
            var stockPoints = new List<StockPoint>();
            foreach (var point in Object.FindObjectsByType<StockPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameKind(TapTapGameJam.Types.MiniGameKind.StockTrade); stockPoints.Add(point); }
            var stockQuest = FindQuest<StockQuest>(panel);
            if (stockPoints.Count > 0)
            {
                if (stockQuest == null) stockQuest = stockPoints[0].gameObject.AddComponent<StockQuest>();
                stockQuest.ConfigurePoints(stockPoints);
                Register(managers, stockQuest);
            }
        }

        private static TQuest FindQuest<TQuest>(MiniGamePanelUI panel)
            where TQuest : MiniGameCandidate
        {
            foreach (var quest in Object.FindObjectsByType<TQuest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (quest.gameObject.scene == panel.gameObject.scene)
                    return quest;
            return null;
        }
        private static void Register(MiniGameManager[] managers, MiniGameCandidate quest)
        {
            foreach (var manager in managers) if (manager.gameObject.scene == quest.gameObject.scene) manager.RegisterCandidate(quest);
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        private static TMP_Text Label(RectTransform parent, string name, string value, Vector2 size, Vector2 position, TMP_FontAsset font, float fontSize)
        {
            var rect = Rect(parent, name, size, position);
            var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = fontSize; text.color = Ink;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
        private static RectTransform Icon(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Color color, TMP_FontAsset font)
        {
            var rect = Rect(parent, name, size, position);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color;
            var text = Label(rect, "Label", label, size - new Vector2(8, 4), Vector2.zero, font, 20);
            text.color = Color.white;
            return rect;
        }
        private static Button Button(RectTransform parent, string name, string label, Vector2 position, TMP_FontAsset font)
        {
            var rect = Icon(parent, name, label, position, new Vector2(180, 48), Accent, font);
            var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            return button;
        }

        private static void SetButtonStyle(Button button, TMP_FontAsset font)
        {
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(105, 56);
            var image = button.GetComponent<Image>();
            if (image != null) image.color = new Color(0.35f, 0.35f, 0.35f);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.font = font;
                label.fontSize = 24;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        private static StockPanelGraphic ConfigureStockPanel(RectTransform rect, Color fill, Color border,
            float radius, float borderWidth)
        {
            var graphic = rect.GetComponent<StockPanelGraphic>() ?? rect.gameObject.AddComponent<StockPanelGraphic>();
            graphic.Configure(fill, border, radius, borderWidth);
            return graphic;
        }
        private static Slider Progress(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            var rect = Rect(parent, name, size, position);
            var bg = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0.75f, 0.82f, 0.82f); bg.raycastTarget = false;
            var fillRect = Rect(rect, "Fill", size, Vector2.zero);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
            var fill = fillRect.GetComponent<Image>() ?? fillRect.gameObject.AddComponent<Image>();
            fill.color = Accent; fill.raycastTarget = false;
            var slider = rect.GetComponent<Slider>() ?? rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.fillRect = fillRect; slider.interactable = false;
            return slider;
        }
    }
}
