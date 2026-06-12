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
        public float moveSpeed     = 1.5f;
        public float rotationSpeed = 15f;  // 상대 방향으로 회전하는 속도

        [Header("Hit Feel")]
        public float knockbackForce  = 6f;   // 피격 시 뒤로 밀리는 세기
        public Renderer bodyRenderer;        // 히트 플래시 대상 렌더러 (InspectorAssign)

        // ── Public state ─────────────────────────────────────────────────────
        public float CurrentHP      { get; private set; }
        public float CurrentStamina { get; private set; }
        public bool  IsAwakened     { get; private set; }
        public FighterState State   { get; private set; }

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action               OnAwakened;
        public event Action               OnKO;

        // ── Private ──────────────────────────────────────────────────────────
        // data가 없을 때 사용하는 기본값 프로퍼티
        FighterData D => data != null ? data : (_fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<FighterData>()));
        FighterData _fallback;

        Animator  _anim;
        Rigidbody _rb;
        bool      _isBlocking;
        float     _dodgeCooldownTimer;
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
        static readonly int AnimSpecial      = Animator.StringToHash("Special");
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
            _spawnY = transform.position.y;   // 시작 높이 기록

            // hitLayer가 0(Nothing)이면 Default 레이어로 초기화
            if (hitLayer == 0)
                hitLayer = ~0; // Everything — 모든 레이어에 히트 판정

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
            // Update에서 기록한 이동 속도를 물리 타이밍에 맞춰 적용
            _rb.linearVelocity = new Vector3(_desiredVelocity.x, 0f, _desiredVelocity.z);
            // Y 위치 고정
            var p = _rb.position;
            if (Mathf.Abs(p.y - _spawnY) > 0.001f)
                _rb.position = new Vector3(p.x, _spawnY, p.z);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <param name="horizontal">X 축 방향 (-1 ~ 1)</param>
        /// <param name="depth">Z 축 방향 (-1 ~ 1, 앞/뒤 스텝)</param>
        public void Move(float horizontal, float depth)
        {
            if (!CanAct()) return;

            // 속도는 여기서 기록만 하고, 실제 적용은 FixedUpdate에서 처리
            _desiredVelocity = new Vector3(horizontal, 0f, depth) * moveSpeed;

            float magnitude = new Vector2(horizontal, depth).magnitude;
            _anim.SetFloat(AnimMoveSpeed, magnitude);
            State = magnitude > 0.1f ? FighterState.Moving : FighterState.Idle;
        }

        public void StartBlock()
        {
            if (!CanAct()) return;
            _isBlocking = true;
            State = FighterState.Blocking;
            _anim.SetBool(AnimBlock, true);
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
            _attackCoroutine = StartCoroutine(AttackRoutine(type, false, height));
        }

        public void UseSpecial()
        {
            if (!CanAttack() || CurrentStamina < D.specialStamCost) return;
            ConsumeStamina(D.specialStamCost);
            State = FighterState.Attacking;
            _anim.SetTrigger(AnimSpecial);
            StartCoroutine(AttackRoutine(null, true, PunchHeight.Mid));
        }

        /// <param name="bypassBlock">true이면 가드 무시 (하단 공격)</param>
        public void TakeDamage(float rawDamage, bool isSpecial = false, bool bypassBlock = false)
        {
            if (State == FighterState.KO) return;
            float reduced = (_isBlocking && !bypassBlock) ? rawDamage * (1f - D.blockDamageReduction) : rawDamage;
            CurrentHP = Mathf.Max(0f, CurrentHP - reduced);
            OnHealthChanged?.Invoke(CurrentHP, D.maxHealth);
            _anim.SetTrigger(AnimHit);

            // 넉백: 수평 방향만 (Y 고정이므로 수직 힘 없음)
            if (!_isBlocking && opponentTransform != null)
            {
                Vector3 pushDir = (transform.position - opponentTransform.position);
                pushDir.y = 0f;
                pushDir = pushDir.normalized;
                _rb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
            }

            // 히트 플래시
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlashCo(isSpecial));

            // 타격감: 히트스톱 + 카메라 쉐이크
            HitFeedback.Instance?.TriggerHit(isSpecial);

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
            _rb.linearVelocity = Vector3.zero;
            _spawnY = transform.position.y;   // 라운드 시작 위치를 새 기준으로
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
            // 이동 중 스태미너 소모 (초당 15 — 약 6초에 완전 소모)
            if (State == FighterState.Moving)
            {
                ConsumeStamina(15f * Time.deltaTime);
                return;
            }

            // 가드 중이거나 가만히 있으면 회복 (가드 시 1.5배 빠르게)
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

        float GetPunchDamage(PunchType? type, bool isSpecial)
        {
            if (isSpecial) return D.specialDamage * (IsAwakened ? D.awakenDamageBonus : 1f);
            float base_ = type switch {
                PunchType.Jab      => D.jabDamage,
                PunchType.Hook     => D.hookDamage,
                PunchType.Uppercut => D.upperDamage,
                _                  => 0f
            };
            return base_ * (IsAwakened ? D.awakenDamageBonus : 1f);
        }

        // ── Coroutines ───────────────────────────────────────────────────────

        IEnumerator AttackRoutine(PunchType? type, bool isSpecial, PunchHeight height = PunchHeight.Mid)
        {
            // 주먹 트레일 시작
            var trail = punchOrigin != null ? punchOrigin.GetComponent<TrailRenderer>() : null;
            if (trail != null) { trail.Clear(); trail.emitting = true; }

            yield return new WaitForSeconds(isSpecial ? 0.3f : 0.15f);

            // 높이별 히트박스 기준점 계산
            // High=+0.55, Mid=0, Low=-0.45 (단위: 월드 Y)
            float yOffset = isSpecial ? 0f : HeightOffsets[(int)height];
            Vector3 hitOrigin = (punchOrigin != null ? punchOrigin.position : transform.position) + Vector3.up * yOffset;

            // 하단 공격은 가드 무시
            bool bypassBlock = !isSpecial && height == PunchHeight.Low;

            float reach = isSpecial ? 1.8f : 1.1f;
            Collider[] hits = Physics.OverlapSphere(hitOrigin, reach, hitLayer);
            foreach (var col in hits)
            {
                if (col.transform.IsChildOf(transform) || col.transform == transform) continue;
                var target = col.GetComponentInParent<Fighter>();
                if (target != null)
                    target.TakeDamage(GetPunchDamage(type, isSpecial), isSpecial, bypassBlock);
            }

            yield return new WaitForSeconds(isSpecial ? 0.5f : 0.25f);

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

        IEnumerator HitFlashCo(bool isSpecial)
        {
            if (bodyRenderer == null) yield break;
            var mpb = new MaterialPropertyBlock();
            // 일반 타격은 빨강, 필살기는 주황
            Color flashColor = isSpecial ? new Color(3f, 0.6f, 0f) : new Color(3f, 0f, 0f);
            mpb.SetColor("_BaseColor", flashColor);       // URP
            mpb.SetColor("_EmissionColor", flashColor * 0.6f);
            bodyRenderer.SetPropertyBlock(mpb);

            yield return new WaitForSecondsRealtime(isSpecial ? 0.18f : 0.10f);

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
