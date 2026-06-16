using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// 1인칭 복싱 HUD.
    /// 플레이어(PlayerInputHandler)와 적(AIController) 파이터를 직접 찾아
    /// 매 프레임 체력/스태미너를 바 + 숫자로 표시한다.
    /// </summary>
    public class BoxingHUD : MonoBehaviour
    {
        Fighter _player;   // 왼쪽 (PlayerInputHandler)
        Fighter _enemy;    // 오른쪽 (AIController)

        Image _hp1, _sp1, _hp2, _sp2;
        Text  _name1, _name2, _hpTxt1, _spTxt1, _hpTxt2, _spTxt2, _timer, _round;

        float _findTimer;
        bool  _eventsHooked;

        void Start() => BuildUI();

        void Update()
        {
            // 0.5초마다 파이터 재탐색 (씬 로드/리스폰 대응)
            _findTimer -= Time.deltaTime;
            if (_findTimer <= 0f || _player == null || _enemy == null)
            {
                _findTimer = 0.5f;
                FindFighters();
            }

            HookEvents();

            // ── 플레이어 ──────────────────────────────────────────────────
            if (_player != null)
            {
                float mh = _player.data != null ? _player.data.maxHealth  : 100f;
                float ms = _player.data != null ? _player.data.maxStamina : 100f;
                if (_hp1) _hp1.fillAmount = Mathf.Clamp01(_player.CurrentHP / mh);
                if (_sp1) _sp1.fillAmount = Mathf.Clamp01(_player.CurrentStamina / ms);
                if (_hpTxt1) _hpTxt1.text = $"HP {Mathf.CeilToInt(_player.CurrentHP)}";
                if (_spTxt1) _spTxt1.text = $"SP {Mathf.CeilToInt(_player.CurrentStamina)}";
            }

            // ── 적 ───────────────────────────────────────────────────────
            if (_enemy != null)
            {
                float mh = _enemy.data != null ? _enemy.data.maxHealth  : 100f;
                float ms = _enemy.data != null ? _enemy.data.maxStamina : 100f;
                if (_hp2) _hp2.fillAmount = Mathf.Clamp01(_enemy.CurrentHP / mh);
                if (_sp2) _sp2.fillAmount = Mathf.Clamp01(_enemy.CurrentStamina / ms);
                if (_hpTxt2) _hpTxt2.text = $"HP {Mathf.CeilToInt(_enemy.CurrentHP)}";
                if (_spTxt2) _spTxt2.text = $"SP {Mathf.CeilToInt(_enemy.CurrentStamina)}";
            }
        }

        // ── 파이터 탐색: 컨트롤러 종류로 구분 ─────────────────────────────────
        void FindFighters()
        {
            var all = FindObjectsByType<Fighter>(FindObjectsSortMode.None);
            foreach (var f in all)
            {
                var pi = f.GetComponent<PlayerInputHandler>();
                var ai = f.GetComponent<AIController>();
                if (pi != null && pi.enabled)      _player = f;
                else if (ai != null && ai.enabled) _enemy  = f;
            }
            // 폴백: 컨트롤러로 못 가르면 MatchManager 사용
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                if (_player == null) _player = mm.fighter1;
                if (_enemy  == null) _enemy  = mm.fighter2;
            }
            // 그래도 비면 이름으로
            if (_player == null)
            {
                var go = GameObject.Find("Fighter1_Robert");
                if (go) _player = go.GetComponent<Fighter>();
            }
            if (_enemy == null)
            {
                var go = GameObject.Find("Fighter2_Engie");
                if (go) _enemy = go.GetComponent<Fighter>();
            }

            if (_player != null && _name1 != null)
                _name1.text = (_player.data != null && _player.data.fighterName.Length > 0)
                    ? _player.data.fighterName : "PLAYER";
            if (_enemy != null && _name2 != null)
                _name2.text = (_enemy.data != null && _enemy.data.fighterName.Length > 0)
                    ? _enemy.data.fighterName : "ENEMY";
        }

        void HookEvents()
        {
            if (_eventsHooked) return;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            mm.OnTimerUpdate += t => { if (_timer) _timer.text = Mathf.CeilToInt(t).ToString("00"); };
            mm.OnRoundStart  += r => { if (_round) _round.text = $"ROUND {r}"; };
            _eventsHooked = true;
        }

        // ── UI 생성 ──────────────────────────────────────────────────────────
        void BuildUI()
        {
            if (GetComponent<Canvas>() == null)
            {
                var c = gameObject.AddComponent<Canvas>();
                c.renderMode   = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 100;
                var sc = gameObject.AddComponent<CanvasScaler>();
                sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var t = transform;
            var L = new Vector2(0, 1);   // 좌상
            var R = new Vector2(1, 1);   // 우상
            var C = new Vector2(.5f, 1); // 중상

            // 플레이어 패널 (왼쪽)
            Panel(t, L, new Vector2(20, -20),  new Vector2(380, 110));
            _name1  = Label(t, "PLAYER", L, new Vector2(34, -28),  new Vector2(360, 26), TextAnchor.MiddleLeft, 20);
            _hp1    = Bar(t, L, new Vector2(30, -58),  new Vector2(360, 28), new Color(.15f,.85f,.25f), false);
            _hpTxt1 = Label(t, "HP 100", L, new Vector2(40, -58), new Vector2(340, 28), TextAnchor.MiddleLeft, 16);
            _sp1    = Bar(t, L, new Vector2(30, -90),  new Vector2(360, 20), new Color(.25f,.55f,1f), false);
            _spTxt1 = Label(t, "SP 100", L, new Vector2(40, -90), new Vector2(340, 20), TextAnchor.MiddleLeft, 14);

            // 적 패널 (오른쪽, 대칭)
            Panel(t, R, new Vector2(-20, -20), new Vector2(380, 110));
            _name2  = Label(t, "ENEMY", R, new Vector2(-34, -28), new Vector2(360, 26), TextAnchor.MiddleRight, 20);
            _hp2    = Bar(t, R, new Vector2(-30, -58), new Vector2(360, 28), new Color(.9f,.18f,.18f), true);
            _hpTxt2 = Label(t, "HP 100", R, new Vector2(-40, -58), new Vector2(340, 28), TextAnchor.MiddleRight, 16);
            _sp2    = Bar(t, R, new Vector2(-30, -90), new Vector2(360, 20), new Color(1f,.55f,.1f), true);
            _spTxt2 = Label(t, "SP 100", R, new Vector2(-40, -90), new Vector2(340, 20), TextAnchor.MiddleRight, 14);

            // 중앙 타이머
            Panel(t, C, new Vector2(0, -20), new Vector2(150, 80));
            _round = Label(t, "ROUND 1", C, new Vector2(0, -26), new Vector2(150, 24), TextAnchor.MiddleCenter, 18);
            _timer = Label(t, "99",      C, new Vector2(0, -54), new Vector2(150, 34), TextAnchor.MiddleCenter, 30);
        }

        static void Panel(Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        }

        static Text Label(Transform p, string txt, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, int fs)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.text = txt; t.fontSize = fs; t.alignment = align; t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0,0,0,.9f); ol.effectDistance = new Vector2(1.5f,-1.5f);
            return t;
        }

        static Image Bar(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color col, bool fromRight)
        {
            var bg = new GameObject("BarBG");
            bg.transform.SetParent(p, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = anchor; bgRt.pivot = anchor;
            bgRt.anchoredPosition = pos; bgRt.sizeDelta = size;
            bg.AddComponent<Image>().color = new Color(.1f,.1f,.1f,.95f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bg.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(2,2); fRt.offsetMax = new Vector2(-2,-2);
            var img = fill.AddComponent<Image>();
            img.color = col;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }
    }
}
