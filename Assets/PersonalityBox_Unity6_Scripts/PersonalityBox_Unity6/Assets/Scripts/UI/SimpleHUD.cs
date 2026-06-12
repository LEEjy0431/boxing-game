using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// 인게임 HUD. 체력바, 스태미너바, 타이머 표시.
    /// ghostHealthBar1/2 를 Inspector 에서 연결하면 지연 체력 감소 연출(Street Fighter 스타일)이 활성화됨.
    /// </summary>
    public class SimpleHUD : MonoBehaviour
    {
        [Header("Fighter 1 (왼쪽)")]
        public Slider healthBar1;
        public Slider ghostHealthBar1;   // 지연 감소 바 (선택) — 체력바 뒤에 배치
        public Slider staminaBar1;
        public Image  awakenEffect1;

        [Header("Fighter 2 (오른쪽)")]
        public Slider healthBar2;
        public Slider ghostHealthBar2;
        public Slider staminaBar2;
        public Image  awakenEffect2;

        [Header("중앙")]
        public Text timerText;
        public Text roundText;

        [Header("Ghost Bar 설정")]
        [Tooltip("ghost bar 가 실제 체력을 따라잡는 속도 (높을수록 빠름)")]
        public float ghostLerpSpeed = 3f;

        float _ghostHp1, _ghostHp2;
        float _maxHp1,   _maxHp2;

        void Start()
        {
            var mm = MatchManager.Instance;
            if (mm == null) { Debug.LogWarning("SimpleHUD: MatchManager 없음"); return; }

            var f1 = mm.fighter1;
            var f2 = mm.fighter2;
            if (f1 == null || f2 == null) { Debug.LogWarning("SimpleHUD: 파이터 참조 없음"); return; }

            _maxHp1 = f1.data != null ? f1.data.maxHealth  : 100f;
            _maxHp2 = f2.data != null ? f2.data.maxHealth  : 100f;
            _ghostHp1 = _maxHp1;
            _ghostHp2 = _maxHp2;

            Setup(healthBar1,      _maxHp1);
            Setup(ghostHealthBar1, _maxHp1);
            Setup(staminaBar1,     f1.data != null ? f1.data.maxStamina : 100f);
            Setup(healthBar2,      _maxHp2);
            Setup(ghostHealthBar2, _maxHp2);
            Setup(staminaBar2,     f2.data != null ? f2.data.maxStamina : 100f);

            if (awakenEffect1) awakenEffect1.enabled = false;
            if (awakenEffect2) awakenEffect2.enabled = false;

            f1.OnHealthChanged  += (v, m) => { Set(healthBar1,  v, m); _maxHp1 = m; };
            f1.OnStaminaChanged += (v, m) => Set(staminaBar1, v, m);
            f1.OnAwakened       += ()     => Awaken(awakenEffect1);

            f2.OnHealthChanged  += (v, m) => { Set(healthBar2,  v, m); _maxHp2 = m; };
            f2.OnStaminaChanged += (v, m) => Set(staminaBar2, v, m);
            f2.OnAwakened       += ()     => Awaken(awakenEffect2);

            mm.OnTimerUpdate += t => { if (timerText) timerText.text = Mathf.CeilToInt(t).ToString(); };
            mm.OnRoundStart  += r => { if (roundText)  roundText.text  = $"ROUND {r}"; };
        }

        void Update()
        {
            // ghost bar 는 실제 체력보다 느리게 따라 내려감
            if (healthBar1 != null && ghostHealthBar1 != null)
            {
                _ghostHp1 = Mathf.Lerp(_ghostHp1, healthBar1.value, ghostLerpSpeed * Time.deltaTime);
                ghostHealthBar1.maxValue = _maxHp1;
                ghostHealthBar1.value    = _ghostHp1;
            }

            if (healthBar2 != null && ghostHealthBar2 != null)
            {
                _ghostHp2 = Mathf.Lerp(_ghostHp2, healthBar2.value, ghostLerpSpeed * Time.deltaTime);
                ghostHealthBar2.maxValue = _maxHp2;
                ghostHealthBar2.value    = _ghostHp2;
            }
        }

        void Setup(Slider bar, float max)
        {
            if (!bar) return;
            bar.minValue = 0;
            bar.maxValue = max;
            bar.value    = max;
        }

        void Set(Slider bar, float val, float max)
        {
            if (!bar) return;
            bar.maxValue = max;
            bar.value    = Mathf.Clamp(val, 0, max);
        }

        void Awaken(Image effect)
        {
            if (!effect) return;
            effect.enabled = true;
            effect.color   = new Color(1f, 0.55f, 0f, 0.85f);
        }
    }
}
