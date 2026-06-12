using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// Wires Fighter events to the in-game HUD.
    /// Assign UI elements in Inspector.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Fighter 1 HUD")]
        public Slider  f1HealthSlider;
        public Slider  f1StaminaSlider;
        public Image   f1AwakenGlow;   // enable when awakened
        public TMP_Text f1NameText;
        public TMP_Text f1ScoreText;

        [Header("Fighter 2 HUD")]
        public Slider  f2HealthSlider;
        public Slider  f2StaminaSlider;
        public Image   f2AwakenGlow;
        public TMP_Text f2NameText;
        public TMP_Text f2ScoreText;

        [Header("Centre HUD")]
        public TMP_Text timerText;
        public TMP_Text roundText;

        [Header("Overlay panels")]
        public GameObject roundStartPanel;   // shows "ROUND X  FIGHT!"
        public TMP_Text   roundStartLabel;
        public GameObject koPanel;           // shows "KO!"
        public GameObject matchEndPanel;
        public TMP_Text   matchEndLabel;

        [Header("Story Mode")]
        public GameObject backstoryPanel;
        public TMP_Text   backstoryText;

        MatchManager _match;

        void Start()
        {
            _match = MatchManager.Instance;
            if (_match == null) return;

            // Init names
            f1NameText.text = _match.fighter1.data.fighterName;
            f2NameText.text = _match.fighter2.data.fighterName;

            // Subscribe fighter events
            _match.fighter1.OnHealthChanged  += (hp, max) => SetSlider(f1HealthSlider,  hp, max);
            _match.fighter1.OnStaminaChanged += (sp, max) => SetSlider(f1StaminaSlider, sp, max);
            _match.fighter1.OnAwakened       += ()        => ToggleGlow(f1AwakenGlow, true);

            _match.fighter2.OnHealthChanged  += (hp, max) => SetSlider(f2HealthSlider,  hp, max);
            _match.fighter2.OnStaminaChanged += (sp, max) => SetSlider(f2StaminaSlider, sp, max);
            _match.fighter2.OnAwakened       += ()        => ToggleGlow(f2AwakenGlow, true);

            // Subscribe match events
            _match.OnTimerUpdate += t => timerText.text = Mathf.CeilToInt(t).ToString();
            _match.OnRoundStart  += ShowRoundStart;
            _match.OnRoundEnd    += UpdateScores;
            _match.OnMatchEnd    += ShowMatchEnd;

            // Init sliders
            SetSlider(f1HealthSlider,  _match.fighter1.data.maxHealth,  _match.fighter1.data.maxHealth);
            SetSlider(f1StaminaSlider, _match.fighter1.data.maxStamina, _match.fighter1.data.maxStamina);
            SetSlider(f2HealthSlider,  _match.fighter2.data.maxHealth,  _match.fighter2.data.maxHealth);
            SetSlider(f2StaminaSlider, _match.fighter2.data.maxStamina, _match.fighter2.data.maxStamina);

            HideAll();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        void SetSlider(Slider slider, float val, float max)
        {
            if (slider == null) return;
            slider.maxValue = max;
            slider.value    = val;
        }

        void ToggleGlow(Image glow, bool on)
        {
            if (glow != null) glow.enabled = on;
        }

        void ShowRoundStart(int round)
        {
            roundText.text = $"ROUND {round}";
            roundStartLabel.text = $"ROUND {round}\nFIGHT!";
            roundStartPanel.SetActive(true);
            Invoke(nameof(HideRoundStart), 2f);
        }

        void HideRoundStart() => roundStartPanel.SetActive(false);

        void UpdateScores(Characters.Fighter winner, int s1, int s2)
        {
            f1ScoreText.text = s1.ToString();
            f2ScoreText.text = s2.ToString();
            koPanel.SetActive(true);
            Invoke(nameof(HideKO), 2f);
        }

        void HideKO() => koPanel.SetActive(false);

        void ShowMatchEnd(Characters.Fighter winner)
        {
            matchEndLabel.text = $"{winner.data.fighterName} WINS!";
            matchEndPanel.SetActive(true);

            // Story mode: show backstory
            if (_match.mode == GameMode.Story && backstoryPanel != null)
            {
                backstoryText.text = _match.GetOpponentBackstory(winner);
                backstoryPanel.SetActive(true);
            }
        }

        void HideAll()
        {
            roundStartPanel?.SetActive(false);
            koPanel?.SetActive(false);
            matchEndPanel?.SetActive(false);
            backstoryPanel?.SetActive(false);
        }
    }
}
