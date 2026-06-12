using UnityEngine;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 파이터가 링 밖으로 나가지 못하도록 막는 경계 스크립트.
    /// 링 중심 오브젝트에 부착. ringRadius = 링 반지름 (단위: 유니티 미터).
    /// </summary>
    public class RingBoundary : MonoBehaviour
    {
        public float ringRadius = 5f;
        public Fighter[] fighters;

        void Start()
        {
            // Inspector에서 연결 안 됐으면 씬에서 자동 탐색
            if (fighters == null || fighters.Length == 0)
                fighters = FindObjectsByType<Fighter>(FindObjectsSortMode.None);
        }

        void LateUpdate()
        {
            if (fighters == null) return;
            foreach (var f in fighters)
            {
                if (f == null) continue;

                Vector3 offset = f.transform.position - transform.position;
                offset.y = 0f;

                if (offset.magnitude > ringRadius)
                {
                    // 경계 안으로 밀어 넣기
                    Vector3 clamped = offset.normalized * ringRadius;
                    f.transform.position = new Vector3(
                        transform.position.x + clamped.x,
                        f.transform.position.y,
                        transform.position.z + clamped.z);
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawCircle(transform.position, ringRadius, 40);
        }

        void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angle = 0f;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
