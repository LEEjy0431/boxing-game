using System;
using System.Collections;
using UnityEngine;
using PersonalityBox.Core;

namespace PersonalityBox.Characters
{
    /// <summary>
    /// 3D 파이터 컨트롤러.
    /// XZ 평면에서 이동, 항상 상대방 방향으로 자동 회전.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class Fighter : MonoBehaviour
    {
        [Header("Data")]
        public FighterData data;

        [Header("References")]
        public Transform opponentTransform;
        public Transform punchOrigin;      // 주먹 끝 위치
        public LayerMask hitLayer;

        [Header("Movement")]
        public float moveSpeed        = 1.5f;
        public float moveStaminaCost  = 12f;   // 이동 중 초당 스태미너 소모
        public float rotationSpeed = 15f;  // 상대 방향으로 회전하는 속도

        [Header("Hit Feel")]
        public float knockbackForce  = 6f;   // 피격 시 뒤로 밀리는 세기
        public Renderer bodyRenderer;        // 히트 플래시 대상 렌더러 (InspectorAssign)

        // ── Public state ─────────────────────────────────────────────────────
        public float CurrentHP      { get; private set; }
        public float CurrentStamina { get; private set; }
        public bool  IsAwakened     { get; private set; }
        public FighterState State   { get; private set; }
        public PunchHeight  CurrentBlockHeight => _blockHeight;   // 현재 가드 방향 (AI 판단용)

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action               OnAwakened;
        public event Action               OnKO;

        // ── Private ──────────────────────────────────────────────────────────
        // data가 없을 때 사용하는 기본값 프로퍼티
        FighterData D => data != null ? data : (_fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<FighterData>()));
        FighterData _fallback;

        Animator     _anim;
        Rigidbody    _rb;
        bool         _isBlocking;
        PunchHeight  _blockHeight = PunchHeight.Mid;   // 현재 가드 방향 (상/중/하)
        float        _dodgeCooldownTimer;
        Coroutine _attackCoroutine;
        Coroutine _hitFlashCoroutine;
        float     _spawnY;          // Y 고정 기준
        Vector3   _desiredVelocity; // FixedUpdate에서 적용할 속도 (Update에서 기록)

        static readonly int AnimMoveSpeed    = Animator.StringToHash("MoveSpeed");
        static readonly int AnimBlock        = Animator.StringToHash("IsBlocking");
        static readonly int AnimJab          = Animator.StringToHash("Jab");
        static readonly int AnimHook         = Animator.StringToHash("Hook");
        static readonly int AnimUpper        = Animator.StringToHash("Uppercut");
        static readonly int AnimDodge        = Animator.StringToHash("Dodge");
        static readonly int AnimHit          = Animator.StringToHash("Hit");
        static readonly int AnimKO           = Animator.StringToHash("KO");
        static readonly int AnimAwaken       = Animator.StringToHash("Awaken");
        // 공격 높이 파라미터 (int): 0=High, 1=Mid, 2=Low
        // Animator에 "AttackHeight" int 파라미터를 추가하면 블렌드 트리로 높이별 애니메이션 지원 가능
        static readonly int AnimAttackHeight = Animator.StringToHash("AttackHeight");

        // 높이별 히트박스 Y 오프셋 (High / Mid / Low)
        static readonly float[] HeightOffsets = { 0.55f, 0f, -0.45f };

        // ── Unity lifecycle ──────────────────────────────────────────────────
        void Awake()
        {
            _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            _rb   = GetComponent<Rigidbody>();
            _rb.useGravity             = false;   // Y는 FixedUpdate에서 직접 고정
            _rb.constraints            = RigidbodyConstraints.FreezeRotation;  // Y Position 제약 제거 (이동 방해)
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.linearDamping          = 1f;      // 낮춰야 연속 이동 가능
            _rb.angularDamping         = 10f;

            // CapsuleCollider가 프리팹 기본값(center=0, height≤1)이면 자동 수정
            var col = GetComponent<CapsuleCollider>();
            if (col != null && col.height <= 1.1f)
            {
                col.height = 2f;
                col.radius = 0.35f;
                col.center = new Vector3(0f, 1f, 0f);
            }

            // ShadowsOnly로 설정된 렌더러 복원
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.enabled = true;
            }

            if (bodyRenderer == null)
                bodyRenderer = GetComponentInChildren<Renderer>();
        }

        void Start()
        {
            if (data == null)
            {
                Debug.LogWarning($"[Fighter] '{name}': FighterData가 없습니다. Inspector의 Data 필드에 Will_DATA 또는 Echo_DATA를 연결하세요.");
                CurrentHP      = 100f;
                CurrentStamina = 100f;
            }
            else
            {
                CurrentHP      = data.maxHealth;
                CurrentStamina = data.maxStamina;
            }
            State   = FighterState.Idle;

            // hitLayer가 0(Nothing)이면 Default 레이어로 초기화
            if (hitLayer == 0)
                hitLayer = ~0; // Everything — 모든 레이어에 히트 판정

            SnapToGround();   // 실제 바닥에 발을 정확히 맞춤 (떠오름/묻힘 방지)

            // opponentTransform이 없으면 씬에서 다른 Fighter를 자동으로 찾음
            if (opponentTransform == null)
            {
                foreach (var f in FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                {
                    if (f != this) { opponentTransform = f.transform; break; }
                }
            }
        }

        void Update()
        {
            if (State == FighterState.KO) return;
            RegenerateStamina();
            FaceOpponent();
            if (_dodgeCooldownTimer > 0f) _dodgeCooldownTimer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            if (_rb == null) return;
            // 회피 중에는 DodgeRoutine이 직접 속도를 제어하므로 덮어쓰지 않음
            if (State != FighterState.Dodging)
                _rb.linearVelocity = new Vector3(_desiredVelocity.x, 0f, _desiredVelocity.z);
            // Y 위치 고정 (떠오름/낙하 방지)
            var p = _rb.position;
            if (Mathf.Abs(p.y - _spawnY) > 0.001f)
                _rb.position = new Vector3(p.x, _spawnY, p.z);
        }

        /// 현재 XZ 위치에서 아래로 레이캐스트해 실제 바닥 표면에 발을 맞춘다.
        /// 캡슐 바닥(=피벗) 기준이므로 발이 바닥에 정확히 닿게 된다.
        public void SnapToGround()
        {
            Vector3 origin = new Vector3(transform.position.x,
                                         transform.position.y + 3f,
                                         transform.position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0,
                                          QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue;
            bool found = false;
            foreach (var h in hits)
            {
                // 파이터 자신/상대는 무시
                if (h.collider.GetComponentInParent<Fighter>() != null) continue;
                if (h.point.y > bestY) { bestY = h.point.y; found = true; }
            }

            if (found)
                transform.position = new Vector3(transform.position.x, bestY, transform.position.z);

            _spawnY = transform.position.y;
            if (_rb != null) _rb.position = transform.position;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <param name="horizontal">X 축 방향 (-1 ~ 1)</param>
        /// <param name="depth">Z 축 방향 (-1 ~ 1, 앞/뒤 스텝)</param>
        public void Move(float horizontal, float depth)
        {
            if (!CanAct()) return;

            // 가드/공격 중에는 제자리 (상태 보존)
            if (State == FighterState.Blocking || State == FighterState.Attacking)
            {
                _desiredVelocity = Vector3.zero;
                _anim.SetFloat(AnimMoveSpeed, 0f);
                return;
            }
            // 회피 중에는 DodgeRoutine이 속도를 직접 제어
            if (State == FighterState.Dodging)
                return;

            _desiredVelocity = new Vector3(horizontal, 0f, depth) * moveSpeed;

            float magnitude = new Vector2(horizontal, depth).magnitude;
            _anim.SetFloat(AnimMoveSpeed, magnitude);

            if (magnitude > 0.1f)
            {
                State = FighterState.Moving;
                ConsumeStamina(moveStaminaCost * Time.deltaTime);   // 이동 중 스태미너 직접 소모
            }
            else
            {
                State = IsAwakened ? FighterState.Awakened : FighterState.Idle;
            }
        }

        /// <param name="height">가드 방향 — 들어오는 공격의 높이와 일치해야 막힘 (상/중/하)</param>
        public void StartBlock(PunchHeight height = PunchHeight.Mid)
        {
            if (!CanAct()) return;
            _isBlocking  = true;
            _blockHeight = height;
            State = FighterState.Blocking;
            _anim.SetBool(AnimBlock, true);
        }

        /// 가드 중 방향 전환 (Q를 누른 채로 W/S 전환 시 호출)
        public void SetBlockHeight(PunchHeight height)
        {
            if (_isBlocking) _blockHeight = height;
        }

        public void StopBlock()
        {
            _isBlocking = false;
            if (State == FighterState.Blocking) State = FighterState.Idle;
            _anim.SetBool(AnimBlock, false);
        }

        public void Dodge(Vector3 worldDirection)
        {
            if (!CanAct() || _dodgeCooldownTimer > 0f) return;
            if (CurrentStamina < 8f) return;

            ConsumeStamina(8f);
            _dodgeCooldownTimer = D.dodgeCooldown;
            State = FighterState.Dodging;
            _anim.SetTrigger(AnimDodge);
            StartCoroutine(DodgeRoutine(worldDirection.normalized));
        }

        /// <param name="height">상단(High)/중단(Mid)/하단(Low) — 하단은 가드 무시</param>
        public void Punch(PunchType type, PunchHeight height = PunchHeight.Mid)
        {
            if (!CanAttack()) return;

            float stamCost = type switch {
                PunchType.Jab      => D.jabStamCost,
                PunchType.Hook     => D.hookStamCost,
                PunchType.Uppercut => D.upperStamCost,
                _                  => 0f
            };
            if (CurrentStamina < stamCost) return;

            ConsumeStamina(stamCost);
            State = FighterState.Attacking;

            int trigger = type switch {
                PunchType.Jab      => AnimJab,
                PunchType.Hook     => AnimHook,
                PunchType.Uppercut => AnimUpper,
                _                  => AnimJab
            };
            // AttackHeight int 파라미터로 애니메이터에 높이 전달
            _anim.SetInteger(AnimAttackHeight, (int)height);
            _anim.SetTrigger(trigger);

            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            _attackCoroutine = StartCoroutine(AttackRoutine(type, height));
        }

        /// <param name="attackHeight">공격이 들어온 높이 — 가드 방향이 이 값과 같아야 막힘</param>
        public void TakeDamage(float rawDamage, PunchHeight attackHeight)
        {
            if (State == FighterState.KO) return;

            // 가드 방향이 공격 높이와 정확히 일치할 때만 막힘 (데미지 1로 고정)
            bool blocked = _isBlocking && _blockHeight == attackHeight;
            float reduced = blocked ? 1f : rawDamage;
            CurrentHP = Mathf.Max(0f, CurrentHP - reduced);
            OnHealthChanged?.Invoke(CurrentHP, D.maxHealth);
            _anim.SetTrigger(AnimHit);

            // 넉백: 막지 못했을 때만, 수평 방향으로만 (Y 고정)
            if (!blocked && opponentTransform != null)
            {
                Vector3 pushDir = (transform.position - opponentTransform.position);
                pushDir.y = 0f;
                pushDir = pushDir.normalized;
                _rb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
            }

            // 히트 플래시
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlashCo());

            // 타격감: 히트스톱 + 카메라 쉐이크
            HitFeedback.Instance?.TriggerHit();

            CheckAwakening();
            if (CurrentHP <= 0f) TriggerKO();
        }

        /// MatchManager가 라운드 리셋 시 호출
        public void ResetFighter()
        {
            CurrentHP      = D.maxHealth;
            CurrentStamina = D.maxStamina;
            IsAwakened     = false;
            State          = FighterState.Idle;
            _isBlocking    = false;
            _desiredVelocity = Vector3.zero;
            _rb.linearVelocity = Vector3.zero;
            SnapToGround();   // 라운드 시작 위치를 바닥에 맞추고 _spawnY 갱신
            OnHealthChanged?.Invoke(CurrentHP, D.maxHealth);
            OnStaminaChanged?.Invoke(CurrentStamina, D.maxStamina);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        bool CanAct()    => State != FighterState.KO && State != FighterState.KnockedDown;
        bool CanAttack() => CanAct() && State != FighterState.Attacking && State != FighterState.Dodging;

        /// 항상 상대방을 바라보도록 Y축 회전
        void FaceOpponent()
        {
            if (opponentTransform == null) return;
            Vector3 dir = opponentTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation  = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        void RegenerateStamina()
        {
            // 이동 중에는 회복하지 않음 (소모는 Move()에서 직접 처리)
            if (State == FighterState.Moving) return;

            // 가만히 있거나 가드 중이면 회복 (가드 시 1.5배 빠르게)
            if (CurrentStamina >= D.maxStamina) return;
            float regenRate = _isBlocking ? D.staminaRegen * 1.5f : D.staminaRegen;
            CurrentStamina = Mathf.Min(D.maxStamina, CurrentStamina + regenRate * Time.deltaTime);
            OnStaminaChanged?.Invoke(CurrentStamina, D.maxStamina);
        }

        void ConsumeStamina(float amount)
        {
            CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
            OnStaminaChanged?.Invoke(CurrentStamina, D.maxStamina);
        }

        void CheckAwakening()
        {
            if (IsAwakened) return;
            if (CurrentHP / D.maxHealth <= D.awakenThreshold)
            {
                IsAwakened = true;
                _anim.SetTrigger(AnimAwaken);
                if (D.awakenRestoresStam)
                {
                    CurrentStamina = D.maxStamina;
                    OnStaminaChanged?.Invoke(CurrentStamina, D.maxStamina);
                }
                OnAwakened?.Invoke();
            }
        }

        void TriggerKO()
        {
            State = FighterState.KO;
            _rb.linearVelocity = Vector3.zero;
            _anim.SetTrigger(AnimKO);
            OnKO?.Invoke();
        }

        float GetPunchDamage(PunchType? type)
        {
            float base_ = type switch {
                PunchType.Jab      => D.jabDamage,
                PunchType.Hook     => D.hookDamage,
                PunchType.Uppercut => D.upperDamage,
                _                  => 0f
            };
            return base_ * (IsAwakened ? D.awakenDamageBonus : 1f);
        }

        // ── Coroutines ───────────────────────────────────────────────────────

        IEnumerator AttackRoutine(PunchType? type, PunchHeight height = PunchHeight.Mid)
        {
            // 주먹 트레일 시작
            var trail = punchOrigin != null ? punchOrigin.GetComponent<TrailRenderer>() : null;
            if (trail != null) { trail.Clear(); trail.emitting = true; }

            yield return new WaitForSeconds(0.15f);

            // 높이별 히트박스 기준점 계산
            // High=+0.55, Mid=0, Low=-0.45 (단위: 월드 Y)
            float yOffset = HeightOffsets[(int)height];
            Vector3 hitOrigin = (punchOrigin != null ? punchOrigin.position : transform.position) + Vector3.up * yOffset;
            float reach = 1.6f;

            // ① 상대를 직접 거리 체크 (가장 확실한 판정) ─────────────────
            bool landed = false;
            if (opponentTransform != null)
            {
                Vector3 a = transform.position; a.y = 0f;
                Vector3 b = opponentTransform.position; b.y = 0f;
                float dist = Vector3.Distance(a, b);
                // 상대가 사거리 안 + 대략 앞쪽(내적>0)이면 명중
                Vector3 toOpp = (b - a).normalized;
                bool inFront = Vector3.Dot(transform.forward, toOpp) > 0.3f;
                if (dist <= reach + 0.6f && inFront)
                {
                    var oppFighter = opponentTransform.GetComponentInParent<Fighter>();
                    if (oppFighter != null && oppFighter != this)
                    {
                        oppFighter.TakeDamage(GetPunchDamage(type), height);
                        landed = true;
                    }
                }
            }

            // ② 보조: OverlapSphere (혹시 모를 추가 타겟)
            if (!landed)
            {
                Collider[] hits = Physics.OverlapSphere(hitOrigin, reach, hitLayer);
                foreach (var col in hits)
                {
                    if (col.transform.IsChildOf(transform) || col.transform == transform) continue;
                    var target = col.GetComponentInParent<Fighter>();
                    if (target != null && target != this)
                    {
                        target.TakeDamage(GetPunchDamage(type), height);
                        break;
                    }
                }
            }

            yield return new WaitForSeconds(0.25f);

            // 주먹 트레일 종료
            if (trail != null) trail.emitting = false;

            if (State == FighterState.Attacking)
                State = IsAwakened ? FighterState.Awakened : FighterState.Idle;
        }

        IEnumerator DodgeRoutine(Vector3 dir)
        {
            float elapsed = 0f, duration = 0.28f;
            while (elapsed < duration)
            {
                _rb.linearVelocity = new Vector3(dir.x * 9f, 0f, dir.z * 9f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _rb.linearVelocity = Vector3.zero;
            if (State == FighterState.Dodging)
                State = IsAwakened ? FighterState.Awakened : FighterState.Idle;
        }

        IEnumerator HitFlashCo()
        {
            if (bodyRenderer == null) yield break;
            var mpb = new MaterialPropertyBlock();
            Color flashColor = new Color(3f, 0f, 0f);
            mpb.SetColor("_BaseColor", flashColor);       // URP
            mpb.SetColor("_EmissionColor", flashColor * 0.6f);
            bodyRenderer.SetPropertyBlock(mpb);

            yield return new WaitForSecondsRealtime(0.10f);

            bodyRenderer.SetPropertyBlock(new MaterialPropertyBlock());
            _hitFlashCoroutine = null;
        }

        void OnDrawGizmosSelected()
        {
            if (punchOrigin == null) return;
            Color[] colors = { Color.yellow, Color.red, Color.cyan };
            string[] labels = { "High", "Mid", "Low" };
            for (int i = 0; i < 3; i++)
            {
                Gizmos.color = colors[i];
                Gizmos.DrawWireSphere(punchOrigin.position + Vector3.up * HeightOffsets[i], 1.1f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(punchOrigin.position + Vector3.up * HeightOffsets[i], labels[i]);
#endif
            }
        }
    }
}
