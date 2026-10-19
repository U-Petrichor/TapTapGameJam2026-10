using TapTapGameJam.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Button shortcutsTab;
    [SerializeField] private Button soundTab;
    [SerializeField] private GameObject shortcutsPage;
    [SerializeField] private GameObject soundPage;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Button animationButton;
    [SerializeField] private TMP_Text animationLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private Sprite selectedTabSprite;
    [SerializeField] private Sprite inactiveTabSprite;
    [SerializeField] private RectTransform selectedPin;
    private Action closeRequested;
    private static SettingsMenu gameInstance;
    public static bool IsGameSettingsOpen => gameInstance != null && gameInstance.gameObject.activeSelf;
    public Button BackButton => backButton;

    public static void OpenInGame(Action onClosed = null)
    {
        if (gameInstance == null)
        {
            var prefab = Resources.Load<GameObject>("UI/SettingsMenu");
            if (prefab == null) { Debug.LogError("Missing UI/SettingsMenu prefab."); return; }
            gameInstance = Instantiate(prefab).GetComponent<SettingsMenu>();
            gameInstance.GetComponent<Canvas>().sortingOrder = 200;
        }
        gameInstance.closeRequested = () => { gameInstance.gameObject.SetActive(false); onClosed?.Invoke(); };
        gameInstance.gameObject.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(gameInstance.backButton.gameObject);
    }

    public static bool CloseGameSettings()
    {
        if (!IsGameSettingsOpen) return false;
        gameInstance.closeRequested?.Invoke();
        return true;
    }

    public static bool AnimationsEnabled => PlayerPrefs.GetInt("Settings.Animations", 1) != 0;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(() => closeRequested?.Invoke());
        shortcutsTab.onClick.AddListener(() => ShowPage(false));
        soundTab.onClick.AddListener(() => ShowPage(true));
        effectsSlider.onValueChanged.AddListener(SetEffects);
        musicSlider.onValueChanged.AddListener(SetMusic);
        animationButton.onClick.AddListener(ToggleAnimations);
    }

    private void OnEnable()
    {
        var audio = SoundManager.Instance;
        effectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Settings.Effects", audio != null ? audio.EffectsVolume : 1f));
        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Settings.Music", audio != null ? audio.MusicVolume : 1f));
        if (audio != null) { audio.EffectsVolume = effectsSlider.value; audio.MusicVolume = musicSlider.value; }
        animationLabel.text = AnimationsEnabled ? "ON" : "OFF";
        ShowPage(false);
    }

    private void OnDisable() => PlayerPrefs.Save();
    private void ShowPage(bool sound)
    {
        shortcutsPage.SetActive(!sound);
        soundPage.SetActive(sound);
        if (selectedTabSprite != null && inactiveTabSprite != null)
        {
            shortcutsTab.image.sprite = sound ? inactiveTabSprite : selectedTabSprite;
            soundTab.image.sprite = sound ? selectedTabSprite : inactiveTabSprite;
            shortcutsTab.image.color = soundTab.image.color = Color.white;
        }
        else
        {
            shortcutsTab.image.color = sound ? Color.gray : new Color(.3f, .4f, .75f);
            soundTab.image.color = sound ? new Color(.3f, .4f, .75f) : Color.gray;
        }
        if (selectedPin != null) selectedPin.anchoredPosition = new Vector2(170, sound ? -407 : -285);
    }
    private void SetEffects(float value)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.EffectsVolume = value;
        PlayerPrefs.SetFloat("Settings.Effects", value);
    }
    private void SetMusic(float value)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.MusicVolume = value;
        PlayerPrefs.SetFloat("Settings.Music", value);
    }
    private void ToggleAnimations()
    {
        PlayerPrefs.SetInt("Settings.Animations", AnimationsEnabled ? 0 : 1);
        animationLabel.text = AnimationsEnabled ? "ON" : "OFF";
    }
}
