using System.Collections;
using UnityEngine;
using PersonalityBox.Core;

namespace PersonalityBox.Characters
{
    /// <summary>
    /// 애니메이션 클립 없이 코드로 동작하는 절차적 파이터 모션.
    /// Fighter와 같은 오브젝트에 추가하면 됩니다.
    ///
    /// 제공하는 모션:
    ///   - 숨쉬기 (Idle 상하 진동)
    ///   - 펀치 앞쏠림 (Jab/Hook/Uppercut 구분)
    ///   - 피격 뒤쏠림
    ///   - 가드 앞경사
    ///   - KO 쓰러짐
    /// </summary>
    [RequireComponent(typeof(Fighter))]
    public class FighterBodyAnim : MonoBehaviour
    {
        [Header("Mesh Root (자동 탐색, 없으면 자동)")]
        [Tooltip("SkinnedMeshRenderer가 있는 자식 Transform. 비워두면 자동 탐색.")]
        public Transform meshRoot;

        [Header("Animator 우선")]
        [Tooltip("체크 시 Animator 클립이 있으면 절차적 모션을 비활성화합니다.")]
        public bool deferToAnimator = true;

        [Header("Idle 숨쉬기")]
        public float breathAmplitude = 0.025f;
        public float breathSpeed     = 1.2f;

        [Header("Punch Lunge")]
        public float jab_ForwardZ    =  0.30f;
        public float hook_RotY       = 18f;
        public float upper_RotX      = -12f;
        public float punchDuration   =  0.22f;

        [Header("Hit React")]
        public float hit_BackZ       = -0.22f;
        public float hit_Duration    =  0.18f;

        [Header("Block Lean")]
        public float block_ForwardZ  =  0.10f;
        public float block_RotX      =  8f;

        // ── 내부 상태 ────────────────────────────────────────────────────────
        Fighter      _fighter;
        Animator     _animator;
        FighterState _prevState;
        Vector3      _restPos;
        Quaternion   _restRot;
        bool         _inAnim;
        float        _breathTimer;

        void Start()
        {
            _fighter  = GetComponent<Fighter>();
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

            // meshRoot 자동 탐색
            if (meshRoot == null)
                meshRoot = FindMeshRoot();

            if (meshRoot == null)
            {
                Debug.LogWarning($"[FighterBodyAnim] {name}: meshRoot를 찾지 못했습니다. Inspector에서 직접 연결하세요.");
                _fighter.OnHealthChanged += OnHealthChanged;
                return;
            }

            _restPos = meshRoot.localPosition;
            _restRot = meshRoot.localRotation;

            // TakeDamage 이벤트 구독
            _fighter.OnHealthChanged += OnHealthChanged;
        }

        void OnDestroy()
        {
            if (_fighter != null)
                _fighter.OnHealthChanged -= OnHealthChanged;
        }

        // meshRoot 자동 탐색 헬퍼 — 가장 가까운 모델 루트 자식을 반환
        Transform FindMeshRoot()
        {
            // ① SkinnedMeshRenderer 탐색
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                // SMR이 직접 자식이면 그 transform, 아니면 그 부모
                Transform t = smr.transform;
                while (t.parent != null && t.parent != transform)
                    t = t.parent;
                return t;
            }
            // ② MeshRenderer 탐색
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                Transform t = mr.transform;
                while (t.parent != null && t.parent != transform)
                    t = t.parent;
                return t;
            }
            return null;
        }

        // Animator에 활성 클립이 있으면 절차적 모션을 건너뜀
        bool AnimatorHasClips()
        {
            if (!deferToAnimator || _animator == null) return false;
            var ctrl = _animator.runtimeAnimatorController;
            if (ctrl == null) return false;
            return ctrl.animationClips != null && ctrl.animationClips.Length > 0;
        }

        void Update()
        {
            // Animator 클립이 있으면 절차적 모션 비활성 (Animator가 우선)
            if (AnimatorHasClips()) return;

            // 늦게 추가된 메시도 찾을 수 있게 재시도
            if (meshRoot == null)
            {
                meshRoot = FindMeshRoot();
                if (meshRoot != null)
                {
                    _restPos = meshRoot.localPosition;
                    _restRot = meshRoot.localRotation;
                }
                return;
            }

            var state = _fighter.State;

            // ── 상태 전환 감지 ───────────────────────────────────────────
            if (state != _prevState)
            {
                OnStateChanged(_prevState, state);
                _prevState = state;
            }

            // ── Idle 숨쉬기 ─────────────────────────────────────────────
            if (!_inAnim && (state == FighterState.Idle || state == FighterState.Moving || state == FighterState.Awakened))
            {
                _breathTimer += Time.deltaTime * breathSpeed;
                float bobY = Mathf.Sin(_breathTimer) * breathAmplitude;
                meshRoot.localPosition = _restPos + new Vector3(0f, bobY, 0f);
            }

            // ── 가드 자세 ────────────────────────────────────────────────
            if (!_inAnim && state == FighterState.Blocking)
            {
                meshRoot.localPosition = Vector3.Lerp(meshRoot.localPosition,
                    _restPos + new Vector3(0f, 0f, block_ForwardZ), Time.deltaTime * 10f);
                meshRoot.localRotation = Quaternion.Slerp(meshRoot.localRotation,
                    _restRot * Quaternion.Euler(block_RotX, 0f, 0f), Time.deltaTime * 10f);
            }
        }

        // ── 상태 전환 핸들러 ─────────────────────────────────────────────────
        void OnStateChanged(FighterState from, FighterState to)
        {
            if (to == FighterState.Attacking)
            {
                // 어떤 펀치인지 마지막 애니메이터 파라미터로 구분할 수 없으므로
                // Animator의 AttackHeight int로 판단 (0=High/1=Mid/2=Low)
                var anim = GetComponentInChildren<Animator>();
                int height = anim != null ? anim.GetInteger("AttackHeight") : 1;
                StartPunchAnim(height);
            }

            if ((from == FighterState.Blocking) &&
                (to == FighterState.Idle || to == FighterState.Moving))
            {
                StartCoroutine(ReturnToRest(0.2f));
            }

            if (to == FighterState.KO)
                StartCoroutine(KOAnim());
        }

        // 체력 감소 = 피격 반응
        void OnHealthChanged(float hp, float max)
        {
            if (!_inAnim)
                StartCoroutine(HitReactAnim());
        }

        // ── 애니메이션 코루틴들 ───────────────────────────────────────────────

        void StartPunchAnim(int heightIdx)
        {
            if (_inAnim) return;

            // height 0=High → 약간 위로 + 앞
            // height 1=Mid  → 앞으로 (잽/훅)
            // height 2=Low  → 약간 아래 + 앞
            float rotX = heightIdx == 0 ? upper_RotX : (heightIdx == 2 ? -upper_RotX * 0.5f : 0f);

            StartCoroutine(PunchAnim(rotX));
        }

        IEnumerator PunchAnim(float rotX)
        {
            _inAnim = true;
            float half = punchDuration * 0.4f;

            // 전진
            yield return LerpPosRot(
                _restPos + new Vector3(0f, 0f, jab_ForwardZ),
                _restRot * Quaternion.Euler(rotX, 0f, 0f),
                half);

            // 복귀
            yield return LerpPosRot(_restPos, _restRot, punchDuration * 0.6f);

            _inAnim = false;
        }

        IEnumerator HitReactAnim()
        {
            _inAnim = true;

            // 뒤로 밀림
            yield return LerpPosRot(
                _restPos + new Vector3(0f, 0f, hit_BackZ),
                _restRot * Quaternion.Euler(-8f, 0f, 0f),
                hit_Duration * 0.3f);

            // 복귀
            yield return LerpPosRot(_restPos, _restRot, hit_Duration * 0.7f);

            _inAnim = false;
        }

        IEnumerator KOAnim()
        {
            _inAnim = true;
            yield return LerpPosRot(
                _restPos + new Vector3(0f, -0.5f, -0.8f),
                _restRot * Quaternion.Euler(-70f, 0f, 0f),
                0.5f);
            // KO 상태에선 복귀 안 함
        }

        IEnumerator ReturnToRest(float duration)
        {
            if (!_inAnim)
                yield return LerpPosRot(_restPos, _restRot, duration);
        }

        IEnumerator LerpPosRot(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            if (meshRoot == null) yield break;
            Vector3    startPos = meshRoot.localPosition;
            Quaternion startRot = meshRoot.localRotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                // EaseInOut
                t = t * t * (3f - 2f * t);
                meshRoot.localPosition = Vector3.Lerp(startPos, targetPos, t);
                meshRoot.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            meshRoot.localPosition = targetPos;
            meshRoot.localRotation = targetRot;
        }
    }
}
