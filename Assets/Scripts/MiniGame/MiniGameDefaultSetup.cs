using System.Collections.Generic;
using TapTapGameJam.Coffee;
using TapTapGameJam.Interactable;
using TapTapGameJam.MiniGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            panel.RegisterContent(0, game);
        }

        private static void SetupStock(MiniGamePanelUI panel, RectTransform parent, TMP_FontAsset font)
        {
            var root = Root(parent, "TradeStocks");
            var game = root.GetComponent<StockPointGame>() ?? root.gameObject.AddComponent<StockPointGame>();
            Label(root, "Title", "模拟炒股", new Vector2(800, 50), new Vector2(0, 250), font, 32);
            var status = Label(root, "Status", "价格 / 金币 / 持仓", new Vector2(1050, 40), new Vector2(0, 200), font, 21);
            var chartRect = FindChild(root, "PointLineChart") ?? Rect(root, "PointLineChart", new Vector2(900, 310), new Vector2(20, 15));
            var chart = chartRect.GetComponent<StockPointChart>() ?? chartRect.gameObject.AddComponent<StockPointChart>();
            chart.enabled = true;
            chart.color = Accent;
            chart.raycastTarget = false;
            if (chartRect.GetComponent<RectMask2D>() == null) chartRect.gameObject.AddComponent<RectMask2D>();
            chartRect.gameObject.SetActive(true);
            var result = Label(root, "Result", "每天 1 次 · 每局 20 秒 · 最后 1.5 秒展示结果", new Vector2(1020, 70), new Vector2(0, -180), font, 19);
            var buy = Button(root, "BuyOne", "买入下一报价", new Vector2(-120, -258), font);
            var sell = Button(root, "SellAll", "卖出下一报价", new Vector2(120, -258), font);
            ((RectTransform)buy.transform).anchoredPosition = new Vector2(-120, -258);
            ((RectTransform)sell.transform).anchoredPosition = new Vector2(120, -258);
            game.Configure(chart, status, result, buy, sell);
            game.enabled = true;
            panel.RegisterContent(3, game);
            // 兼容已经保存过旧布局的场景，不重建用户的面板。
            foreach (string obsolete in new[] { "UpperPrice", "LowerPrice", "SellOne", "BuyAll", "Wait" })
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
            if (!game.IsConfigured)
            {
                Label(root, "Title", "制作咖啡", new Vector2(800, 50), new Vector2(0, 250), font, 32);
                var steps = Label(root, "Steps", "制作步骤", new Vector2(355, 360), new Vector2(-350, -5), font, 21);
                steps.alignment = TextAlignmentOptions.TopLeft;
                var table = Rect(root, "Table", new Vector2(660, 400), new Vector2(205, 0));
                var tableImage = table.GetComponent<Image>() ?? table.gameObject.AddComponent<Image>();
                tableImage.color = new Color(0.86f, 0.89f, 0.86f);
                tableImage.raycastTarget = false;
                var heat = Button(table, "Heat", "烧水开关", new Vector2(-210, -160), font);
                heat.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 42);
                var grind = Icon(table, "Grind", "按住研磨", new Vector2(0, -160), new Vector2(140, 42), Accent, font);
                var beans = Icon(table, "Beans", "咖啡豆", new Vector2(-230, 130), new Vector2(100, 80), new Color(0.50f, 0.35f, 0.24f), font);
                var dripper = Icon(table, "Dripper", "滤杯", new Vector2(-80, 130), new Vector2(100, 80), Accent, font);
                var filter = Icon(table, "Filter", "滤纸", new Vector2(80, 130), new Vector2(100, 80), new Color(0.56f, 0.56f, 0.47f), font);
                var grounds = Icon(table, "Grounds", "咖啡粉", new Vector2(-190, 25), new Vector2(100, 80), new Color(0.42f, 0.30f, 0.21f), font);
                var kettle = Icon(table, "Kettle", "水壶", new Vector2(245, 130), new Vector2(110, 90), new Color(0.28f, 0.43f, 0.61f), font);
                var grinderZone = Icon(table, "GrinderZone", "研磨机\n拖咖啡豆到此", new Vector2(-145, -65), new Vector2(210, 90), new Color(0.51f, 0.59f, 0.58f), font);
                var cupZone = Icon(table, "CupZone", "咖啡杯\n滤杯 / 滤纸 / 咖啡粉", new Vector2(160, -65), new Vector2(240, 110), new Color(0.51f, 0.59f, 0.58f), font);
                var pourZone = Rect(table, "PourZone", new Vector2(240, 110), new Vector2(160, -65));
                var placedDripper = Icon(cupZone, "PlacedDripper", "滤杯已就位", new Vector2(0, 35), new Vector2(220, 24), Accent, font);
                var placedFilter = Icon(cupZone, "PlacedFilter", "滤纸已就位", new Vector2(0, -35), new Vector2(220, 24), Accent, font);
                var water = Icon(table, "WaterEffect", "注水中…", new Vector2(160, 15), new Vector2(140, 28), new Color(0.28f, 0.58f, 0.76f), font);
                grinderZone.GetComponent<Image>().raycastTarget = false;
                cupZone.GetComponent<Image>().raycastTarget = false;
                placedDripper.GetComponent<Image>().raycastTarget = false;
                placedFilter.GetComponent<Image>().raycastTarget = false;
                water.GetComponent<Image>().raycastTarget = false;
                placedDripper.gameObject.SetActive(false); placedFilter.gameObject.SetActive(false); water.gameObject.SetActive(false);
                Label(root, "Instruction", "按顺序点击、按住或拖拽器具 · 注水时将水壶按住在咖啡杯区域", new Vector2(1000, 40), new Vector2(0, -235), font, 20);
                var progress = Progress(root, "Progress", new Vector2(1000, 14), new Vector2(0, -275));
                game.Configure(heat, grind, beans, dripper, filter, grounds, kettle, grinderZone, cupZone, pourZone,
                    placedDripper.gameObject, placedFilter.gameObject, water.gameObject, steps, progress);
            }
            panel.RegisterContent(4, game);
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
            panel.RegisterContent(5, game);
        }

        private static void RegisterPrinterPoints(MiniGamePanelUI panel)
        {
            var managers = Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var points = new List<PrinterRepairPoint>();
            foreach (var point in Object.FindObjectsByType<PrinterRepairPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == panel.gameObject.scene)
                {
                    point.ConfigureGameIndex(5);
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
            var cleanPoints = new List<CleanMopWater>();
            foreach (var point in Object.FindObjectsByType<CleanMopWater>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameIndex(0); cleanPoints.Add(point); }
            var cleanQuest = FindQuest<CleanQuest>(panel);
            if (cleanPoints.Count > 0)
            {
                if (cleanQuest == null) cleanQuest = cleanPoints[0].gameObject.AddComponent<CleanQuest>();
                cleanQuest.ConfigurePoints(cleanPoints);
                Register(managers, cleanQuest);
            }

            var coffeePoints = new List<TaskMakeCoffee>();
            foreach (var point in Object.FindObjectsByType<TaskMakeCoffee>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameIndex(4); coffeePoints.Add(point); }
            TaskMakeCoffee coffee = coffeePoints.Count > 0 ? coffeePoints[0] : null;
            var coffeeQuest = FindQuest<CoffeeQuest>(panel);
            if (coffeePoints.Count > 0)
            {
                if (coffeeQuest == null) coffeeQuest = coffeePoints[0].gameObject.AddComponent<CoffeeQuest>();
                coffeeQuest.ConfigurePoints(coffeePoints);
                Register(managers, coffeeQuest);
            }
            foreach (var legacy in Object.FindObjectsByType<CoffeeMinigameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (legacy.gameObject.scene == scene) { legacy.HideLegacyView(); legacy.enabled = false; }
            foreach (var legacy in Object.FindObjectsByType<CoffeeDeskInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (legacy.gameObject.scene == scene) legacy.enabled = false;
            var existingStockPoints = Object.FindObjectsByType<StockTradePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasStock = false;
            foreach (var point in existingStockPoints) if (point.gameObject.scene == scene) hasStock = true;
            if (!hasStock && coffee != null)
            {
                var go = new GameObject("StockTradePoint");
                go.transform.SetParent(coffee.transform.parent, false);
                go.transform.position = coffee.transform.position + Vector3.right * 3f;
                var collider = go.AddComponent<CapsuleCollider>();
                collider.isTrigger = true; collider.radius = 1.5f; collider.height = 3f;
                go.AddComponent<StockTradePoint>();
                var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                terminal.name = "StockTerminalPlaceholder";
                terminal.transform.SetParent(go.transform, false);
                terminal.transform.localPosition = Vector3.up * 0.6f;
                terminal.transform.localScale = new Vector3(0.8f, 1.2f, 0.5f);
                var solid = terminal.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(solid); else Object.DestroyImmediate(solid);
            }
            var stockPoints = new List<StockTradePoint>();
            foreach (var point in Object.FindObjectsByType<StockTradePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point.gameObject.scene == scene) { point.ConfigureGameIndex(3); stockPoints.Add(point); }
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
