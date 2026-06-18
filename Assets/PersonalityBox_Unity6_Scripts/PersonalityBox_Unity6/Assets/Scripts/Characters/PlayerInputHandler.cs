using UnityEngine;
using PersonalityBox.Core;

namespace PersonalityBox.Characters
{
    /// <summary>
    /// 3D 조작 입력.
    /// WASD = XZ 평면 이동 | Q(홀드) = 가드 | Shift = 회피
    /// J/K/L = 잽/훅/어퍼컷
    ///
    /// 높이 수정자: Space = 상단(High), C = 하단(Low), 중립 = 중단(Mid)
    ///   Space+J = 상단잽, Space+K = 상단훅
    ///   C+J = 하단잽, C+K = 하단훅
    ///   Space+Q = 상단가드, C+Q = 하단가드
    /// 가드는 공격 높이와 정확히 일치해야 막힘 (틀리면 그대로 맞음).
    /// </summary>
    [RequireComponent(typeof(Fighter))]
    public class PlayerInputHandler : MonoBehaviour
    {
        public enum InputScheme { Player1, Player2 }
        public InputScheme scheme = InputScheme.Player1;

        Fighter _fighter;

        void Awake()
        {
            _fighter = GetComponent<Fighter>();
        }

        void Update()
        {
            // Awake 타이밍 문제 방어
            if (_fighter == null)
            {
                _fighter = GetComponent<Fighter>();
                if (_fighter == null) return;
            }

            // 매치가 진행 중이고 라운드 사이인 경우에만 차단 (매치 시작 전은 허용)
            if (MatchManager.Instance != null && MatchManager.Instance.MatchActive && !MatchManager.Instance.RoundActive) return;

            if (scheme == InputScheme.Player1) HandleP1();
            else                               HandleP2();
        }

        // ────────────────── Player 1 ─────────────────────────────────────────
        void HandleP1()
        {
            // 이동: 상대방 기준 월드 방향으로 직접 계산
            // W=상대방 쪽 전진, S=후퇴, A=왼쪽 스텝, D=오른쪽 스텝
            Vector3 fwd  = Vector3.zero;
            Vector3 side = Vector3.zero;

            if (_fighter.opponentTransform != null)
            {
                fwd = (_fighter.opponentTransform.position - _fighter.transform.position);
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.001f) fwd.Normalize();
                else fwd = _fighter.transform.forward;
                side = new Vector3(fwd.z, 0f, -fwd.x);  // fwd 기준 오른쪽 수직 (D=오른쪽)
            }
            else
            {
                fwd  = _fighter.transform.forward;
                side = _fighter.transform.right;
            }

            // 높이 수정자: Space=상단 / C=하단 / 중립=중단
            bool spaceMod = Input.GetKey(KeyCode.Space);
            bool cMod     = Input.GetKey(KeyCode.C);
            PunchHeight height = spaceMod ? PunchHeight.High
                               : cMod    ? PunchHeight.Low
                               : PunchHeight.Mid;

            // 가드: Q (홀드) — Space/C 수정자로 가드 방향 전환
            if (Input.GetKeyDown(KeyCode.Q)) _fighter.StartBlock(height);
            if (Input.GetKey(KeyCode.Q))     _fighter.SetBlockHeight(height);
            if (Input.GetKeyUp(KeyCode.Q))   _fighter.StopBlock();

            // 이동: 키를 누를 때마다 한 스텝씩 이동 (발소리 포함)
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) ||
                Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
            {
                Vector3 stepDir = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) stepDir += fwd;
                if (Input.GetKey(KeyCode.S)) stepDir -= fwd;
                if (Input.GetKey(KeyCode.D)) stepDir += side;
                if (Input.GetKey(KeyCode.A)) stepDir -= side;
                if (stepDir.sqrMagnitude > 0.01f)
                    _fighter.Step(stepDir.normalized);
            }
            // 스텝 중이 아닐 때 아이들 상태 유지
            _fighter.Move(0f, 0f);

            // 회피: Left Shift — WASD 방향 또는 상대방 반대쪽으로 회피
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                Vector3 dodgeDir = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) dodgeDir += fwd;
                if (Input.GetKey(KeyCode.S)) dodgeDir -= fwd;
                if (Input.GetKey(KeyCode.D)) dodgeDir += side;
                if (Input.GetKey(KeyCode.A)) dodgeDir -= side;
                if (dodgeDir.sqrMagnitude < 0.01f) dodgeDir = -fwd;
                _fighter.Dodge(dodgeDir.normalized);
            }

            if (Input.GetKeyDown(KeyCode.J)) _fighter.Punch(PunchType.Jab,      height);
            if (Input.GetKeyDown(KeyCode.K)) _fighter.Punch(PunchType.Hook,     height);
            if (Input.GetKeyDown(KeyCode.L)) _fighter.Punch(PunchType.Uppercut, height);
        }

        // ────────────────── Player 2 ─────────────────────────────────────────
        void HandleP2()
        {
            // 높이 판단: 방향키 Up=상단 / Down=하단 / 중립=중단
            bool upHeld   = Input.GetKey(KeyCode.UpArrow);
            bool downHeld = Input.GetKey(KeyCode.DownArrow);
            PunchHeight height = upHeld   ? PunchHeight.High
                               : downHeld ? PunchHeight.Low
                               : PunchHeight.Mid;

            if (Input.GetKeyDown(KeyCode.Keypad0)) _fighter.StartBlock(height);
            if (Input.GetKey(KeyCode.Keypad0))     _fighter.SetBlockHeight(height);
            if (Input.GetKeyUp(KeyCode.Keypad0))   _fighter.StopBlock();

            // 이동: 키를 누를 때마다 한 스텝씩 이동 (발소리 포함)
            if (Input.GetKeyDown(KeyCode.UpArrow)    || Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.RightArrow))
            {
                float h = 0f, v = 0f;
                if (Input.GetKey(KeyCode.LeftArrow))  h = -1f;
                if (Input.GetKey(KeyCode.RightArrow)) h =  1f;
                if (Input.GetKey(KeyCode.UpArrow))    v =  1f;
                if (Input.GetKey(KeyCode.DownArrow))  v = -1f;
                Vector3 stepDir = _fighter.transform.TransformDirection(new Vector3(h, 0f, v));
                stepDir.y = 0f;
                if (stepDir.sqrMagnitude > 0.01f)
                    _fighter.Step(stepDir.normalized);
            }
            _fighter.Move(0f, 0f);

            if (Input.GetKeyDown(KeyCode.RightShift))
            {
                float h = 0f, v = 0f;
                if (Input.GetKey(KeyCode.LeftArrow))  h = -1f;
                if (Input.GetKey(KeyCode.RightArrow)) h =  1f;
                if (Input.GetKey(KeyCode.UpArrow))    v =  1f;
                if (Input.GetKey(KeyCode.DownArrow))  v = -1f;
                Vector3 worldDodge = _fighter.transform.TransformDirection(new Vector3(h, 0f, v));
                worldDodge.y = 0f;
                if (worldDodge.sqrMagnitude < 0.01f)
                    worldDodge = -_fighter.transform.forward;
                _fighter.Dodge(worldDodge.normalized);
            }

            if (Input.GetKeyDown(KeyCode.Keypad1)) _fighter.Punch(PunchType.Jab,      height);
            if (Input.GetKeyDown(KeyCode.Keypad2)) _fighter.Punch(PunchType.Hook,     height);
            if (Input.GetKeyDown(KeyCode.Keypad3)) _fighter.Punch(PunchType.Uppercut, height);
        }
    }
}
