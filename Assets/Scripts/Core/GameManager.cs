using System;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.UI;
using TapTapGameJam.Types;
using UnityEngine;
using System.ComponentModel.Design.Serialization;

namespace TapTapGameJam.Core
{

    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        [SerializeField] private PlayerBasic _player;
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private ShopMenu _shopMenu;
        [SerializeField] private GamePhaseManager _gamePhaseManager;

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
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        }

        // Update is called once per frame
        void Update()
        {

        }
        private void HandleGamePhaseChanged(GamePhaseChangedEvent evt)
        {
            switch (evt.NewPhase)
            {
                case GamePhase.Day:
                    {
                        _gamePhaseManager.EnterPhase(GamePhase.Day);
                        _gamePhaseManager.CouldWork = true;
                        break;
                    }
                case GamePhase.Shopping:
                    {
                        _gamePhaseManager.EnterPhase(GamePhase.Shopping);
                        _gamePhaseManager.CouldWork = false;
                        _shopMenu.OpenShopMenu();
                        break;
                    }
                case GamePhase.Night:
                    {

                        _gamePhaseManager.EnterPhase(GamePhase.Night);
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