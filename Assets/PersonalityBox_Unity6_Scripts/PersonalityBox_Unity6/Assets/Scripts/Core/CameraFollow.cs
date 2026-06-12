using UnityEngine;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 두 파이터 사이 중심점을 비스듬히 내려다보는 3D 시네마틱 카메라.
    /// 파이터 간격에 따라 FOV와 거리를 자동 조정.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Fighter fighter1;
        public Fighter fighter2;

        [Header("Position")]
        public float height       = 4.5f;    // 중심점 위 높이
        public float distance     = 9f;      // 중심점 뒤 거리 (Z-)
        public float smoothTime   = 0.18f;

        [Header("FOV")]
        public float minFOV       = 48f;
        public float maxFOV       = 68f;
        public float fovSmoothTime = 0.2f;

        [Header("Look target")]
        public float lookHeightOffset = 1.2f;  // 캐릭터 중심보다 약간 위를 바라봄

        Camera _cam;
        Vector3 _posVel;
        float   _fovVel;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (fighter1 == null || fighter2 == null) return;

            Vector3 p1  = fighter1.transform.position;
            Vector3 p2  = fighter2.transform.position;
            Vector3 mid = (p1 + p2) * 0.5f;
            float   dist = Vector3.Distance(p1, p2);

            // 카메라 목표 위치 (중심 뒤&위)
            Vector3 targetPos = mid + new Vector3(0f, height, -(distance + dist * 0.25f));
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, smoothTime);

            // 중심점의 약간 위를 바라봄
            transform.LookAt(mid + Vector3.up * lookHeightOffset);

            // 거리에 따라 FOV 조정
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, Mathf.Clamp01(dist / 7f));
            _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, targetFOV, ref _fovVel, fovSmoothTime);
        }
    }
}
