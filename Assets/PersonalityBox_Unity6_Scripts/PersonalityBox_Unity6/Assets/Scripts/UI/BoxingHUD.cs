using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// 1인칭 복싱 HUD.
    /// 매 프레임 파이터 값을 직접 읽어 바를 갱신 (이벤트 방식 대신 폴링).
    /// </summary>
    public class BoxingHUD : MonoBehaviour
    {
        [Header("Inspector 직접 연결 (비워두면 자동)")]
        public Fighter fighter1;
        public Fighter fighter2;

        [Header("Fighter 1 (왼쪽)")]
        public Image healthBar1;
        public Image staminaBar1;
        public Text  nameText1;

        [Header("Fighter 2 (오른쪽)")]
        public Image healthBar2;
        public Image staminaBar2;
        public Text  nameText2;

        [Header("중앙")]
        public Text timerText;
        public Text roundText;

        bool _ready;

        void Start()
        {
            if (healthBar1 == null) CreateUI();
            TryInit();
        }

        void Update()
        {
            if (!_ready) TryInit();
            if (!_ready) return;

            // 매 프레임 직접 폴링 — 이벤트 연결 없이도 항상 최신 값 표시
            float maxHp1 = fighter1.data != null ? fighter1.data.maxHealth  : 100f;
            float maxHp2 = fighter2.data != null ? fighter2.data.maxHealth  : 100f;
            float maxSt1 = fighter1.data != null ? fighter1.data.maxStamina : 100f;
            float maxSt2 = fighter2.data != null ? fighter2.data.maxStamina : 100f;

            SetFill(healthBar1,  fighter1.CurrentHP      / maxHp1);
            SetFill(staminaBar1, fighter1.CurrentStamina / maxSt1);
            SetFill(healthBar2,  fighter2.CurrentHP      / maxHp2);
            SetFill(staminaBar2, fighter2.CurrentStamina / maxSt2);
        }

        void TryInit()
        {
            var mm = MatchManager.Instance;
            if (fighter1 == null && mm != null) fighter1 = mm.fighter1;
            if (fighter2 == null && mm != null) fighter2 = mm.fighter2;
            if (fighter1 == null || fighter2 == null) return;

            if (nameText1) nameText1.text = fighter1.data?.fighterName ?? fighter1.name;
            if (nameText2) nameText2.text = fighter2.data?.fighterName ?? fighter2.name;

            if (mm != null)
            {
                mm.OnTimerUpdate += t => { if (timerText) timerText.text = Mathf.CeilToInt(t).ToString("00"); };
                mm.OnRoundStart  += r => { if (roundText)  roundText.text  = $"ROUND {r}"; };
            }
            _ready = true;
        }

        void SetFill(Image img, float value)
        {
            if (img) img.fillAmount = Mathf.Clamp01(value);
        }

        // ── UI 자동 생성 ─────────────────────────────────────────────────────
        void CreateUI()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var t  = transform;
            var tl = new Vector2(0f,  1f);
            var tr = new Vector2(1f,  1f);
            var tc = new Vector2(.5f, 1f);

            nameText1   = MakeText(t, "Lbl_P1",    "PLAYER 1", tl, new Vector2(10,-10),  new Vector2(320,28), TextAnchor.MiddleLeft,  20);
            healthBar1  = MakeBar (t, "HP1",  tl, new Vector2(10,-42),  new Vector2(320,22), new Color(.15f,.85f,.25f), false);
            staminaBar1 = MakeBar (t, "ST1",  tl, new Vector2(10,-68),  new Vector2(320,14), new Color(.25f,.55f,1.0f), false);

            nameText2   = MakeText(t, "Lbl_P2",    "FIGHTER 2", tr, new Vector2(-10,-10), new Vector2(320,28), TextAnchor.MiddleRight, 20);
            healthBar2  = MakeBar (t, "HP2",  tr, new Vector2(-10,-42), new Vector2(320,22), new Color(.90f,.20f,.20f), true);
            staminaBar2 = MakeBar (t, "ST2",  tr, new Vector2(-10,-68), new Vector2(320,14), new Color(1.0f,.60f,.15f), true);

            roundText = MakeText(t, "Lbl_Round", "ROUND 1", tc, new Vector2(0,-10), new Vector2(200,28), TextAnchor.MiddleCenter, 22);
            timerText = MakeText(t, "Lbl_Timer", "99",      tc, new Vector2(0,-42), new Vector2(100,40), TextAnchor.MiddleCenter, 32);
        }

        static Text MakeText(Transform parent, string id, string txt,
            Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, int fontSize)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot            = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            var tx = go.AddComponent<Text>();
            tx.text      = txt;
            tx.fontSize  = fontSize;
            tx.alignment = align;
            tx.color     = Color.white;
            var ol = go.AddComponent<Outline>();
            ol.effectColor    = new Color(0, 0, 0, 0.7f);
            ol.effectDistance = new Vector2(1, -1);
            return tx;
        }

        static Image MakeBar(Transform parent, string id,
            Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor, bool fromRight)
        {
            var bgGo = new GameObject("BarBG_" + id);
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = anchor;
            bgRt.pivot            = anchor;
            bgRt.anchoredPosition = pos;
            bgRt.sizeDelta        = size;
            bgGo.AddComponent<Image>().color = new Color(.08f, .08f, .08f, .85f);

            var fGo = new GameObject("Fill_" + id);
            fGo.transform.SetParent(bgGo.transform, false);
            var fRt = fGo.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(2, 2);
            fRt.offsetMax = new Vector2(-2, -2);
            var fImg = fGo.AddComponent<Image>();
            fImg.color      = fillColor;
            fImg.type       = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;
            fImg.fillOrigin = fromRight
                ? (int)Image.OriginHorizontal.Right
                : (int)Image.OriginHorizontal.Left;
            fImg.fillAmount = 1f;
            return fImg;
        }
    }
}
