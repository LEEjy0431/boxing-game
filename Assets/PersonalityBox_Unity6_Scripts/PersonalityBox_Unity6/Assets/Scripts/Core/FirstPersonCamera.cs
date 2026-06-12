using UnityEngine;
using UnityEngine.Rendering;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// Fighter1 시점의 1인칭 카메라.
    /// fighter1 필드가 비어 있으면 씬에서 PlayerInputHandler가 활성화된 Fighter를 자동 탐색.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("대상 파이터")]
        public Fighter fighter1;

        [Header("눈 위치")]
        [Tooltip("발바닥 기준 눈 높이 (캡슐 키의 약 80~85%)")]
        public float eyeHeight        = 1.65f;
        [Tooltip("눈을 살짝 앞으로 밀어 클리핑 방지")]
        public float eyeForwardOffset = 0.12f;
        [Tooltip("수직 시선 각도 — 음수일수록 아래(상대 몸통 기준)")]
        public float verticalAngle    = -8f;

        [Header("헤드 밥 (이동 중 상하 흔들림)")]
        public bool  headBob      = true;
        public float bobFrequency = 9f;
        public float bobAmplitude = 0.035f;

        float   _bobTimer;
        Vector3 _shakeOffset;
        float   _searchTimer;

        void Start()
        {
            TryFindFighter();
            SetPlayerVisible(false);
        }

        void OnDestroy() => SetPlayerVisible(true);

        void SetPlayerVisible(bool visible)
        {
            if (fighter1 == null) return;
            var mode = visible
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in fighter1.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = mode;
        }

        void LateUpdate()
        {
            // fighter1이 없으면 0.5초마다 재탐색 (씬 간 참조 문제 대응)
            if (fighter1 == null)
            {
                _searchTimer -= Time.deltaTime;
                if (_searchTimer <= 0f)
                {
                    _searchTimer = 0.5f;
                    TryFindFighter();
                }
                if (fighter1 == null) return;
            }

            // 헤드 밥 타이머
            bool moving = fighter1.State == FighterState.Moving;
            if (headBob)
            {
                if (moving) _bobTimer += Time.deltaTime * bobFrequency;
                else         _bobTimer  = Mathf.Lerp(_bobTimer, 0f, Time.deltaTime * 12f);
            }

            float bobY = headBob ? Mathf.Sin(_bobTimer) * bobAmplitude : 0f;

            Vector3 eye = fighter1.transform.position
                        + Vector3.up    * (eyeHeight + bobY)
                        + fighter1.transform.forward * eyeForwardOffset;

            transform.position = eye + _shakeOffset;

            float yaw = fighter1.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(verticalAngle, yaw, 0f);
        }

        void TryFindFighter()
        {
            if (fighter1 != null) return;

            foreach (var f in FindObjectsByType<Fighter>(FindObjectsSortMode.None))
            {
                var ph = f.GetComponent<PlayerInputHandler>();
                if (ph != null && ph.enabled)
                {
                    fighter1 = f;
                    return;
                }
            }
        }

        /// HitFeedback.cs 에서 카메라 흔들림을 줄 때 호출
        public void SetShakeOffset(Vector3 offset) => _shakeOffset = offset;
    }
}
