using TapTapGameJam.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public static bool AnimationsEnabled => PlayerPrefs.GetInt("Settings.Animations", 1) != 0;

    private void Awake()
    {
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
        shortcutsTab.image.color = sound ? Color.gray : new Color(.3f, .4f, .75f);
        soundTab.image.color = sound ? new Color(.3f, .4f, .75f) : Color.gray;
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
