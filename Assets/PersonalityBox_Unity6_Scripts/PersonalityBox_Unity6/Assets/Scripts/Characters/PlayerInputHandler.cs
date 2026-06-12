using UnityEngine;
using PersonalityBox.Core;

namespace PersonalityBox.Characters
{
    /// <summary>
    /// 3D 조작 입력.
    /// WASD = XZ 평면 이동 | Q = 가드 | Shift = 회피
    /// J/K/L = 잽/훅/어퍼컷 | Space = 필살기
    ///
    /// 공격 높이 — 펀치 키 입력 시 이동 방향으로 결정:
    ///   W(위) 누른 채로 펀치 = 상단(High)
    ///   S(아래) 누른 채로 펀치 = 하단(Low) ← 가드 무시
    ///   중립 = 중단(Mid)
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
                side = new Vector3(-fwd.z, 0f, fwd.x);  // fwd의 오른쪽 수직
            }
            else
            {
                fwd  = _fighter.transform.forward;
                side = _fighter.transform.right;
            }

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += fwd;
            if (Input.GetKey(KeyCode.S)) move -= fwd;
            if (Input.GetKey(KeyCode.D)) move += side;
            if (Input.GetKey(KeyCode.A)) move -= side;
            if (move.sqrMagnitude > 1f) move.Normalize();

            _fighter.Move(move.x, move.z);

            // 가드: Q (홀드)
            if (Input.GetKeyDown(KeyCode.Q)) _fighter.StartBlock();
            if (Input.GetKeyUp(KeyCode.Q))   _fighter.StopBlock();

            // 회피: Left Shift — 현재 이동 방향으로 회피
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                Vector3 dodgeDir = move.sqrMagnitude > 0.01f ? move : -fwd;
                _fighter.Dodge(dodgeDir.normalized);
            }

            // 높이 결정: W=상단 / S=하단 / 중립=중단
            bool wDown = Input.GetKey(KeyCode.W);
            bool sDown = Input.GetKey(KeyCode.S);
            PunchHeight height = wDown ? PunchHeight.High
                               : sDown ? PunchHeight.Low
                               : PunchHeight.Mid;

            if (Input.GetKeyDown(KeyCode.J)) _fighter.Punch(PunchType.Jab,      height);
            if (Input.GetKeyDown(KeyCode.K)) _fighter.Punch(PunchType.Hook,     height);
            if (Input.GetKeyDown(KeyCode.L)) _fighter.Punch(PunchType.Uppercut, height);
            if (Input.GetKeyDown(KeyCode.Space)) _fighter.UseSpecial();
        }

        // ────────────────── Player 2 ─────────────────────────────────────────
        void HandleP2()
        {
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.LeftArrow))  h = -1f;
            if (Input.GetKey(KeyCode.RightArrow)) h =  1f;
            if (Input.GetKey(KeyCode.UpArrow))    v =  1f;
            if (Input.GetKey(KeyCode.DownArrow))  v = -1f;

            Vector3 worldMove = _fighter.transform.TransformDirection(new Vector3(h, 0f, v));
            worldMove.y = 0f;
            _fighter.Move(worldMove.x, worldMove.z);

            if (Input.GetKeyDown(KeyCode.Keypad0)) _fighter.StartBlock();
            if (Input.GetKeyUp(KeyCode.Keypad0))   _fighter.StopBlock();

            if (Input.GetKeyDown(KeyCode.RightShift))
            {
                Vector3 localDodge = new Vector3(h, 0f, v);
                Vector3 worldDodge = _fighter.transform.TransformDirection(localDodge);
                worldDodge.y = 0f;
                if (worldDodge.sqrMagnitude < 0.01f)
                    worldDodge = -_fighter.transform.forward;
                _fighter.Dodge(worldDodge.normalized);
            }

            PunchHeight height = v > 0.3f  ? PunchHeight.High
                               : v < -0.3f ? PunchHeight.Low
                               : PunchHeight.Mid;

            if (Input.GetKeyDown(KeyCode.Keypad1)) _fighter.Punch(PunchType.Jab,      height);
            if (Input.GetKeyDown(KeyCode.Keypad2)) _fighter.Punch(PunchType.Hook,     height);
            if (Input.GetKeyDown(KeyCode.Keypad3)) _fighter.Punch(PunchType.Uppercut, height);
            if (Input.GetKeyDown(KeyCode.RightControl)) _fighter.UseSpecial();
        }
    }
}
