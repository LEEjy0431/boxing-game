using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    public class BoxingHUD : MonoBehaviour
    {
        [Header("파이터 (비워두면 자동)")]
        public Fighter fighter1;
        public Fighter fighter2;

        [Header("UI 요소")]
        public Image healthBar1;
        public Image staminaBar1;
        public Text  nameText1;
        public Image healthBar2;
        public Image staminaBar2;
        public Text  nameText2;
        public Text  timerText;
        public Text  roundText;

        bool _timerHooked;

        void Start()
        {
            if (healthBar1 == null) CreateUI();
        }

        void Update()
        {
            // 매 프레임 파이터 탐색 시도
            if (fighter1 == null) FindFighter(ref fighter1, "Fighter1_Robert");
            if (fighter2 == null) FindFighter(ref fighter2, "Fighter2_Engie");

            // 타이머/라운드 이벤트 한 번만 연결
            if (!_timerHooked && fighter1 != null && fighter2 != null)
            {
                var mm = MatchManager.Instance;
                if (mm != null)
                {
                    mm.OnTimerUpdate += t => { if (timerText) timerText.text = Mathf.CeilToInt(t).ToString("00"); };
                    mm.OnRoundStart  += r => { if (roundText)  roundText.text  = $"ROUND {r}"; };
                    _timerHooked = true;
                }
                if (nameText1) nameText1.text = (fighter1.data != null && fighter1.data.fighterName.Length > 0)
                    ? fighter1.data.fighterName : "PLAYER";
                if (nameText2) nameText2.text = (fighter2.data != null && fighter2.data.fighterName.Length > 0)
                    ? fighter2.data.fighterName : "ENEMY";
            }

            if (fighter1 == null || fighter2 == null) return;

            float mh1 = fighter1.data != null ? fighter1.data.maxHealth  : 100f;
            float mh2 = fighter2.data != null ? fighter2.data.maxHealth  : 100f;
            float ms1 = fighter1.data != null ? fighter1.data.maxStamina : 100f;
            float ms2 = fighter2.data != null ? fighter2.data.maxStamina : 100f;

            SetFill(healthBar1,  fighter1.CurrentHP      / mh1);
            SetFill(staminaBar1, fighter1.CurrentStamina / ms1);
            SetFill(healthBar2,  fighter2.CurrentHP      / mh2);
            SetFill(staminaBar2, fighter2.CurrentStamina / ms2);
        }

        static void FindFighter(ref Fighter slot, string goName)
        {
            // 1) MatchManager
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                if (goName.Contains("Robert") && mm.fighter1 != null) { slot = mm.fighter1; return; }
                if (goName.Contains("Engie")  && mm.fighter2 != null) { slot = mm.fighter2; return; }
            }
            // 2) 이름으로 직접 탐색
            var go = GameObject.Find(goName);
            if (go != null) slot = go.GetComponent<Fighter>();
        }

        void SetFill(Image img, float v) { if (img) img.fillAmount = Mathf.Clamp01(v); }

        // ── UI 자동 생성 ─────────────────────────────────────────────────────
        void CreateUI()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var sc = gameObject.AddComponent<CanvasScaler>();
                sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var t = transform;

            // ── P1 (왼쪽) ────────────────────────────────────────────────
            // 패널 배경
            BgPanel(t, new Vector2(0,1), new Vector2(0,0), new Vector2(360, 96));
            nameText1   = Txt(t, "N1", "PLAYER",   new Vector2(0,1), new Vector2(12,-12), new Vector2(336,24), TextAnchor.MiddleLeft, 18);
            healthBar1  = Bar(t, "HP1", new Vector2(0,1), new Vector2(8,-40),  new Vector2(344,26), new Color(.1f,.9f,.2f),   false);
            staminaBar1 = Bar(t, "ST1", new Vector2(0,1), new Vector2(8,-70),  new Vector2(344,18), new Color(.2f,.55f,1f),   false);

            // ── P2 (오른쪽, 정확한 미러) ──────────────────────────────────
            BgPanel(t, new Vector2(1,1), new Vector2(0,0), new Vector2(360, 96));
            nameText2   = Txt(t, "N2", "ENEMY",    new Vector2(1,1), new Vector2(-12,-12), new Vector2(336,24), TextAnchor.MiddleRight, 18);
            healthBar2  = Bar(t, "HP2", new Vector2(1,1), new Vector2(-8,-40), new Vector2(344,26), new Color(.9f,.15f,.15f), true);
            staminaBar2 = Bar(t, "ST2", new Vector2(1,1), new Vector2(-8,-70), new Vector2(344,18), new Color(1f,.55f,.1f),   true);

            // ── 중앙 타이머 ───────────────────────────────────────────────
            BgPanel(t, new Vector2(.5f,1), new Vector2(0,0), new Vector2(140, 72));
            roundText = Txt(t, "Round", "ROUND 1", new Vector2(.5f,1), new Vector2(0,-14), new Vector2(130,22), TextAnchor.MiddleCenter, 18);
            timerText = Txt(t, "Timer", "99",      new Vector2(.5f,1), new Vector2(0,-42), new Vector2(130,32), TextAnchor.MiddleCenter, 28);
        }

        static void BgPanel(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot            = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        }

        static Text Txt(Transform parent, string id, string content,
            Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, int fs)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot            = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            var t = go.AddComponent<Text>();
            t.text = content; t.fontSize = fs; t.alignment = align; t.color = Color.white;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0,0,0,.8f); ol.effectDistance = new Vector2(1.5f,-1.5f);
            return t;
        }

        static Image Bar(Transform parent, string id,
            Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool fromRight)
        {
            var bg = new GameObject("BG_" + id);
            bg.transform.SetParent(parent, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = anchor;
            bgRt.pivot = anchor; bgRt.anchoredPosition = pos; bgRt.sizeDelta = size;
            bg.AddComponent<Image>().color = new Color(.08f,.08f,.08f,.9f);

            var fill = new GameObject("F"); fill.transform.SetParent(bg.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(2,2); fRt.offsetMax = new Vector2(-2,-2);
            var img = fill.AddComponent<Image>();
            img.color = color; img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }
    }
}
