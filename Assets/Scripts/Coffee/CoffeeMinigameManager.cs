using System.Collections;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.Coffee
{
    // 场景中的咖啡流程协调者：连接配方、3D 器具、输入和 UI，直接开关自己的镜头。
    public class CoffeeMinigameManager : MonoBehaviour
    {
        // 小游戏整体状态；具体的烧水、磨豆等步骤由 CoffeeRecipe 记录。
        public enum SessionState
        {
            Inactive, // 未开始，玩家可以正常活动。
            Entering, // 正在进入，等待镜头切到桌前。
            Playing,  // 正在制作咖啡。
            Exiting   // 正在退出，等待镜头返回。
        }

        // 咖啡制作镜头：固定在桌前的 Cinemachine 镜头。
        private CinemachineCamera _coffeeCamera;

        // 主相机上的 Brain：只查询实际输出相机和过渡状态，不修改优先级。
        [Tooltip("拖入 Main Camera 上的 CinemachineBrain；不填时从带 MainCamera 标签的相机查找。")]
        [SerializeField] private CinemachineBrain _brain;

        // 桌面交互器：统一处理鼠标点击、按住和拖拽。
        [Tooltip("【必填】桌面交互控制器。拖入 CoffeeGame 上的 CoffeeDeskInteractor 组件。")]
        [SerializeField] private CoffeeDeskInteractor _interactor;

        // 等待镜头切换的最长时间，单位为现实秒数。
        [Tooltip("镜头切换最多等待多少秒。进入时超时会中止制作，返回时超时会结束等待并恢复操作；不是咖啡制作时限。")]
        [SerializeField, Min(1f)] private float _cameraTimeout = 8f;

        [Header("器具：每个字段对应一个实际物体")]
        // 热水壶开关：玩家点击这里开始加热。
        [Tooltip("【必填】热水壶的加热开关。拖入开关上的 CoffeeClickable 组件；也可以直接使用壶身上的 CoffeeClickable。")]
        [SerializeField] private CoffeeClickable _kettleSwitch;

        // 研磨机操作部位：玩家在这里按住鼠标磨豆。
        [Tooltip("【必填】研磨机按钮或手柄的可点击部位。拖入其 CoffeeClickable 组件，玩家按住这里推进研磨进度。")]
        [SerializeField] private CoffeeClickable _grinderButton;

        // 热水壶本体：最后一步拖起它，向滤杯注入热水。
        [Tooltip("【必填】可拖动的热水壶本体。拖入壶身上的 CoffeeDraggable 组件；与 Kettle Switch 的开关组件用途不同。")]
        [SerializeField] private CoffeeDraggable _kettle;

        // 咖啡豆：尚未研磨的一份豆子，玩家将它拖入研磨机。
        [Tooltip("【必填】可拖动的一份咖啡豆，也可以用装豆的小容器表示。拖入其 CoffeeDraggable 组件，放入研磨机后该物体会隐藏。")]
        [SerializeField] private CoffeeDraggable _beans;

        // 滤杯：放在接咖啡的杯子上，用来承托滤纸和咖啡粉。
        [Tooltip("【必填】手冲滤杯，通常是漏斗状器具，不是下面接咖啡的杯子。拖入滤杯上的 CoffeeDraggable 组件。")]
        [SerializeField] private CoffeeDraggable _dripper;

        // 滤纸：放入滤杯内部，用来过滤咖啡粉。
        [Tooltip("【必填】可拖动的滤纸。拖入滤纸物体上的 CoffeeDraggable 组件。")]
        [SerializeField] private CoffeeDraggable _filterPaper;

        // 咖啡粉容器（粉杯）：装着磨好的咖啡粉，用来向滤纸倒粉。
        [Tooltip("【必填】盛放磨好咖啡粉的容器。拖入其 CoffeeDraggable 组件；制作开始时隐藏，磨豆结束后显示，倒粉完成后再次隐藏。")]
        [SerializeField] private CoffeeDraggable _groundsCup;

        [Header("放置区域")]
        // 研磨机接收区：判断咖啡豆是否放进研磨机。
        [Tooltip("【必填】研磨机入口处的接收区域。拖入该位置的 CoffeeDropZone；Radius 决定放豆的有效范围。")]
        [SerializeField] private CoffeeDropZone _grinderZone;

        // 杯口接收区：判断滤杯是否放到了接咖啡的杯子上。
        [Tooltip("【必填】下方接咖啡的杯子杯口区域。拖入 CoffeeDropZone；其 Snap Point 决定滤杯放好后的位置和朝向。")]
        [SerializeField] private CoffeeDropZone _cupZone;

        // 滤杯内部接收区：放滤纸、倒咖啡粉和注水共用的目标区域。
        [Tooltip("【必填】滤杯内部的 CoffeeDropZone。建议放在滤杯下面作为子物体，随滤杯移动；接收滤纸、咖啡粉和热水。其 Snap Point 用于滤纸吸附。")]
        [SerializeField] private CoffeeDropZone _filterZone;

        // 倒粉位置：粉杯开始倾倒前，先摆到这个位置和基础朝向。
        [Tooltip("【可选】滤杯上方用于倒粉的空物体 Transform。粉杯先对齐该位置和朝向，再叠加自身 Pour Euler Angles 倾倒角度；不填则在松手位置倾倒。")]
        [SerializeField] private Transform _groundsPourPoint;

        [Header("步骤时长，单位秒")]
        // 加热时长：点击开关后，自动烧水所需的秒数。
        [Tooltip("点击热水壶开关后，加热水所需的秒数。加热过程自动进行，世界倒计时继续。")]
        [SerializeField, Min(0.1f)] private float _heatSeconds = 1f;

        // 研磨时长：需要在研磨机上累计按住鼠标的秒数。
        [Tooltip("磨豆需要累计有效按住多少秒。松手或把鼠标移出研磨机操作部位时停止增长，已有进度保留。")]
        [SerializeField, Min(0.1f)] private float _grindSeconds = 1f;

        // 倒粉时长：粉杯放到正确区域后，自动倾倒咖啡粉所需的秒数。
        [Tooltip("粉杯拖到滤杯区域并松手后，自动倒入咖啡粉所需的秒数。")]
        [SerializeField, Min(0.1f)] private float _groundsSeconds = 0.5f;

        // 注水时长：需要在滤杯上方累计有效注水的秒数。
        [Tooltip("完成注水需要累计多少秒。只在拖着热水壶、按住鼠标且位于滤杯有效区域时增长；可以分次注水。")]
        [SerializeField, Min(0.1f)] private float _waterSeconds = 2f;

        // 滴滤时长：注水量足够后，等待咖啡液滤完的秒数。
        [Tooltip("注水完成后自动等待滴滤的秒数。时间达到后自动获得一份咖啡、关闭小游戏并恢复镜头。")]
        [SerializeField, Min(0.1f)] private float _dripSeconds = 0.5f;

        [Header("界面：只显示提示和进度")]
        // 咖啡面板：进入时显示、退出时隐藏的 UI 根物体。
        [Tooltip("【必填】咖啡提示界面的根物体，如 CoffeePanel。控制器、交互器和桌面器具应放在它外面，避免关闭面板时一起被禁用。")]
        [SerializeField] private GameObject _coffeePanel;

        // 提示文字：显示当前步骤以及玩家应该怎样操作。
        [Tooltip("【必填】显示操作提示的 TextMeshPro UI 文本组件，例如“将滤纸拖到滤杯上”。建议关闭其 Raycast Target，避免挡住桌面点击。")]
        [SerializeField] private TMP_Text _hintText;

        // 进度条：显示当前步骤的完成比例，不是整套流程的总进度。
        [Tooltip("【可选】显示当前步骤进度的 Slider。脚本会设为 0～1 且不可交互；只作显示，不需要玩家拖动滑块。")]
        [SerializeField] private Slider _progressBar;

        [Header("可选表现：不影响配方判定")]
        // 加热表现：烧水期间显示的指示灯、蒸汽等视觉对象。
        [Tooltip("【可选】水壶加热时启用的视觉对象，例如指示灯或蒸汽特效。填独立的表现物体，不要填整个热水壶。")]
        [SerializeField] private GameObject _heatingEffect;

        // 注水水流：热水壶正在有效注水时显示。
        [Tooltip("【可选】壶嘴流出的水流模型或特效物体。建议作为水壶的子物体，跟随壶嘴移动；停止注水时自动隐藏。")]
        [SerializeField] private GameObject _waterStream;

        // 研磨机里的豆子：用于表现已经装入的豆子，不是可拖动的豆子物体。
        [Tooltip("【可选】研磨机内部咖啡豆的视觉对象。豆子放入后显示，研磨完成后隐藏；与 Beans 字段中的可拖动物体分开。")]
        [SerializeField] private GameObject _beansInGrinder;

        // 滤纸里的咖啡粉：倒粉完成后显示的粉层。
        [Tooltip("【可选】滤杯/滤纸中的咖啡粉模型。倒粉完成后显示；不是 Grounds Cup 中可拖动的粉杯。建议放在滤杯下面随其移动。")]
        [SerializeField] private GameObject _groundsInFilter;

        // 杯中的咖啡液：进入滴滤阶段时显示的成品液体表现。
        [Tooltip("【可选】接咖啡的杯子内的液体模型。进入滴滤阶段时显示；当前仅控制显示/隐藏，不自动改变液面高度。")]
        [SerializeField] private GameObject _coffeeLiquid;

        // 研磨机旋转部件：按住磨豆时转动的手柄或转子模型。
        [Tooltip("【可选】研磨机上需要旋转的视觉部件 Transform。有效磨豆时绕自身 Y 轴旋转；请填独立部件，避免整个研磨机和接收区域一起转动。")]
        [SerializeField] private Transform _grinderRotor;

        // 制作完成事件：咖啡已发放后，通知额外的音效、任务等系统。
        [Tooltip("【可选】整杯咖啡完成时调用的额外事件，可连接音效或任务反馈。调用前 CoffeeCount 已增加 1，不要在这里重复发放咖啡。取消制作时不会触发。")]
        [SerializeField] private UnityEvent _onCoffeeCompleted = new UnityEvent();

        // 本次发起制作的玩家，由 Begin(player) 传入；退出时清除。
        private PlayerBasic _player;

        // 本次玩家的资源组件，用于完成后增加 CoffeeCount（持有的咖啡数量）。
        private PlayerResources _resources;

        // 本轮配方实例，记录步骤、进度和顺序限制；每次进入重新创建。
        private CoffeeRecipe _recipe;

        // 本轮需要统一复位的五个物体：水壶、豆子、滤杯、滤纸、粉杯。
        private CoffeeDraggable[] _items;

        // 进入前鼠标指针是否可见，退出时恢复。
        private bool _savedCursorVisible;

        // 进入前鼠标的锁定方式（自由、锁定等），退出时恢复。
        private CursorLockMode _savedCursorLock;

        // 进入前研磨机旋转部件的局部角度，退出时恢复。
        private Quaternion _savedRotorRotation;

        // 本轮是否已处理成功结算，防止重复发放咖啡。
        private bool _completionHandled;

        // 小游戏的整体状态，只有此控制器可以修改。
        public SessionState State { get; private set; }

        // 是否处于小游戏中；进入和退出的镜头过渡期间也算运行中。
        public bool IsRunning => State != SessionState.Inactive;

        // 当前是否允许桌面交互；具体器具是否可用还要检查配方步骤。
        public bool CanOperate => State == SessionState.Playing && Time.timeScale > 0f;

        // 当前手冲步骤；尚未创建配方时返回第一步“加热水”。
        public CoffeeStep CurrentStep => _recipe != null ? _recipe.Step : CoffeeStep.HeatWater;

        // 桌面拾取使用真正渲染画面的相机，不能使用 Cinemachine 虚拟镜头。
        public Camera PointerCamera => _brain != null ? _brain.OutputCamera : null;

        private void Awake()
        {
            _coffeeCamera = GetComponentInChildren<CinemachineCamera>(true);
            if (_coffeeCamera == null)
                Debug.LogError("[CoffeeMinigame]：请在子物体上放置 CinemachineCamera。", this);
            else
                _coffeeCamera.enabled = false;

            if (_brain == null && Camera.main != null)
                _brain = Camera.main.GetComponent<CinemachineBrain>();

            if (_interactor == null)
                Debug.LogError("[CoffeeMinigame] 请拖入 CoffeeDeskInteractor 组件。", this);
        }
        private void OnEnable()
        {
            // 倒计时照常运行。阶段结束或游戏结束时，中止尚未完成的制作。
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }

        private void Start()
        {
            if (!IsRunning && _coffeePanel != null) _coffeePanel.SetActive(false);
            if (!IsRunning && _interactor != null) _interactor.SetInputEnabled(false);
        }

        public void Begin(PlayerBasic player)
        {
            // 先通知，小游戏开始，让其他组件处理
            EventPipeline.Publish<EventPlayMiniGame>(new EventPlayMiniGame(true));
            if (!isActiveAndEnabled || IsRunning || player == null ||
                player.InputManager == null || player.PlayerMovement == null ||
                !ValidateSetup(player)) return;

            _player = player;
            _resources = player.GetComponent<PlayerResources>();
            _savedCursorVisible = Cursor.visible;
            _savedCursorLock = Cursor.lockState;
            if (_grinderRotor != null) _savedRotorRotation = _grinderRotor.localRotation;
            _completionHandled = false;
            _recipe = new CoffeeRecipe(_heatSeconds, _grindSeconds, _groundsSeconds,
                _waterSeconds, _dripSeconds);
            _recipe.StepChanged += HandleStepChanged;
            State = SessionState.Entering;

            // 只锁玩家操作，世界倒计时照常推进。
            _player.PlayerMovement.SetMoveInput(Vector2.zero);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _interactor.SetInputEnabled(false);
            foreach (CoffeeDraggable item in _items) item.ResetItem();
            ResetEffects();
            _groundsCup.gameObject.SetActive(false);
            _coffeePanel.SetActive(true);
            if (_progressBar != null)
            {
                _progressBar.minValue = 0f;
                _progressBar.maxValue = 1f;
                _progressBar.interactable = false;
                _progressBar.SetValueWithoutNotify(0f);
            }
            _hintText.text = "正在移到咖啡桌前……";

            // 玩家镜头保持启用；Brain 自动选择优先级更高的咖啡镜头。
            _coffeeCamera.enabled = true;
            StartCoroutine(WaitForCoffeeCamera());
        }
        public void Exit()
        {
            // 先通知，小游戏结束，让其他组件处理
            EventPipeline.Publish<EventPlayMiniGame>(new EventPlayMiniGame(false));
            if (!IsRunning || State == SessionState.Exiting) return;
            StopAllCoroutines();
            State = SessionState.Exiting;
            _interactor.SetInputEnabled(false);
            _coffeePanel.SetActive(false);
            // 关闭自己的镜头后，Brain 自动选择剩余启用镜头中优先级最高的一个。
            if (_coffeeCamera != null) _coffeeCamera.enabled = false;
            StartCoroutine(WaitForReturnCamera());
        }

        private bool ValidateSetup(PlayerBasic player)
        {
            _items = new[] { _kettle, _beans, _dripper, _filterPaper, _groundsCup };
            if (_coffeeCamera == null || !_coffeeCamera.gameObject.activeInHierarchy ||
                _brain == null || !_brain.isActiveAndEnabled ||
                PointerCamera == null || !PointerCamera.isActiveAndEnabled ||
                _interactor == null || !_interactor.isActiveAndEnabled ||
                _kettleSwitch == null || _grinderButton == null ||
                _grinderZone == null || _cupZone == null || _filterZone == null ||
                _coffeePanel == null || _hintText == null ||
                player.GetComponent<PlayerResources>() == null)
            {
                Debug.LogError("[CoffeeMinigame] 请连接 Brain、镜头、所有器具/区域、面板和提示；主相机和咖啡镜头物体需激活，玩家需要 PlayerResources。", this);
                return false;
            }
            if (transform.IsChildOf(_coffeePanel.transform) ||
                _interactor.transform.IsChildOf(_coffeePanel.transform) ||
                _coffeeCamera.transform.IsChildOf(_coffeePanel.transform))
            {
                Debug.LogError("[CoffeeMinigame] 控制器、交互器和咖啡镜头必须位于 CoffeePanel 外。", this);
                return false;
            }
            foreach (CoffeeDraggable item in _items)
            {
                if (item == null || item.transform.IsChildOf(_coffeePanel.transform))
                {
                    Debug.LogError("[CoffeeMinigame] 五个可拖拽物体都需要配置并放在 UI 面板外。", this);
                    return false;
                }
                Rigidbody body = item.GetComponentInParent<Rigidbody>();
                if (body != null && !body.isKinematic)
                {
                    Debug.LogError("[CoffeeMinigame] 桌面物体的 Rigidbody 应为 Is Kinematic。", item);
                    return false;
                }
            }
            if (_heatSeconds <= 0f || _grindSeconds <= 0f || _groundsSeconds <= 0f ||
                _waterSeconds <= 0f || _dripSeconds <= 0f) return false;
            return true;
        }

        private IEnumerator WaitForCoffeeCamera()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + _cameraTimeout;
            while (_brain != null && _brain.isActiveAndEnabled &&
                _coffeeCamera != null && _coffeeCamera.isActiveAndEnabled &&
                (!ReferenceEquals(_brain.ActiveVirtualCamera, _coffeeCamera) || _brain.IsBlending))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogError("[CoffeeMinigame] 镜头未接管：咖啡镜头 Priority 需高于其他启用的镜头，并检查 Channel、Solo 和 Timeline。", this);
                    Cleanup();
                    yield break;
                }
                yield return null;
            }
            if (_brain == null || !_brain.isActiveAndEnabled ||
                _coffeeCamera == null || !_coffeeCamera.isActiveAndEnabled)
            {
                Cleanup();
                yield break;
            }
            State = SessionState.Playing;
            _interactor.SetInputEnabled(true);
            HandleStepChanged(_recipe.Step);
        }

        public bool TryClick(CoffeeClickable target)
        {
            if (!CanOperate || target != _kettleSwitch || !_recipe.StartHeating()) return false;
            SetVisible(_heatingEffect, true);
            RefreshHint();
            return true;
        }

        public bool CanHold(CoffeeClickable target) => CanOperate &&
            _recipe.Step == CoffeeStep.GrindBeans && target == _grinderButton;

        public void Hold(CoffeeClickable target, float deltaTime)
        {
            if (!CanHold(target) || deltaTime <= 0f) return;
            if (_grinderRotor != null) _grinderRotor.Rotate(Vector3.up, 360f * deltaTime);
            _recipe.Grind(deltaTime);
        }

        public bool CanDrag(CoffeeDraggable item)
        {
            if (!CanOperate || item == null || !item.isActiveAndEnabled ||
                item.IsPlaced || _recipe.IsProcessing) return false;
            switch (_recipe.Step)
            {
                case CoffeeStep.LoadBeans: return item == _beans;
                case CoffeeStep.PlaceDripper: return item == _dripper;
                case CoffeeStep.PlaceFilter: return item == _filterPaper;
                case CoffeeStep.PourGrounds: return item == _groundsCup;
                case CoffeeStep.PourWater: return item == _kettle;
                default: return false;
            }
        }

        // 松手放置：物体和目标区域都正确，才推进对应步骤。
        public bool TryDrop(CoffeeDraggable item)
        {
            if (!CanDrag(item)) return false;
            switch (_recipe.Step)
            {
                case CoffeeStep.LoadBeans:
                    if (!_grinderZone.Contains(item.transform.position)) return false;
                    item.PlaceAt(_grinderZone.SnapPoint);
                    item.gameObject.SetActive(false);
                    SetVisible(_beansInGrinder, true);
                    return _recipe.LoadBeans();
                case CoffeeStep.PlaceDripper:
                    if (!_cupZone.Contains(item.transform.position)) return false;
                    item.PlaceAt(_cupZone.SnapPoint);
                    return _recipe.PlaceDripper();
                case CoffeeStep.PlaceFilter:
                    if (!_filterZone.Contains(item.transform.position)) return false;
                    item.PlaceAt(_filterZone.SnapPoint);
                    return _recipe.PlaceFilter();
                case CoffeeStep.PourGrounds:
                    if (!_filterZone.Contains(item.transform.position)) return false;
                    if (_groundsPourPoint != null)
                        item.transform.SetPositionAndRotation(
                            _groundsPourPoint.position, _groundsPourPoint.rotation);
                    item.SetPouring(true);
                    return _recipe.StartPouringGrounds();
                default:
                    // 水壶松手后归位；已经倒入的水保留进度。
                    return false;
            }
        }

        public void UpdateDraggedItem(CoffeeDraggable item, float deltaTime)
        {
            if (item != _kettle) return;
            bool pouring = CanDrag(item) && deltaTime > 0f &&
                _filterZone.Contains(item.transform.position);
            _kettle.SetPouring(pouring);
            SetVisible(_waterStream, pouring);
            if (pouring) _recipe.PourWater(deltaTime);
        }

        public void StopInteractionEffects()
        {
            if (_kettle != null) _kettle.SetPouring(false);
            SetVisible(_waterStream, false);
        }

        private void HandleStepChanged(CoffeeStep step)
        {
            switch (step)
            {
                case CoffeeStep.LoadBeans:
                    SetVisible(_heatingEffect, false);
                    break;
                case CoffeeStep.PlaceDripper:
                    SetVisible(_beansInGrinder, false);
                    _groundsCup.gameObject.SetActive(true);
                    break;
                case CoffeeStep.PourWater:
                    _groundsCup.ResetItem();
                    _groundsCup.gameObject.SetActive(false);
                    SetVisible(_groundsInFilter, true);
                    break;
                case CoffeeStep.Dripping:
                    _interactor.CancelInteraction();
                    SetVisible(_coffeeLiquid, true);
                    break;
                case CoffeeStep.Complete:
                    CompleteCoffee();
                    return;
            }
            RefreshHint();
        }

        private void Update()
        {
            if (IsRunning && State != SessionState.Exiting && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Exit();
                return;
            }
            if (State != SessionState.Playing) return;

            // 用普通 deltaTime，只推进咖啡自身的自动过程，不写 timeScale 或世界倒计时。
            _recipe.Tick(Time.deltaTime);
            if (State == SessionState.Playing) RefreshHint();
        }

        private void RefreshHint()
        {
            string text;
            switch (_recipe.Step)
            {
                case CoffeeStep.HeatWater:
                    text = _recipe.IsProcessing ? "1/7 正在加热水" : "1/7 点击热水壶开关，加热水";
                    break;
                case CoffeeStep.LoadBeans: text = "2/7 将咖啡豆拖到研磨机中"; break;
                case CoffeeStep.GrindBeans: text = "3/7 在研磨机上按住鼠标左键，磨咖啡豆"; break;
                case CoffeeStep.PlaceDripper: text = "4/7 将滤杯拖到杯子上"; break;
                case CoffeeStep.PlaceFilter: text = "5/7 将滤纸拖到滤杯上"; break;
                case CoffeeStep.PourGrounds:
                    text = _recipe.IsProcessing ? "6/7 正在倒入咖啡粉" : "6/7 将咖啡粉容器拖到滤纸上，松手倒粉";
                    break;
                case CoffeeStep.PourWater: text = "7/7 拖起热水壶，在滤杯上方持续按住注水"; break;
                default: text = "7/7 正在滴滤，完成后自动返回"; break;
            }
            _hintText.text = text;
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(_recipe.Progress);
        }

        private void CompleteCoffee()
        {
            if (_completionHandled || State != SessionState.Playing) return;
            _completionHandled = true;
            PlayerResources receiver = _resources;
            // 先开始关闭，确保奖励事件触发其他界面时不会遗留咖啡输入。
            Exit();
            receiver.AddCoffee();
            _onCoffeeCompleted.Invoke();
        }

        // 仅用于取消（Esc/可选退出按钮）；成功时由 CompleteCoffee 自动调用。


        private IEnumerator WaitForReturnCamera()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + _cameraTimeout;
            while (_brain != null && _brain.isActiveAndEnabled &&
                (_brain.IsBlending || ReferenceEquals(_brain.ActiveVirtualCamera, _coffeeCamera)) &&
                Time.realtimeSinceStartup < deadline)
                yield return null;
            Cleanup();
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt) => Abort();
        private void HandleGameOver(EventGameOver evt) => Abort();

        private void Abort()
        {
            if (!IsRunning) return;
            StopAllCoroutines();
            // 阶段切换要马上交还控制权，不等返回镜头动画，也不发放奖励。
            Cleanup();
        }

        private void Cleanup()
        {
            if (!IsRunning) return;
            State = SessionState.Inactive;
            if (_recipe != null) _recipe.StepChanged -= HandleStepChanged;
            if (_interactor != null) _interactor.SetInputEnabled(false);
            // 完成、取消、切换阶段、禁用脚本或进入超时，都必须释放咖啡镜头。
            if (_coffeeCamera != null) _coffeeCamera.enabled = false;
            if (_items != null)
                foreach (CoffeeDraggable item in _items)
                    if (item != null) item.ResetItem();
            ResetEffects();
            if (_grinderRotor != null) _grinderRotor.localRotation = _savedRotorRotation;
            if (_coffeePanel != null) _coffeePanel.SetActive(false);
            if (_player != null)
            {
                if (_player.PlayerMovement != null) _player.PlayerMovement.SetMoveInput(Vector2.zero);
            }
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            _player = null;
            _resources = null;
            _recipe = null;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null) target.SetActive(visible);
        }

        private void ResetEffects()
        {
            StopInteractionEffects();
            SetVisible(_heatingEffect, false);
            SetVisible(_beansInGrinder, false);
            SetVisible(_groundsInFilter, false);
            SetVisible(_coffeeLiquid, false);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            Abort();
        }
    }
}
