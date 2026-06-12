using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// 1인칭 복싱 HUD — 체력/스태미너 바를 매 프레임 직접 폴링해서 갱신.
    /// </summary>
    public class BoxingHUD : MonoBehaviour
    {
        [Header("파이터 (비워두면 자동 탐색)")]
        public Fighter fighter1;
        public Fighter fighter2;

        [Header("UI 요소 (자동 생성)")]
        public Image healthBar1;
        public Image staminaBar1;
        public Text  nameText1;
        public Image healthBar2;
        public Image staminaBar2;
        public Text  nameText2;
        public Text  timerText;
        public Text  roundText;

        bool _ready;

        void Start()
        {
            if (healthBar1 == null) CreateUI();
            TryInit();
        }

        void Update()
        {
            if (!_ready) { TryInit(); return; }

            // ── 매 프레임 직접 읽기 ────────────────────────────────────────
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
            // 1순위: Inspector 직접 연결
            // 2순위: MatchManager
            // 3순위: 이름으로 직접 탐색
            if (fighter1 == null)
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.fighter1 != null)
                    fighter1 = mm.fighter1;
                else
                {
                    var go = GameObject.Find("Fighter1_Robert");
                    if (go != null) fighter1 = go.GetComponent<Fighter>();
                }
            }
            if (fighter2 == null)
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.fighter2 != null)
                    fighter2 = mm.fighter2;
                else
                {
                    var go = GameObject.Find("Fighter2_Engie");
                    if (go != null) fighter2 = go.GetComponent<Fighter>();
                }
            }

            if (fighter1 == null || fighter2 == null) return;

            if (nameText1) nameText1.text = fighter1.data?.fighterName.Length > 0
                ? fighter1.data.fighterName : "PLAYER";
            if (nameText2) nameText2.text = fighter2.data?.fighterName.Length > 0
                ? fighter2.data.fighterName : "FIGHTER 2";

            var mm2 = MatchManager.Instance;
            if (mm2 != null)
            {
                mm2.OnTimerUpdate += t => { if (timerText) timerText.text = Mathf.CeilToInt(t).ToString("00"); };
                mm2.OnRoundStart  += r => { if (roundText)  roundText.text  = $"ROUND {r}"; };
            }
            _ready = true;
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

            var tr = transform;

            // ── Fighter 1 패널 (왼쪽 상단) ───────────────────────────────
            // 검은 배경 패널
            MakePanel(tr, "P1_Panel", new Vector2(0, 1), new Vector2(10, -8), new Vector2(340, 90));

            nameText1   = MakeText(tr, "P1_Name",  "PLAYER 1",
                new Vector2(0,1), new Vector2(18, -14), new Vector2(310, 26),
                TextAnchor.MiddleLeft, 18);
            healthBar1  = MakeBar(tr, "HP1",
                new Vector2(0,1), new Vector2(10, -44), new Vector2(330, 26),
                new Color(.15f,.85f,.25f), false);
            staminaBar1 = MakeBar(tr, "ST1",
                new Vector2(0,1), new Vector2(10, -74), new Vector2(330, 16),
                new Color(.25f,.55f,1.0f), false);

            // ── Fighter 2 패널 (오른쪽 상단, 정확히 미러) ────────────────
            MakePanel(tr, "P2_Panel", new Vector2(1, 1), new Vector2(-10, -8), new Vector2(340, 90));

            nameText2   = MakeText(tr, "P2_Name",  "FIGHTER 2",
                new Vector2(1,1), new Vector2(-18, -14), new Vector2(310, 26),
                TextAnchor.MiddleRight, 18);
            healthBar2  = MakeBar(tr, "HP2",
                new Vector2(1,1), new Vector2(-10, -44), new Vector2(330, 26),
                new Color(.90f,.20f,.20f), true);
            staminaBar2 = MakeBar(tr, "ST2",
                new Vector2(1,1), new Vector2(-10, -74), new Vector2(330, 16),
                new Color(1.0f,.60f,.15f), true);

            // ── 중앙 타이머 ──────────────────────────────────────────────
            roundText = MakeText(tr, "Round", "ROUND 1",
                new Vector2(.5f,1), new Vector2(0, -12), new Vector2(220, 28),
                TextAnchor.MiddleCenter, 22);
            timerText = MakeText(tr, "Timer", "99",
                new Vector2(.5f,1), new Vector2(0, -44), new Vector2(120, 38),
                TextAnchor.MiddleCenter, 30);
        }

        // 반투명 검은 배경 패널
        static void MakePanel(Transform parent, string id,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot            = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.45f);
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
            var t = go.AddComponent<Text>();
            t.text      = txt;
            t.fontSize  = fontSize;
            t.alignment = align;
            t.color     = Color.white;
            var ol = go.AddComponent<Outline>();
            ol.effectColor    = new Color(0, 0, 0, 0.8f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Image MakeBar(Transform parent, string id,
            Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor, bool fromRight)
        {
            // 배경
            var bg = new GameObject("BG_" + id);
            bg.transform.SetParent(parent, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = anchor;
            bgRt.pivot            = anchor;
            bgRt.anchoredPosition = pos;
            bgRt.sizeDelta        = size;
            bg.AddComponent<Image>().color = new Color(.1f, .1f, .1f, .9f);

            // 채우기
            var fill = new GameObject("Fill_" + id);
            fill.transform.SetParent(bg.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(2, 2);
            fRt.offsetMax = new Vector2(-2, -2);
            var img = fill.AddComponent<Image>();
            img.color      = fillColor;
            img.type       = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = fromRight
                ? (int)Image.OriginHorizontal.Right
                : (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }
    }
}
