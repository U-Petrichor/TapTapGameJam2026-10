using UnityEngine;
using TapTapGameJam.Events;

public class ESCMenu : MonoBehaviour
{
    public static ESCMenu Instance { get; private set; }
    private bool _isMenuOpen = false;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }
        Instance = this;
        CloseESCMenu();
    }
    private void OnEnable()
    {
        EventPipeline.Subscribe<ESCMenuEvent>(HandleESCMenuEvent);
    }
    private void OnDisable()
    {
        EventPipeline.UnSubscribe<ESCMenuEvent>(HandleESCMenuEvent);
    }
    private void HandleESCMenuEvent(ESCMenuEvent evt)
    {
        if(_isMenuOpen)
        {
            CloseESCMenu();
            _isMenuOpen = false;
        }
        else
        {
            OpenESCMenu();
            _isMenuOpen = true;
        }
    }
    public void OpenESCMenu()
    {
        foreach (Transform child in transform)
        {
            // 选择激活每一个子物体，否则关闭Canvas_Settings_Menu之后无法再次打开
            child.gameObject.SetActive(true);
        }
    }
    public void CloseESCMenu()
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }
}
