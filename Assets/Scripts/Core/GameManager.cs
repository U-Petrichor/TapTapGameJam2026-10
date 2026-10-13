using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TapTapGameJam.UI;
using UnityEngine;

namespace TapTapGameJam.Core
{

    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        [SerializeField] private PlayerBasic _player;
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private ShopMenu _shopMenu;
        [SerializeField] private GamePhaseManager _gamePhaseManager;
        [Tooltip("背包界面（HUDCanvas 下的 BagPanel）")]
        [SerializeField] private BagPanelUI _bagPanel;
        private int _dayCount=0 ;
        
        public static bool IsDay => _instance._gamePhaseManager.CurrentPhase == GamePhase.Day;
        public static bool IsNight => _instance._gamePhaseManager.CurrentPhase == GamePhase.Night;
        public static PlayerBasic Player => _instance._player;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }

            if (_bagPanel == null)
            {
                Debug.LogError("[GameManager]：没有配置 BagPanel，按 B 打开背包会没有反应", this);
            }
        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandleGamePhaseChanged);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandleGamePhaseChanged);
        }
        void Start()
        {
            Debug.Log($"游戏要开始了");

            // 背包界面开局先收起来，等 BackpackEvent 再打开
            // （PlayerBag 引用由 BagPanelUI 在 Inspector 里直接拖好，这里不做运行时接线）
            if (_bagPanel != null)
            {
                _bagPanel.SetVisible(false);
            }

            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        }

        private void HandleGamePhaseChanged(GamePhaseChangedEvent evt)
        {
            switch (evt.NewPhase)
            {
                case GamePhase.Day:
                    {
                        _gamePhaseManager.CouldWork = true;
                        EventPipeline.Publish<EventNewDay>(new EventNewDay(_dayCount++));
                        break;
                    }
                case GamePhase.Shopping:
                    {
                        _gamePhaseManager.CouldWork = false;
                        _shopMenu.OpenShopMenu();
                        break;
                    }
                case GamePhase.Night:
                    {
                        _gamePhaseManager.CouldWork = true;
                        _shopMenu.CloseShopMenu();
                        break;
                    }
                default:
                    {
                        Debug.LogError($"[GameManager]：未知的游戏阶段{evt.NewPhase}");
                        break;
                    }
            }
        }
    }

}
