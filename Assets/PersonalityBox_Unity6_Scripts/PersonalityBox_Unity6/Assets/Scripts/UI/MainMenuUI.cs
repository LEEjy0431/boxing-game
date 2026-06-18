using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// BoxBox 로비 전체 UI 관리.
    ///
    /// 패널 구조
    ///   lobbyPanel      ── 로고 + 4개 버튼 (게임시작/설정/커맨드/나가기)
    ///   modePanel       ── 게임 모드 선택 (스토리/AI/2P)
    ///   charSelectPanel ── 캐릭터 선택
    ///   settingsPanel   ── 오디오·화면 설정
    ///   commandsPanel   ── 조작 커맨드 설명
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        // ── 패널 ─────────────────────────────────────────────────────────────
        [Header("Panels")]
        public GameObject lobbyPanel;
        public GameObject modePanel;
        public GameObject charSelectPanel;
        public GameObject settingsPanel;
        public GameObject commandsPanel;

        // ── 로비 로고 ─────────────────────────────────────────────────────────
        [Header("Lobby – Logo")]
        public Text logoText;

        // ── 캐릭터 선택 ───────────────────────────────────────────────────────
        [Header("Character Select")]
        public FighterData[] availableFighters;
        public Text p1SelectLabel;
        public Text p2SelectLabel;

        // ── 설정 패널 ─────────────────────────────────────────────────────────
        [Header("Settings – Audio")]
        public Slider bgmSlider;
        public Slider sfxSlider;
        public Text   bgmValueLabel;
        public Text   sfxValueLabel;

        [Header("Settings – Display")]
        public Dropdown resolutionDropdown;
        public Toggle   fullscreenToggle;

        // ── 내부 상태 ─────────────────────────────────────────────────────────
        int _p1Choice = 0;
        int _p2Choice = 1;

        // ═════════════════════════════════════════════════════════════════════
        void Start()
        {
            if (logoText != null) logoText.text = "BoxBox";

            InitSettings();
            ShowLobby();
            // BGM은 SoundManager.Start()에서 자동 시작
        }

        // ══ 로비 버튼 핸들러 ═════════════════════════════════════════════════

        public void OnClickStart()
        {
            PlayClick();
            if (GameSettings.Instance != null)
                GameSettings.Instance.mode = GameMode.VsAI;
            StartCoroutine(StartFightWithBell());
        }

        public void OnClickSettings()
        {
            PlayClick();
            ShowPanel(settingsPanel);
        }

        public void OnClickCommands()
        {
            PlayClick();
            ShowPanel(commandsPanel);
        }

        public void OnClickQuit()
        {
            PlayClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ══ 뒤로가기 ════════════════════════════════════════════════════════

        public void OnClickBack()
        {
            PlayClick();
            ShowLobby();
        }

        // ══ 모드 선택 ════════════════════════════════════════════════════════

        public void SelectMode(int modeIndex)
        {
            PlayClick();
            if (GameSettings.Instance != null)
                GameSettings.Instance.mode = (GameMode)modeIndex;
            ShowPanel(charSelectPanel);
        }

        // ══ 캐릭터 선택 ══════════════════════════════════════════════════════

        public void P1ChooseFighter(int index)
        {
            PlayClick();
            _p1Choice = index;
            if (p1SelectLabel != null && availableFighters != null && index < availableFighters.Length)
                p1SelectLabel.text = availableFighters[index].fighterName;
        }

        public void P2ChooseFighter(int index)
        {
            PlayClick();
            _p2Choice = index;
            if (p2SelectLabel != null && availableFighters != null && index < availableFighters.Length)
                p2SelectLabel.text = availableFighters[index].fighterName;
        }

        /// 싸우기 버튼 → 종소리 → 씬 로드
        public void StartFight()
        {
            PlayClick();
            if (availableFighters != null && availableFighters.Length > 0 && GameSettings.Instance != null)
            {
                GameSettings.Instance.fighter1Data = availableFighters[_p1Choice];
                GameSettings.Instance.fighter2Data = availableFighters[_p2Choice];
            }
            StartCoroutine(StartFightWithBell());
        }

        IEnumerator StartFightWithBell()
        {
            SoundManager.Instance?.PlayBell();
            yield return new WaitForSeconds(1.2f);   // 종소리가 울리는 동안 대기
            SceneManager.LoadScene("BoxingGame");
        }

        // ══ 설정 ════════════════════════════════════════════════════════════

        void InitSettings()
        {
            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value    = GameSettings.Instance != null ? GameSettings.Instance.bgmVolume : 0.5f;
                bgmSlider.onValueChanged.AddListener(OnBGMChanged);
                UpdateBGMLabel(bgmSlider.value);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.value    = GameSettings.Instance != null ? GameSettings.Instance.sfxVolume : 1f;
                sfxSlider.onValueChanged.AddListener(OnSFXChanged);
                UpdateSFXLabel(sfxSlider.value);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                Resolution[] resolutions = Screen.resolutions;
                var options = new List<string>();
                int currentIndex = 0;
                for (int i = 0; i < resolutions.Length; i++)
                {
                    options.Add($"{resolutions[i].width} x {resolutions[i].height}");
                    if (resolutions[i].width  == Screen.currentResolution.width &&
                        resolutions[i].height == Screen.currentResolution.height)
                        currentIndex = i;
                }
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = currentIndex;
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            }
        }

        public void OnBGMChanged(float v)
        {
            SoundManager.Instance?.SetBGMVolume(v);
            UpdateBGMLabel(v);
        }

        public void OnSFXChanged(float v)
        {
            SoundManager.Instance?.SetSFXVolume(v);
            UpdateSFXLabel(v);
        }

        void UpdateBGMLabel(float v)
        {
            if (bgmValueLabel != null) bgmValueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        void UpdateSFXLabel(float v)
        {
            if (sfxValueLabel != null) sfxValueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        void OnResolutionChanged(int index)
        {
            if (GameSettings.Instance != null) GameSettings.Instance.resolutionIndex = index;
            Resolution[] res = Screen.resolutions;
            if (index >= 0 && index < res.Length)
                Screen.SetResolution(res[index].width, res[index].height, Screen.fullScreen);
        }

        void OnFullscreenChanged(bool value)
        {
            Screen.fullScreen = value;
            if (GameSettings.Instance != null) GameSettings.Instance.fullscreen = value;
        }

        // ══ 유틸리티 ════════════════════════════════════════════════════════

        void ShowLobby() => ShowPanel(lobbyPanel);

        void ShowPanel(GameObject target)
        {
            lobbyPanel?.SetActive(false);
            modePanel?.SetActive(false);
            charSelectPanel?.SetActive(false);
            settingsPanel?.SetActive(false);
            commandsPanel?.SetActive(false);
            target?.SetActive(true);
        }

        void PlayClick() => SoundManager.Instance?.PlayUIClick();

        // ── 하위 호환 (기존 씬에서 ShowModeSelect / QuitGame을 쓰는 경우) ──
        public void ShowModeSelect() => OnClickStart();
        public void QuitGame()       => OnClickQuit();
    }
}
