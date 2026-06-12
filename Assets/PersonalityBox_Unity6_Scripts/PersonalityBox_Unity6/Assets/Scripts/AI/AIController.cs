using System.Collections;
using UnityEngine;
using PersonalityBox.Characters;
using PersonalityBox.Core;

namespace PersonalityBox.AI
{
    public enum AIBehaviour { Aggressive, Balanced, Technical }

    /// <summary>
    /// 3D 룰 기반 AI.
    /// 상대와의 3D 거리 계산 후 XZ 평면에서 이동/회피/공격 결정.
    /// </summary>
    [RequireComponent(typeof(Fighter))]
    public class AIController : MonoBehaviour
    {
        [Header("Behaviour")]
        public AIBehaviour behaviour = AIBehaviour.Balanced;

        [Header("Timing")]
        public float thinkInterval = 0.28f;
        public float reactionDelay = 0.12f;

        Fighter _self;
        Fighter _opponent;
        float   _thinkTimer;
        bool    _isBlocking;

        void Start()
        {
            _self = GetComponent<Fighter>();
            foreach (var f in FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                if (f != _self) { _opponent = f; break; }

            _self.OnHealthChanged += (hp, max) => ReactToLowHealth(hp / max);
        }

        void Update()
        {
            if (_opponent == null || _self.State == FighterState.KO) return;
            // 매치가 진행 중이고 라운드 사이인 경우에만 차단 (매치 시작 전은 허용)
            if (MatchManager.Instance != null && MatchManager.Instance.MatchActive && !MatchManager.Instance.RoundActive) return;
            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = thinkInterval + Random.Range(-0.04f, 0.04f);
                StartCoroutine(ThinkAndAct());
            }
        }

        IEnumerator ThinkAndAct()
        {
            yield return new WaitForSeconds(reactionDelay);

            // 3D 거리 (XZ 평면만 비교)
            Vector3 selfPos = _self.transform.position;
            Vector3 oppPos  = _opponent.transform.position;
            float   dist    = Vector3.Distance(
                new Vector3(selfPos.x, 0, selfPos.z),
                new Vector3(oppPos.x,  0, oppPos.z));

            float maxHp     = _self.data != null ? _self.data.maxHealth  : 100f;
            float maxStam   = _self.data != null ? _self.data.maxStamina : 100f;
            float hpRatio   = _self.CurrentHP      / maxHp;
            float stamRatio = _self.CurrentStamina / maxStam;
            bool  oppAtk    = _opponent.State == FighterState.Attacking;

            // ── 가드 판단 ─────────────────────────────────────────────────
            bool shouldBlock = behaviour switch {
                AIBehaviour.Technical  => oppAtk && hpRatio < 0.65f,
                AIBehaviour.Balanced   => oppAtk && Random.value > 0.5f,
                AIBehaviour.Aggressive => oppAtk && Random.value > 0.8f,
                _                      => false
            };

            if (shouldBlock && !_isBlocking)
            {
                _isBlocking = true;
                _self.StartBlock();
                yield return new WaitForSeconds(Random.Range(0.2f, 0.45f));
                _self.StopBlock();
                _isBlocking = false;
                yield break;
            }

            // ── 회피 판단 ─────────────────────────────────────────────────
            if (oppAtk && !shouldBlock && stamRatio > 0.2f && Random.value > 0.55f)
            {
                // 상대 반대 방향으로 회피
                Vector3 awayDir = (selfPos - oppPos).normalized;
                awayDir.y = 0f;
                // 약간 측면으로 섞어서 자연스럽게
                Vector3 sideDir = new Vector3(-awayDir.z, 0, awayDir.x);
                Vector3 dodgeDir = (awayDir + sideDir * Random.Range(-0.5f, 0.5f)).normalized;
                _self.Dodge(dodgeDir);
                yield break;
            }

            // ── 이동 판단 ─────────────────────────────────────────────────
            float ideal = 1.6f;
            float moveH = 0f, moveV = 0f;

            if (dist > ideal + 0.4f)
            {
                // 상대에게 접근
                Vector3 dir = (oppPos - selfPos).normalized;
                moveH = dir.x;
                moveV = dir.z;
            }
            else if (dist < ideal - 0.4f)
            {
                // 상대에게서 후퇴
                Vector3 dir = (selfPos - oppPos).normalized;
                moveH = dir.x;
                moveV = dir.z;
            }
            else if (behaviour == AIBehaviour.Technical)
            {
                // 기술형: 측면으로 계속 이동 (서클링)
                Vector3 side = new Vector3(
                    -(oppPos.z - selfPos.z),
                    0f,
                     (oppPos.x - selfPos.x)).normalized;
                moveH = side.x * 0.6f;
                moveV = side.z * 0.6f;
            }

            _self.Move(moveH, moveV);

            // ── 공격 판단 ─────────────────────────────────────────────────
            if (dist <= ideal + 0.3f && stamRatio > 0.15f)
            {
                float roll = Random.value;

                float specialCost = _self.data != null ? _self.data.specialStamCost : 40f;
                if (_self.IsAwakened && roll > 0.65f && _self.CurrentStamina >= specialCost)
                {
                    _self.UseSpecial();
                    yield break;
                }

                PunchType punch = behaviour switch {
                    AIBehaviour.Aggressive => roll < 0.45f ? PunchType.Hook     : PunchType.Uppercut,
                    AIBehaviour.Technical  => roll < 0.55f ? PunchType.Jab      : PunchType.Hook,
                    _                      => roll < 0.4f  ? PunchType.Jab      :
                                             roll < 0.7f  ? PunchType.Hook     : PunchType.Uppercut
                };

                // 높이 선택:
                // - 상대가 가드 중이면 하단(가드 무시)을 높은 확률로 선택
                // - Technical AI는 상단/하단 믹스업을 적극 활용
                // - 그 외에는 확률 기반으로 고름
                bool oppBlocking = _opponent.State == FighterState.Blocking;
                PunchHeight height;
                if (oppBlocking)
                {
                    height = behaviour == AIBehaviour.Technical
                        ? (Random.value > 0.3f ? PunchHeight.Low : PunchHeight.High)
                        : (Random.value > 0.45f ? PunchHeight.Low : PunchHeight.Mid);
                }
                else
                {
                    float hr = Random.value;
                    height = behaviour switch {
                        AIBehaviour.Technical  => hr < 0.35f ? PunchHeight.High :
                                                  hr < 0.65f ? PunchHeight.Mid  : PunchHeight.Low,
                        AIBehaviour.Aggressive => hr < 0.25f ? PunchHeight.High : PunchHeight.Mid,
                        _                      => hr < 0.33f ? PunchHeight.High :
                                                  hr < 0.66f ? PunchHeight.Mid  : PunchHeight.Low
                    };
                }

                _self.Punch(punch, height);
            }
        }

        void ReactToLowHealth(float ratio)
        {
            if (ratio < 0.3f && behaviour == AIBehaviour.Balanced)
                behaviour = AIBehaviour.Aggressive;
        }
    }
}
