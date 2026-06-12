using System.Collections;
using UnityEngine;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 타격감 연출 — 히트스톱 + 카메라 쉐이크
    /// GameManager 오브젝트에 Add Component로 추가.
    /// Fighter.cs의 TakeDamage에서 자동으로 호출됨.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance { get; private set; }

        [Header("히트스톱 (화면이 잠깐 멈추는 효과)")]
        public float normalHitStop  = 0.07f;   // 일반 펀치
        public float specialHitStop = 0.18f;   // 필살기

        [Header("카메라 흔들림")]
        public float shakeDuration  = 0.20f;
        public float shakeMagnitude = 0.15f;

        Camera           _cam;
        FirstPersonCamera _fpCam;
        bool             _shaking;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Start에서 찾아야 다른 오브젝트가 초기화된 후 카메라를 찾을 수 있음
            RefreshCamera();
        }

        void RefreshCamera()
        {
            _cam   = Camera.main;
            _fpCam = _cam != null ? _cam.GetComponent<FirstPersonCamera>() : null;
        }

        /// Fighter.cs의 TakeDamage에서 자동 호출
        public void TriggerHit(bool isSpecial = false)
        {
            // 카메라가 아직 없으면 재탐색 (씬 로드 타이밍 방어)
            if (_cam == null) RefreshCamera();

            StopAllCoroutines();
            StartCoroutine(HitStopCo(isSpecial ? specialHitStop : normalHitStop));
            if (!_shaking && _cam != null) StartCoroutine(ShakeCo());
        }

        IEnumerator HitStopCo(float duration)
        {
            Time.timeScale      = 0.04f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale      = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        IEnumerator ShakeCo()
        {
            if (_cam == null) { _shaking = false; yield break; }
            _shaking = true;
            Vector3 origin3P = (_fpCam == null) ? _cam.transform.localPosition : Vector3.zero;
            float   elapsed  = 0f;

            while (elapsed < shakeDuration)
            {
                float   t      = 1f - (elapsed / shakeDuration);
                Vector3 offset = (Vector3)(Random.insideUnitCircle * shakeMagnitude * t);

                if (_fpCam != null) _fpCam.SetShakeOffset(offset);
                else                 _cam.transform.localPosition = origin3P + offset;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_fpCam != null) _fpCam.SetShakeOffset(Vector3.zero);
            else                 _cam.transform.localPosition = origin3P;
            _shaking = false;
        }
    }
}
