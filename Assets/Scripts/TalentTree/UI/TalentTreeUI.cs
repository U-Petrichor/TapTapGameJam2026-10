using TapTapGameJam.Audio;
using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace TalentTree
{
    public sealed class TalentTreeUI : MonoBehaviour
    {
        [SerializeField] private TalentTreeAsset _talentTreeAsset;
        [SerializeField] private PlayerTalentTree _playerTalentTree;
        [SerializeField] private TMP_FontAsset _fontAsset;
        [Header("Hand-authored UI (optional)")]
        [SerializeField] private GameObject _talentTreePanel;
        [SerializeField] private Button _talentTreeCloseButton;
        [SerializeField] private TMP_Text _talentPoints;
        [SerializeField] private TMP_Text _introduceTitleText;
        [SerializeField] private TMP_Text _introduceDescriptionText;
        [SerializeField] private TMP_Text _introduceTypeText;
        [SerializeField] private TMP_Text _introduceUnlockText;
        [SerializeField] private TMP_Text _introduceUnlockCostText;
        [SerializeField] private Image _introduceIcon;

        [Header("Talent tree navigation")]
        [SerializeField, Min(0.1f)] private float _minimumZoom = 0.75f;
        [SerializeField, Min(0.1f)] private float _maximumZoom = 2.5f;
        [SerializeField, Min(1.01f)] private float _zoomStep = 1.1f;

        [Header("Talent tree background shadow")]
        [SerializeField, Range(0f, 1f)] private float _backgroundShadowOpacity = 0.94f;
        [SerializeField] private Vector2 _backgroundShadowCenter = new(0.5f, 0.12f);
        [SerializeField] private Vector2 _backgroundShadowRadius = new(0.58f, 0.95f);

        private readonly Dictionary<TalentData, Button> _nodeButtons = new();
        private readonly Dictionary<TalentData, TMP_Text> _nodeLabels = new();
        private GameObject _panel;
        private TMP_Text _pointsText;
        private TMP_Text _titleText;
        private TMP_Text _detailText;
        private Button _upgradeButton;
        private TalentData _selected;
        private TalentNodeView[] _authoredNodes = System.Array.Empty<TalentNodeView>();
        private TalentConnectionView[] _authoredConnections = System.Array.Empty<TalentConnectionView>();
        private bool _usingAuthoredUi;
        private PlayerInput _playerInput;
        private TapTapGameJam.Player.PlayerInputManager _playerInputManager;
        private InputActionMap _talentTreeActions;
        private InputAction _pressE;
        private InputAction _pressF;
        private InputAction _pressT;
        private InputAction _pressQ;
        private InputAction _pressEscape;
        private string _previousActionMapName;
        private bool _talentTreeInputActive;
        private RectTransform _panelRect;
        private RectTransform _treeContent;
        private RectTransform _introducePanelRect;
        private RectTransform _detailsRect;
        private RectTransform _backgroundShadowRect;
        private Vector3 _initialTreeContentPosition;
        private Vector3 _initialTreeContentScale;
        private float _zoom = 1f;
        private bool _isPanning;
        private Vector2 _lastPanPointer;
        private static Sprite _defaultSprite;
        private static Texture2D _backgroundShadowTexture;
        private static TMP_FontAsset _activeFontAsset;

        private void OnValidate()
        {
            _minimumZoom = Mathf.Max(0.1f, _minimumZoom);
            _maximumZoom = Mathf.Max(_minimumZoom, _maximumZoom);
            _zoomStep = Mathf.Max(1.01f, _zoomStep);
        }

        private void Awake()
        {
            _activeFontAsset = _fontAsset;
            if (_playerTalentTree == null) _playerTalentTree = FindFirstObjectByType<PlayerTalentTree>();
            if (_talentTreeAsset == null) _talentTreeAsset = _playerTalentTree?.TalentTreeAsset;
            InitializeTalentTreeInput();
            _authoredNodes = GetComponentsInChildren<TalentNodeView>(true);
            _authoredConnections = GetComponentsInChildren<TalentConnectionView>(true);
            _usingAuthoredUi = _authoredNodes.Length > 0;
            if (_usingAuthoredUi)
            {
                _panel = _talentTreePanel;
                _pointsText = _talentPoints;
                foreach (TalentNodeView node in _authoredNodes)
                {
                    node.Bind(this);
                    TalentData data = _talentTreeAsset?.FindTalent(node.TalentId);
                    node.SetInteractable(_talentTreeAsset?.FindNode(data)?.IsRoot != true);
                }
                if (_upgradeButton != null) _upgradeButton.onClick.AddListener(UpgradeSelected);
                if (_talentTreeCloseButton != null) _talentTreeCloseButton.onClick.AddListener(() => SetVisible(false));
                InitializeTreeNavigation();
                ResolveIntroduceReferences();
                _titleText = _introduceTitleText;
                _detailText = _introduceDescriptionText;
                SetVisible(false);
                Refresh();
                return;
            }
            BuildUi();
            InitializeTreeNavigation();
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<TalentLevelChangedEvent>(OnTalentChanged);
            Refresh();
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<TalentLevelChangedEvent>(OnTalentChanged);
            SetVisible(false);
        }

        private void Update()
        {
            if (TapTapGameJam.UI.IntroVideoTransition.IsTransitioning) return;
            if (_panel == null) return;

            if (!_panel.activeSelf)
            {
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) SetVisible(true);
                return;
            }

            bool closeWithT = WasPressedOrFallback(_pressT, Keyboard.current != null ? Keyboard.current.tKey : null);
            bool closeWithEscape = WasPressedOrFallback(_pressEscape, Keyboard.current != null ? Keyboard.current.escapeKey : null);
            if (closeWithT || closeWithEscape)
            {
                SetVisible(false);
                return;
            }

            if (WasPressed(_pressE, _talentTreeActions == null && _playerInput == null && Keyboard.current != null ? Keyboard.current.eKey : null))
                UnlockSelected();
            if (WasPressed(_pressF, _talentTreeActions == null && _playerInput == null && Keyboard.current != null ? Keyboard.current.fKey : null))
                RemoveSelected();
            if (WasPressed(_pressQ, _talentTreeActions == null && _playerInput == null && Keyboard.current != null ? Keyboard.current.qKey : null))
                ResetAllTalents();

            HandleTreeNavigation();
        }

        private void BuildUi()
        {
            Button toggle = CreateButton(transform, "天赋树 (T)", new Vector2(150f, 42f));
            SetAnchor(toggle.transform as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(150f, 42f));
            toggle.onClick.AddListener(() => SetVisible(!_panel.activeSelf));

            _panel = new GameObject("TalentTreePanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            RectTransform panelRect = (RectTransform)_panel.transform;
            SetAnchor(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 820f));
            SetImage(_panel.GetComponent<Image>(), new Color(0.035f, 0.055f, 0.04f, 0.97f));

            TMP_Text header = CreateText(_panel.transform, "天赋树", 30, TextAnchor.MiddleLeft);
            if (header != null) SetAnchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -72f), new Vector2(-56f, 52f));
            _pointsText = CreateText(_panel.transform, "天赋点 0", 22, TextAnchor.MiddleRight);
            if (_pointsText != null) SetAnchor(_pointsText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -72f), new Vector2(-56f, 52f));
            Button close = CreateButton(_panel.transform, "关闭", new Vector2(80f, 38f));
            SetAnchor(close.transform as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(80f, 38f));
            close.onClick.AddListener(() => SetVisible(false));

            RectTransform graph = CreateArea(_panel.transform, "Graph", new Vector2(1040f, 680f), new Vector2(-200f, -20f));
            RectTransform details = CreateArea(_panel.transform, "Details", new Vector2(360f, 680f), new Vector2(540f, -20f));
            SetImage(details.GetComponent<Image>(), new Color(0.1f, 0.14f, 0.1f, 1f));
            _titleText = CreateText(details, "选择一个天赋", 24, TextAnchor.UpperLeft);
            if (_titleText != null) SetAnchor(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -90f), new Vector2(-44f, 50f));
            _detailText = CreateText(details, string.Empty, 16, TextAnchor.UpperLeft);
            if (_detailText != null) SetAnchor(_detailText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -160f), new Vector2(-44f, 280f));
            _upgradeButton = CreateButton(details, "升级", new Vector2(300f, 52f));
            SetAnchor(_upgradeButton.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 52f));
            _upgradeButton.onClick.AddListener(UpgradeSelected);

            if (_talentTreeAsset == null) return;
            Vector2[] positions = CalculatePositions(graph.rect.size, _talentTreeAsset.Nodes);
            for (int i = 0; i < _talentTreeAsset.Nodes.Count; i++)
            {
                TalentNodeData node = _talentTreeAsset.Nodes[i];
                if (node?.Data == null) continue;
                foreach (TalentGraphEdgeData edge in _talentTreeAsset.Edges)
                {
                    if (edge?.First != node.Data) continue;
                    int otherIndex = IndexOf(edge.Second);
                    if (otherIndex >= 0) CreateLine(graph, positions[i], positions[otherIndex]);
                }
            }

            for (int i = 0; i < _talentTreeAsset.Nodes.Count; i++)
            {
                TalentNodeData node = _talentTreeAsset.Nodes[i];
                if (node?.Data == null) continue;
                Button button = CreateButton(graph, node.Data.DisplayName, new Vector2(148f, 86f));
                SetAnchor(button.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), positions[i], new Vector2(148f, 86f));
                TalentData data = node.Data;
                button.onClick.AddListener(() => Select(data));
                _nodeButtons[data] = button;
                _nodeLabels[data] = button.GetComponentInChildren<TMP_Text>();
            }
        }

        private void Refresh()
        {
            if (_playerTalentTree == null) _playerTalentTree = FindFirstObjectByType<PlayerTalentTree>();
            if (_playerTalentTree == null || _talentTreeAsset == null) return;
            if (_pointsText != null) _pointsText.text = $"天赋点 {_playerTalentTree.PlayerResources?.TalentPoint ?? 0}";

            foreach (TalentNodeData node in _talentTreeAsset.Nodes)
            {
                TalentData data = node?.Data;
                if (data == null || !_nodeButtons.TryGetValue(data, out Button button)) continue;
                int level = _playerTalentTree.GetLevel(data);
                bool canUpgrade = _playerTalentTree.CanUpgrade(data, out _);
                Color color = level > 0 ? new Color(0.25f, 0.55f, 0.3f) : canUpgrade ? new Color(0.65f, 0.55f, 0.2f) : new Color(0.2f, 0.25f, 0.22f);
                SetImage(button.GetComponent<Image>(), color);
                if (_nodeLabels.TryGetValue(data, out TMP_Text label) && label != null)
                    label.text = $"{data.DisplayName}\n{(level > 0 ? "已解锁" : "未解锁")}";
            }
            foreach (TalentNodeView node in _authoredNodes)
            {
                TalentData data = _talentTreeAsset.FindTalent(node.TalentId);
                node.Refresh(_playerTalentTree, data);
            }
            foreach (TalentConnectionView connection in _authoredConnections)
                connection.Refresh(_playerTalentTree, _talentTreeAsset);
            Select(_selected);
        }

        public void SelectById(string talentId)
        {
            TalentData data = _talentTreeAsset?.FindTalent(talentId);
            if (_talentTreeAsset?.FindNode(data)?.IsRoot == true) return;
            Select(data);
        }

        private void Select(TalentData data)
        {
            _selected = data;
            SetIntroduceVisible(data != null);
            foreach (TalentNodeView node in _authoredNodes)
                node.SetSelected(data != null && node.TalentId == data.Id);
            RefreshIntroduction();
            if (_selected == null || _playerTalentTree == null)
            {
                if (_titleText != null) _titleText.text = "选择一个天赋";
                if (_detailText != null) _detailText.text = string.Empty;
                if (_upgradeButton != null) _upgradeButton.interactable = false;
                return;
            }

            int level = _playerTalentTree.GetLevel(_selected);
            bool canUnlock = level <= 0 && _playerTalentTree.CanUnlock(_selected, out _);
            if (_titleText != null) _titleText.text = _selected.DisplayName;
            if (_detailText != null) _detailText.text = _selected.Description ?? string.Empty;
            if (_upgradeButton != null) _upgradeButton.interactable = canUnlock;
        }

        private void RefreshIntroduction()
        {
            int level = _selected != null && _playerTalentTree != null ? _playerTalentTree.GetLevel(_selected) : 0;
            if (_introduceTypeText != null)
                _introduceTypeText.text = _selected != null ? _selected.TalentType : string.Empty;
            if (_introduceUnlockText != null)
                _introduceUnlockText.text = _selected != null && level > 0 ? "1/1" : "0/1";
            if (_introduceIcon != null)
            {
                _introduceIcon.sprite = _selected != null ? _selected.Icon : null;
                _introduceIcon.enabled = _introduceIcon.sprite != null;
            }
            if (_introduceUnlockCostText != null)
            {
                bool showCost = _selected != null && level <= 0;
                _introduceUnlockCostText.gameObject.SetActive(showCost);
                if (showCost)
                    _introduceUnlockCostText.text = $"需要点数：{_selected.GetLevelData(1)?.TalentPointCost ?? 0}";
            }
        }

        private void SetIntroduceVisible(bool visible)
        {
            if (_introducePanelRect == null) return;
            if (_introducePanelRect.gameObject.activeSelf != visible)
                _introducePanelRect.gameObject.SetActive(visible);
        }

        private void ResolveIntroduceReferences()
        {
            if (_introducePanelRect == null) return;
            if (_introduceTitleText == null) _introduceTitleText = FindChildComponent<TMP_Text>(_introducePanelRect, "Title");
            if (_introduceDescriptionText == null) _introduceDescriptionText = FindChildComponent<TMP_Text>(_introducePanelRect, "Description");
            if (_introduceTypeText == null) _introduceTypeText = FindChildComponent<TMP_Text>(_introducePanelRect, "Type");
            if (_introduceUnlockText == null) _introduceUnlockText = FindChildComponent<TMP_Text>(_introducePanelRect, "Unlock");
            if (_introduceUnlockCostText == null) _introduceUnlockCostText = FindChildComponent<TMP_Text>(_introducePanelRect, "UnlockCostPoint");
            if (_introduceIcon == null) _introduceIcon = FindChildComponent<Image>(_introducePanelRect, "Icon");
        }

        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void UpgradeSelected()
        {
            if (_selected == null || _playerTalentTree == null) return;
            if (!_playerTalentTree.TryUpgrade(_selected).Succeeded) Sfx.Play(SoundId.Error, this);
            Refresh();
        }

        private void UnlockSelected()
        {
            if (_selected == null || _playerTalentTree == null) return;

            TalentOperationResult result = _playerTalentTree.TryUnlock(_selected);
            if (!result.Succeeded) Sfx.Play(SoundId.Error, this);
            Refresh();
        }

        private void RemoveSelected()
        {
            if (_selected == null || _playerTalentTree == null) return;

            TalentOperationResult result = _playerTalentTree.TryRemove(_selected);
            if (!result.Succeeded) Sfx.Play(SoundId.Error, this);
            Refresh();
        }

        private void ResetAllTalents()
        {
            if (_playerTalentTree == null) return;
            _playerTalentTree.TryResetAll();
            Refresh();
        }

        private void OnTalentChanged(TalentLevelChangedEvent _) => Refresh();

        public bool IsOpen => _panel != null && _panel.activeInHierarchy;
        public void Open()
        {
            if (!isActiveAndEnabled || _panel == null) return;
            SetVisible(true);
            TutorialSignal.Send(TutorialSignals.TalentOpened);
        }
        public void Close() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (visible && !isActiveAndEnabled) return;
            if (_panel == null) return;
            if (!visible)
            {
                _isPanning = false;
                DisableTalentTreeInput();
            }
            _panel.SetActive(visible);
            if (visible)
            {
                EnableTalentTreeInput();
                ResetTreeView();
                Select(null);
                Refresh();
            }
        }

        private void InitializeTalentTreeInput()
        {
            _playerInput = _playerTalentTree != null
                ? _playerTalentTree.GetComponent<PlayerInput>()
                : FindFirstObjectByType<PlayerInput>();
            _playerInput ??= FindFirstObjectByType<PlayerInput>();
            _playerInputManager = _playerInput != null
                ? _playerInput.GetComponent<TapTapGameJam.Player.PlayerInputManager>()
                : FindFirstObjectByType<TapTapGameJam.Player.PlayerInputManager>();

            _talentTreeActions = _playerInput?.actions?.FindActionMap("TalentTree", false);
            if (_talentTreeActions == null) return;
            _pressE = _talentTreeActions.FindAction("PressE", false);
            _pressF = _talentTreeActions.FindAction("PressF", false);
            _pressT = _talentTreeActions.FindAction("PressT", false);
            _pressQ = _talentTreeActions.FindAction("PressQ", false);
            _pressEscape = _talentTreeActions.FindAction("PressESC", false);
        }

        private void EnableTalentTreeInput()
        {
            if (_talentTreeInputActive) return;
            if (_playerInput == null || _playerInputManager == null) InitializeTalentTreeInput();
            if (_playerInput == null) return;

            _previousActionMapName = _playerInput.currentActionMap?.name;
            if (_playerInputManager != null) _playerInputManager.SwitchActionMap("TalentTree");
            else _playerInput.SwitchCurrentActionMap("TalentTree");
            _talentTreeInputActive = _playerInput.currentActionMap?.name == "TalentTree";
        }

        private void DisableTalentTreeInput()
        {
            if (!_talentTreeInputActive || _playerInput == null) return;
            string restoreMap = string.IsNullOrEmpty(_previousActionMapName) || _previousActionMapName == "TalentTree"
                ? "DayPlayer"
                : _previousActionMapName;
            if (_playerInputManager != null) _playerInputManager.SwitchActionMap(restoreMap);
            else _playerInput.SwitchCurrentActionMap(restoreMap);
            _talentTreeInputActive = false;
        }

        private static bool WasPressed(InputAction action, KeyControl fallback)
        {
            return action != null ? action.WasPressedThisFrame() : fallback != null && fallback.wasPressedThisFrame;
        }

        private static bool WasPressedOrFallback(InputAction action, KeyControl fallback)
        {
            return (action != null && action.WasPressedThisFrame()) || (fallback != null && fallback.wasPressedThisFrame);
        }

        private void InitializeTreeNavigation()
        {
            if (_panel == null) return;
            _panelRect = _panel.transform as RectTransform;
            if (_panelRect == null) return;

            _treeContent = FindRectTransform("Nodes") ?? FindRectTransform("Graph");
            InitializeBackgroundShadow();
            if (_treeContent == null) return;

            _introducePanelRect = FindRectTransform("IntroducePanel");
            _detailsRect = FindRectTransform("Details");
            _initialTreeContentPosition = _treeContent.position;
            _initialTreeContentScale = _treeContent.localScale;
            _zoom = 1f;

            SetIntroduceVisible(false);

            // Keep the fixed UI above the moving tree even if the scene's
            // sibling order is changed later in the editor.
            _introducePanelRect?.SetAsLastSibling();
            _detailsRect?.SetAsLastSibling();
            _talentPoints?.rectTransform.SetAsLastSibling();
            _pointsText?.rectTransform.SetAsLastSibling();
            _talentTreeCloseButton?.transform.SetAsLastSibling();

            // The panel itself is the clipping viewport. Fixed sibling UI is
            // rendered after Nodes and therefore remains above the moving tree.
            if (_panel.GetComponent<RectMask2D>() == null) _panel.AddComponent<RectMask2D>();
        }

        private void InitializeBackgroundShadow()
        {
            // A fixed layer behind the tree keeps nodes, connections and labels crisp.
            Transform shadowParent = !_usingAuthoredUi && _treeContent != null ? _treeContent : _panel.transform;
            Transform existing = shadowParent.Find("BackgroundShadow");
            RawImage shadow = existing != null ? existing.GetComponent<RawImage>() : null;
            if (shadow == null)
            {
                GameObject go = new("BackgroundShadow", typeof(RectTransform), typeof(RawImage));
                go.layer = _panel.layer;
                go.transform.SetParent(shadowParent, false);
                shadow = go.GetComponent<RawImage>();
            }

            if (_backgroundShadowTexture == null)
            {
                const int resolution = 512;
                _backgroundShadowTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                {
                    name = "TalentTreeSoftShadow",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                Color32[] pixels = new Color32[resolution * resolution];
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Vector2 point = new Vector2(x, y) / (resolution - 1f) * 2f - Vector2.one;
                        float fade = Mathf.InverseLerp(0.12f, 1f, point.magnitude);
                        float alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);
                        pixels[y * resolution + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }
                _backgroundShadowTexture.SetPixels32(pixels);
                _backgroundShadowTexture.Apply(false, true);
            }

            Vector2 radius = new(Mathf.Max(0.01f, _backgroundShadowRadius.x), Mathf.Max(0.01f, _backgroundShadowRadius.y));
            RectTransform rect = shadow.rectTransform;
            _backgroundShadowRect = rect;
            rect.anchorMin = _backgroundShadowCenter - radius;
            rect.anchorMax = _backgroundShadowCenter + radius;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // Immediately below the tree, above any authored background images.
            rect.SetSiblingIndex(_usingAuthoredUi && _treeContent != null ? _treeContent.GetSiblingIndex() : 0);
            shadow.texture = _backgroundShadowTexture;
            shadow.color = new Color(1f, 1f, 1f, _backgroundShadowOpacity);
            shadow.raycastTarget = false;
        }

        private RectTransform FindRectTransform(string childName)
        {
            if (_panel == null) return null;
            Transform child = _panel.transform.Find(childName);
            return child as RectTransform;
        }

        private void ResetTreeView()
        {
            if (_treeContent == null) return;
            _isPanning = false;
            _zoom = 1f;
            _treeContent.localScale = _initialTreeContentScale;
            _treeContent.position = _initialTreeContentPosition;
        }

        private void HandleTreeNavigation()
        {
            if (_treeContent == null || Mouse.current == null) return;

            Vector2 pointerScreen = Mouse.current.position.ReadValue();
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                if (IsTreePointer(pointerScreen))
                {
                    _isPanning = true;
                    _lastPanPointer = pointerScreen;
                }
            }

            if (_isPanning)
            {
                if (!Mouse.current.middleButton.isPressed)
                {
                    _isPanning = false;
                }
                else if (TryGetPanelLocalPoint(pointerScreen, out Vector2 currentPoint) &&
                         TryGetPanelLocalPoint(_lastPanPointer, out Vector2 previousPoint))
                {
                    MoveContentInPanelSpace(currentPoint - previousPoint);
                    _lastPanPointer = pointerScreen;
                }
            }

            if (!IsTreePointer(pointerScreen)) return;
            Vector2 scroll = Mouse.current.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) < 0.001f) return;

            // Mouse wheels commonly report 120 units per detent, while some
            // touchpads report values close to one. Support both conventions.
            float scrollSteps = Mathf.Abs(scroll.y) > 10f ? scroll.y / 120f : scroll.y;
            float targetZoom = Mathf.Clamp(_zoom * Mathf.Pow(_zoomStep, scrollSteps), _minimumZoom, _maximumZoom);
            if (!Mathf.Approximately(targetZoom, _zoom)) ApplyZoom(targetZoom, pointerScreen);
        }

        private bool IsTreePointer(Vector2 screenPoint)
        {
            if (_panelRect == null || !RectTransformUtility.RectangleContainsScreenPoint(_panelRect, screenPoint, UiCamera())) return false;
            return !IsPointerOver(_introducePanelRect, screenPoint) &&
                   !IsPointerOver(_detailsRect, screenPoint) &&
                   !IsPointerOver(_talentTreeCloseButton?.transform as RectTransform, screenPoint) &&
                   !IsPointerOver(_talentPoints?.rectTransform, screenPoint);
        }

        private bool IsPointerOver(RectTransform rect, Vector2 screenPoint)
        {
            return rect != null && rect.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, UiCamera());
        }

        private Camera UiCamera()
        {
            Canvas canvas = _panelRect != null ? _panelRect.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private bool TryGetPanelLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_panelRect, screenPoint, UiCamera(), out localPoint);
        }

        private void ApplyZoom(float targetZoom, Vector2 pointerScreen)
        {
            if (!TryGetPanelLocalPoint(pointerScreen, out Vector2 pointerPanelPoint)) return;

            Vector3 pointerWorld = _panelRect.TransformPoint(pointerPanelPoint);
            Vector3 contentPoint = _treeContent.InverseTransformPoint(pointerWorld);
            _zoom = targetZoom;
            _treeContent.localScale = _initialTreeContentScale * _zoom;

            Vector3 scaledPointWorld = _treeContent.TransformPoint(contentPoint);
            _treeContent.position += pointerWorld - scaledPointWorld;

            if (_zoom <= _minimumZoom + 0.0001f) CenterContent();
            else ClampContentToPanel();
        }

        private void MoveContentInPanelSpace(Vector2 panelDelta)
        {
            if (panelDelta.sqrMagnitude < 0.000001f) return;
            _treeContent.anchoredPosition += panelDelta;
            ClampContentToPanel();
        }

        private void CenterContent()
        {
            Bounds bounds = GetContentBoundsInPanelSpace();
            Vector2 delta = (Vector2)_panelRect.rect.center - (Vector2)bounds.center;
            MoveContentInPanelSpaceWithoutClamp(delta);
        }

        private void ClampContentToPanel()
        {
            Bounds bounds = GetContentBoundsInPanelSpace();
            Rect viewport = _panelRect.rect;
            Vector2 delta = Vector2.zero;

            if (bounds.size.x <= viewport.width)
            {
                if (_zoom <= _minimumZoom + 0.0001f) delta.x = viewport.center.x - bounds.center.x;
                else if (bounds.max.x < viewport.min.x) delta.x = viewport.min.x - bounds.max.x;
                else if (bounds.min.x > viewport.max.x) delta.x = viewport.max.x - bounds.min.x;
            }
            else if (bounds.min.x > viewport.min.x)
                delta.x = viewport.min.x - bounds.min.x;
            else if (bounds.max.x < viewport.max.x)
                delta.x = viewport.max.x - bounds.max.x;

            if (bounds.size.y <= viewport.height)
            {
                if (_zoom <= _minimumZoom + 0.0001f) delta.y = viewport.center.y - bounds.center.y;
                else if (bounds.max.y < viewport.min.y) delta.y = viewport.min.y - bounds.max.y;
                else if (bounds.min.y > viewport.max.y) delta.y = viewport.max.y - bounds.min.y;
            }
            else if (bounds.min.y > viewport.min.y)
                delta.y = viewport.min.y - bounds.min.y;
            else if (bounds.max.y < viewport.max.y)
                delta.y = viewport.max.y - bounds.max.y;

            MoveContentInPanelSpaceWithoutClamp(delta);
        }

        private void MoveContentInPanelSpaceWithoutClamp(Vector2 panelDelta)
        {
            _treeContent.anchoredPosition += panelDelta;
        }

        private Bounds GetContentBoundsInPanelSpace()
        {
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            for (int i = 0; i < _treeContent.childCount; i++)
            {
                if (!(_treeContent.GetChild(i) is RectTransform child) || child == _backgroundShadowRect || !child.gameObject.activeInHierarchy) continue;
                Vector3[] corners = new Vector3[4];
                child.GetWorldCorners(corners);
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector3 panelPoint = _panelRect.InverseTransformPoint(corners[corner]);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(panelPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(panelPoint);
                }
            }

            if (hasBounds) return bounds;
            Vector3[] contentCorners = new Vector3[4];
            _treeContent.GetWorldCorners(contentCorners);
            for (int i = 0; i < contentCorners.Length; i++)
            {
                Vector3 panelPoint = _panelRect.InverseTransformPoint(contentCorners[i]);
                if (!hasBounds)
                {
                    bounds = new Bounds(panelPoint, Vector3.zero);
                    hasBounds = true;
                }
                else bounds.Encapsulate(panelPoint);
            }
            return bounds;
        }

        private int IndexOf(TalentData data)
        {
            for (int i = 0; i < _talentTreeAsset.Nodes.Count; i++) if (_talentTreeAsset.Nodes[i]?.Data == data) return i;
            return -1;
        }

        private static Vector2[] CalculatePositions(Vector2 size, IReadOnlyList<TalentNodeData> nodes)
        {
            var result = new Vector2[nodes.Count];
            if (nodes.Count == 0) return result;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (TalentNodeData node in nodes)
            {
                if (node == null) continue;
                minX = Mathf.Min(minX, node.Position.x); maxX = Mathf.Max(maxX, node.Position.x);
                minY = Mathf.Min(minY, node.Position.y); maxY = Mathf.Max(maxY, node.Position.y);
            }
            float dx = Mathf.Max(1f, maxX - minX), dy = Mathf.Max(1f, maxY - minY);
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 p = nodes[i]?.Position ?? Vector2.zero;
                result[i] = new Vector2(Mathf.Lerp(-size.x * 0.44f, size.x * 0.44f, (p.x - minX) / dx), Mathf.Lerp(-size.y * 0.42f, size.y * 0.42f, (p.y - minY) / dy));
            }
            return result;
        }

        private static RectTransform CreateArea(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            SetImage(go.GetComponent<Image>(), new Color(0.05f, 0.08f, 0.06f, 0.82f));
            return rect;
        }

        private static Button CreateButton(Transform parent, string text, Vector2 size)
        {
            GameObject go = new(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetImage(go.GetComponent<Image>(), new Color(0.25f, 0.38f, 0.27f, 1f));
            TMP_Text label = CreateText(go.transform, text, 16, TextAnchor.MiddleCenter);
            if (label != null) SetAnchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateText(Transform parent, string text, int size, TextAnchor anchor)
        {
            GameObject go = new("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TMP_Text label = go.AddComponent<TextMeshProUGUI>();
            if (label == null) return null;
            label.text = text;
            label.font = _activeFontAsset ?? FindFont();
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                _ => TextAlignmentOptions.Center
            };
            return label;
        }

        private static TMP_FontAsset FindFont()
        {
            // This component is also used by the runtime preview. The actual
            // production UI can override the font from the Inspector.
            return Resources.Load<TMP_FontAsset>("Fonts/AlibabaPuHuiTi-3-55-Regular SDF") ??
                   Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
        }

        private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void CreateLine(Transform parent, Vector2 from, Vector2 to)
        {
            GameObject go = new("GraphEdge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            Vector2 delta = to - from;
            SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (from + to) * 0.5f, new Vector2(delta.magnitude, 3f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            SetImage(go.GetComponent<Image>(), new Color(0.45f, 0.7f, 0.5f, 0.8f));
            go.transform.SetAsFirstSibling();
        }

        private static void SetImage(Image image, Color color)
        {
            if (image == null) return;
            image.sprite ??= GetDefaultSprite();
            image.color = color;
        }

        private static Sprite GetDefaultSprite()
        {
            if (_defaultSprite != null) return _defaultSprite;
            _defaultSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (_defaultSprite != null) return _defaultSprite;

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _defaultSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            _defaultSprite.hideFlags = HideFlags.HideAndDontSave;
            return _defaultSprite;
        }
    }

}
