using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>Settings window. Keep this component on the always-active canvas.</summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }
    private static int inputBlockedThroughFrame = -1;
    public static bool BlocksGameplay => TargetRangeGame.IsOpen || SceneTravel.IsTravelling || LocalGameSession.BlocksGameplay || (Instance != null && Instance.IsOpen)
        || Time.frameCount <= inputBlockedThroughFrame;
    public bool IsOpen { get; private set; }

    [Header("Window")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Optional. Layouts without an X button can use Resume or Escape to close.")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button defaultsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private TMP_Text fullscreenLabel;

    [Header("Audio")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text sfxValue;
    [Tooltip("Optional: assign when music and sound effects are routed through an AudioMixer.")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string musicParameter = "MusicVolume";
    [SerializeField] private string sfxParameter = "SFXVolume";

    [Header("Controls and confirmation")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text confirmationTitle;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private const string Key = "RPG.Settings.";
    private enum Confirmation { None, Defaults, Quit, QuitWithoutSave }
    private Confirmation confirmation;
    private float previousTimeScale;
    private bool fullscreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        inputBlockedThroughFrame = -1;
    }

    private void Awake()
    {
        Instance = this;
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);
        confirmationPanel.SetActive(false);
        masterSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(Key + "Master", 1f));
        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(Key + "Music", 0.7f));
        sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(Key + "SFX", 0.8f));
        fullscreen = PlayerPrefs.GetInt(Key + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
        ApplyFullscreen();
    }

    // Mixer parameters are applied after audio initialization, not in Awake.
    private void Start() => ApplyAudio();

    private void OnEnable()
    {
        Instance = this;
        settingsButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        resumeButton.onClick.AddListener(Close);
        quitButton.onClick.AddListener(AskToQuit);
        defaultsButton.onClick.AddListener(AskToReset);
        controlsButton.onClick.AddListener(ShowControls);
        controlsBackButton.onClick.AddListener(HideControls);
        fullscreenButton.onClick.AddListener(ToggleFullscreen);
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(CancelConfirmation);
        masterSlider.onValueChanged.AddListener(OnAudioChanged);
        musicSlider.onValueChanged.AddListener(OnAudioChanged);
        sfxSlider.onValueChanged.AddListener(OnAudioChanged);
    }

    private void OnDisable()
    {
        settingsButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        resumeButton.onClick.RemoveListener(Close);
        quitButton.onClick.RemoveListener(AskToQuit);
        defaultsButton.onClick.RemoveListener(AskToReset);
        controlsButton.onClick.RemoveListener(ShowControls);
        controlsBackButton.onClick.RemoveListener(HideControls);
        fullscreenButton.onClick.RemoveListener(ToggleFullscreen);
        confirmButton.onClick.RemoveListener(Confirm);
        cancelButton.onClick.RemoveListener(CancelConfirmation);
        masterSlider.onValueChanged.RemoveListener(OnAudioChanged);
        musicSlider.onValueChanged.RemoveListener(OnAudioChanged);
        sfxSlider.onValueChanged.RemoveListener(OnAudioChanged);
        if (IsOpen) Close();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;
        inputBlockedThroughFrame = Time.frameCount;
        if (confirmationPanel.activeSelf) CancelConfirmation();
        else if (controlsPanel.activeSelf) HideControls();
        else Close();
    }

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (IsOpen || TargetRangeGame.IsOpen || LocalGameSession.BlocksGameplay || SceneTravel.IsTravelling) return;
        Instance = this;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsOpen = true;
        GameMenuUI menu = FindObjectOfType<GameMenuUI>();
        if (menu != null) { menu.ClosePanels(); menu.CloseMenu(); }
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    public void Close()
    {
        if (!IsOpen) return;
        inputBlockedThroughFrame = Time.frameCount;
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        controlsPanel.SetActive(false);
        CancelConfirmation();
        settingsPanel.SetActive(false);
        SaveSettings();
    }

    private void OnAudioChanged(float unused)
    {
        ApplyAudio();
        // Update preferences now; flush on close rather than on every drag.
        StorePreferences();
    }

    private void ApplyAudio()
    {
        AudioListener.volume = masterSlider.value;
        GameAudio.SetVolumes(masterSlider.value, musicSlider.value, sfxSlider.value);
        if (audioMixer != null)
        {
            audioMixer.SetFloat(musicParameter, ToDecibels(musicSlider.value));
            audioMixer.SetFloat(sfxParameter, ToDecibels(sfxSlider.value));
        }
        masterValue.text = Mathf.RoundToInt(masterSlider.value * 100f) + "%";
        musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
    }

    private static float ToDecibels(float value) => value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

    private void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        ApplyFullscreen();
        StorePreferences();
    }

    private void ApplyFullscreen()
    {
        if (!Application.isEditor) Screen.fullScreen = fullscreen;
        fullscreenLabel.text = fullscreen ? "ON" : "OFF";
    }

    private void ShowControls()
    {
        StatsManager stats = StatsManager.GetOrCreate();
        string attack = stats.playerAttackKey.ToString();
        if (stats.allowLeftMouseAttack) attack += " / Left click";
        controlsText.text = "<b>MOVE</b>     WASD / Arrow keys\n\n"
            + "<b>ATTACK</b>     " + attack + "\n\n"
            + "<b>INTERACT</b>     E\n\n"
            + "<b>DIALOGUE</b>     E / Space / Click\n\n"
            + "<b>CLOSE PANEL</b>     Esc";
        controlsPanel.SetActive(true);
    }

    private void HideControls() => controlsPanel.SetActive(false);
    private void AskToReset() => Ask(Confirmation.Defaults, "RESTORE DEFAULTS?", "Reset sound and display settings?\nYour game progress will not change.");
    private void AskToQuit() => Ask(Confirmation.Quit, "QUIT GAME?", "Leave the game now?\nUnsaved progress may be lost.");

    private void Ask(Confirmation action, string title, string message)
    {
        confirmation = action;
        confirmationTitle.text = title;
        confirmationText.text = message;
        confirmationPanel.SetActive(true);
    }

    private void CancelConfirmation()
    {
        confirmation = Confirmation.None;
        confirmationPanel.SetActive(false);
    }

    private void Confirm()
    {
        Confirmation action = confirmation;
        CancelConfirmation();
        if (action == Confirmation.Defaults)
        {
            masterSlider.SetValueWithoutNotify(1f);
            musicSlider.SetValueWithoutNotify(0.7f);
            sfxSlider.SetValueWithoutNotify(0.8f);
            fullscreen = true;
            ApplyAudio();
            ApplyFullscreen();
            SaveSettings();
        }
        else if (action == Confirmation.Quit || action == Confirmation.QuitWithoutSave)
        {
            LocalGameSession session = LocalGameSession.Instance;
            if (action == Confirmation.Quit && session != null && session.CanSave && !session.TrySave())
            {
                Ask(Confirmation.QuitWithoutSave, "QUIT WITHOUT SAVING?", "Saving failed. The previous save is preserved.\nQuit and lose progress since the last save?");
                return;
            }
            if (session != null) session.StopSavingForExit();
            Close();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void StorePreferences()
    {
        PlayerPrefs.SetFloat(Key + "Master", masterSlider.value);
        PlayerPrefs.SetFloat(Key + "Music", musicSlider.value);
        PlayerPrefs.SetFloat(Key + "SFX", sfxSlider.value);
        PlayerPrefs.SetInt(Key + "Fullscreen", fullscreen ? 1 : 0);
    }

    private void SaveSettings() { StorePreferences(); PlayerPrefs.Save(); }
    private void OnApplicationPause(bool paused) { if (paused) SaveSettings(); }
}
