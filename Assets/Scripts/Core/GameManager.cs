using TapTapGameJam.Events;
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
        [SerializeField] private GamePhaseManager _gamePhaseManager;
        
        public static bool IsDay => _instance != null && _instance._gamePhaseManager.CurrentPhase == GamePhase.Day;
        public static bool IsNight => _instance != null && _instance._gamePhaseManager.CurrentPhase == GamePhase.Night;
        public static PlayerBasic Player => _instance != null ? _instance._player : null;
        public static PlayerInputManager PlayerInputManager => _instance != null ? _instance._playerInputManager : null;

        /// <summary>场景里是否有 GameManager。守卫这类"只在夜晚工作"的组件靠它判断能不能开工</summary>
        public static bool HasInstance => _instance != null;

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
    }

}
