using UnityEngine;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 파이터가 링(로프) 밖으로 나가지 못하도록 막는 경계 스크립트.
    /// useRectangle=true면 실제 로프 사각형 기준으로 막음 (Building의 Corner 오브젝트 기준).
    /// false면 기존 원형 경계 사용.
    /// </summary>
    public class RingBoundary : MonoBehaviour
    {
        [Header("원형 모드 (useRectangle=false일 때)")]
        public float ringRadius = 5f;

        [Header("사각형 모드 (실제 로프 기준, 권장)")]
        public bool useRectangle = false;
        public Vector2 rectHalfExtents = new Vector2(3f, 3f);

        public Fighter[] fighters;

        void Start()
        {
            if (fighters == null || fighters.Length == 0)
                fighters = FindObjectsByType<Fighter>(FindObjectsSortMode.None);
        }

        void LateUpdate()
        {
            if (fighters == null) return;
            foreach (var f in fighters)
            {
                if (f == null) continue;
                Vector3 local = f.transform.position - transform.position;

                if (useRectangle)
                {
                    float cx = Mathf.Clamp(local.x, -rectHalfExtents.x, rectHalfExtents.x);
                    float cz = Mathf.Clamp(local.z, -rectHalfExtents.y, rectHalfExtents.y);
                    if (!Mathf.Approximately(cx, local.x) || !Mathf.Approximately(cz, local.z))
                    {
                        f.transform.position = new Vector3(
                            transform.position.x + cx,
                            f.transform.position.y,
                            transform.position.z + cz);
                    }
                }
                else
                {
                    Vector3 offset = local; offset.y = 0f;
                    if (offset.magnitude > ringRadius)
                    {
                        Vector3 clamped = offset.normalized * ringRadius;
                        f.transform.position = new Vector3(
                            transform.position.x + clamped.x,
                            f.transform.position.y,
                            transform.position.z + clamped.z);
                    }
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            if (useRectangle)
            {
                Vector3 c = transform.position;
                Vector3 a = c + new Vector3(-rectHalfExtents.x, 0,  rectHalfExtents.y);
                Vector3 b = c + new Vector3( rectHalfExtents.x, 0,  rectHalfExtents.y);
                Vector3 d = c + new Vector3( rectHalfExtents.x, 0, -rectHalfExtents.y);
                Vector3 e = c + new Vector3(-rectHalfExtents.x, 0, -rectHalfExtents.y);
                Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, d);
                Gizmos.DrawLine(d, e); Gizmos.DrawLine(e, a);
            }
            else
            {
                DrawCircle(transform.position, ringRadius, 40);
            }
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
