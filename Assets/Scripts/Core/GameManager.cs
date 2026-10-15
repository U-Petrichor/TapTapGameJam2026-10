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
        
        public static bool IsDay => _instance._gamePhaseManager.CurrentPhase == GamePhase.Day;
        public static bool IsNight => _instance._gamePhaseManager.CurrentPhase == GamePhase.Night;
        public static PlayerBasic Player => _instance._player;
        public static PlayerInputManager PlayerInputManager => _instance._playerInputManager;

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
