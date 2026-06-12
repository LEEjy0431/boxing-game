using System;
using System.Collections;
using UnityEngine;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    public enum GameMode { Story, VsAI, TwoPlayer }

    /// <summary>
    /// 라운드/경기 전체를 관리. 3D 버전.
    /// 라운드 시작 시 파이터를 링 반대편으로 배치 후 ResetFighter() 호출.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Settings")]
        public GameMode mode         = GameMode.TwoPlayer;
        public int    roundsToWin   = 2;
        public float  roundDuration = 99f;

        [Header("References")]
        public Fighter fighter1;
        public Fighter fighter2;

        [Header("Spawn Points (3D)")]
        public Transform spawnPoint1;   // 링 한쪽 코너
        public Transform spawnPoint2;   // 링 반대쪽 코너

        [Header("Ring Floor")]
        [Tooltip("링 바닥 Y 좌표. 0이면 자동 감지. 카메라가 링 아래에 있으면 이 값을 링 표면 높이로 맞추세요.")]
        public float ringFloorYOverride = 0f;

        // Events
        public event Action<int>               OnRoundStart;
        public event Action<Fighter, int, int> OnRoundEnd;
        public event Action<Fighter>           OnMatchEnd;
        public event Action<float>             OnTimerUpdate;

        public int  CurrentRound { get; private set; }
        public bool MatchActive  { get; private set; }
        public bool RoundActive  => _roundActive;   // 입력/AI 게이팅용

        int   _score1, _score2;
        float _timer;
        bool  _roundActive;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Inspector 연결이 없으면 씬에서 자동 탐색 (씬 간 참조 에러 대응)
            if (fighter1 == null || fighter2 == null)
                AutoFindFighters();

            // 스폰포인트가 이미 설정돼 있으면 그 Y를 사용 (가장 신뢰할 수 있는 값)
            // ringFloorYOverride, GetRingFloorY() 순으로 폴백
            float ringY = 0f;
            if (spawnPoint1 != null && spawnPoint1.position.y > 0.01f)
                ringY = spawnPoint1.position.y;
            else if (ringFloorYOverride > 0.01f)
                ringY = ringFloorYOverride;
            else
                ringY = GetRingFloorY();

            if (ringY > 0.01f)
            {
                if (spawnPoint1 != null && spawnPoint1.position.y < ringY)
                    spawnPoint1.position = new Vector3(spawnPoint1.position.x, ringY, spawnPoint1.position.z);
                if (spawnPoint2 != null && spawnPoint2.position.y < ringY)
                    spawnPoint2.position = new Vector3(spawnPoint2.position.x, ringY, spawnPoint2.position.z);
                Debug.Log($"[MatchManager] 링 바닥 Y={ringY:F2}");
            }
            else
            {
                Debug.LogWarning("[MatchManager] 링 바닥 감지 실패 — 파이터가 Y=0에 스폰됩니다.");
            }

            // RingBoundary 없으면 런타임에 자동 생성
            var existingRB = FindAnyObjectByType<RingBoundary>();
            if (existingRB == null)
            {
                var rbGo = new GameObject("RingBoundary");
                rbGo.transform.position = new Vector3(0f, ringY > 0.01f ? ringY : 0f, 0f);
                var rb = rbGo.AddComponent<RingBoundary>();
                rb.ringRadius = 5f;
                rb.fighters   = new Fighter[] { fighter1, fighter2 };
                Debug.Log("[MatchManager] RingBoundary 자동 생성 (반경 5m)");
            }
            else if (existingRB.fighters == null || existingRB.fighters.Length == 0)
            {
                existingRB.fighters = new Fighter[] { fighter1, fighter2 };
            }

            StartCoroutine(RunMatch());
        }

        void AutoFindFighters()
        {
            // 1순위: 이름으로 탐색
            var f1Go = GameObject.Find("Fighter1_Robert");
            var f2Go = GameObject.Find("Fighter2_Engie");
            if (f1Go != null && fighter1 == null) fighter1 = f1Go.GetComponent<Fighter>();
            if (f2Go != null && fighter2 == null) fighter2 = f2Go.GetComponent<Fighter>();

            // 2순위: PlayerInputHandler 유무로 구분
            if (fighter1 == null || fighter2 == null)
            {
                foreach (var f in FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                {
                    var ph = f.GetComponent<PlayerInputHandler>();
                    if (ph != null && ph.enabled)
                    {
                        if (fighter1 == null) fighter1 = f;
                    }
                    else
                    {
                        if (fighter2 == null) fighter2 = f;
                    }
                }
            }

            // 3순위: 아무 Fighter나 두 개
            if (fighter1 == null || fighter2 == null)
            {
                var all = FindObjectsByType<Fighter>(FindObjectsSortMode.None);
                if (all.Length >= 1 && fighter1 == null) fighter1 = all[0];
                if (all.Length >= 2 && fighter2 == null) fighter2 = all[1];
                if (fighter1 == fighter2) fighter2 = null; // 같은 오브젝트면 무효
            }

            // opponentTransform 상호 연결
            if (fighter1 != null && fighter2 != null)
            {
                fighter1.opponentTransform = fighter2.transform;
                fighter2.opponentTransform = fighter1.transform;
                Debug.Log($"[MatchManager] Fighter1={fighter1.name}, Fighter2={fighter2.name}");
            }
            else
            {
                Debug.LogError("[MatchManager] 파이터를 찾지 못했습니다! Inspector에서 Fighter1/Fighter2를 직접 연결하거나 Tools > PersonalityBox > 🔥 COMPLETE FIX 를 실행하세요.");
            }
        }

        void Update()
        {
            if (!_roundActive) return;
            _timer -= Time.deltaTime;
            OnTimerUpdate?.Invoke(_timer);
            if (_timer <= 0f) EndRoundByTime();
        }

        // ── Match flow ───────────────────────────────────────────────────────

        IEnumerator RunMatch()
        {
            MatchActive = true;

            // 파이터 참조 재확인 — 최대 2초 대기하며 재탐색
            if (fighter1 == null || fighter2 == null)
            {
                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForSeconds(0.1f);
                    AutoFindFighters();
                    if (fighter1 != null && fighter2 != null) break;
                }
            }

            if (fighter1 == null || fighter2 == null)
            {
                Debug.LogError("[MatchManager] RunMatch: 파이터 참조 없음 — 매치를 시작할 수 없습니다. Tools > 🔥 COMPLETE FIX 실행 후 재시작하세요.");
                yield break;
            }

            fighter1.OnKO += () => { if (_roundActive) StartCoroutine(EndRound(fighter2)); };
            fighter2.OnKO += () => { if (_roundActive) StartCoroutine(EndRound(fighter1)); };

            while (_score1 < roundsToWin && _score2 < roundsToWin)
            {
                CurrentRound++;
                yield return StartCoroutine(StartRound());
                yield return new WaitUntil(() => !_roundActive);
                yield return new WaitForSeconds(2.5f);
            }

            Fighter winner = _score1 >= roundsToWin ? fighter1 : fighter2;
            MatchActive = false;
            OnMatchEnd?.Invoke(winner);
        }

        IEnumerator StartRound()
        {
            ResetFighters();
            yield return new WaitForSeconds(0.2f);   // 파이터 리셋 안정화 대기
            _timer = roundDuration;
            _roundActive = true;
            OnRoundStart?.Invoke(CurrentRound);
        }

        IEnumerator EndRound(Fighter winner)
        {
            _roundActive = false;
            if (winner == fighter1) _score1++;
            else                    _score2++;
            OnRoundEnd?.Invoke(winner, _score1, _score2);
            yield return null;
        }

        void EndRoundByTime()
        {
            Fighter winner = fighter1.CurrentHP >= fighter2.CurrentHP ? fighter1 : fighter2;
            StartCoroutine(EndRound(winner));
        }

        void ResetFighters()
        {
            if (spawnPoint1 != null)
            {
                fighter1.transform.position = spawnPoint1.position;
                fighter1.transform.rotation = spawnPoint1.rotation;
            }
            else
            {
                float ringY = GetRingFloorY();
                fighter1.transform.position = new Vector3(-2.5f, ringY, 0f);
                fighter1.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            if (spawnPoint2 != null)
            {
                fighter2.transform.position = spawnPoint2.position;
                fighter2.transform.rotation = spawnPoint2.rotation;
            }
            else
            {
                float ringY = GetRingFloorY();
                fighter2.transform.position = new Vector3(2.5f, ringY, 0f);
                fighter2.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            fighter1.ResetFighter();
            fighter2.ResetFighter();
        }

        // 씬 중앙 위에서 레이캐스트로 링 바닥 Y를 감지
        float GetRingFloorY()
        {
            // 다양한 위치에서 레이캐스트
            float[] testX = { 0f, -1f, 1f, -2f, 2f };
            float[] testZ = { 0f, -1f, 1f };
            foreach (float x in testX)
            {
                foreach (float z in testZ)
                {
                    var ray = new Ray(new Vector3(x, 40f, z), Vector3.down);
                    if (Physics.Raycast(ray, out RaycastHit hit, 60f))
                    {
                        if (!hit.collider.isTrigger &&
                            hit.collider.GetComponentInParent<Fighter>() == null &&
                            hit.point.y > -10f)   // 지하 오브젝트 제외
                            return hit.point.y + 0.05f;  // 표면 위로 살짝 올림
                    }
                }
            }
            // 레이캐스트 실패 시 렌더러 기반 추정
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.gameObject.name.ToLower();
                if (n.Contains("ringfloor") || n.Contains("ring floor") ||
                    n.Contains("stage") || n.Contains("canvas"))
                    return r.bounds.max.y + 0.05f;
            }
            return 0f;
        }

        public string GetOpponentBackstory(Fighter opponent)
            => opponent.data != null ? opponent.data.backstory : "";
    }
}
