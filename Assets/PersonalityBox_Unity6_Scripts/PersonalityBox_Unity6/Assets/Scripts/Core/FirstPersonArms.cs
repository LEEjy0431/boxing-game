using UnityEngine;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 1인칭 시점에서 P1 팔(글러브)을 화면 하단에 표시.
    /// Main Camera에 부착. Start()에서 자동으로 글러브 메시를 생성.
    /// </summary>
    public class FirstPersonArms : MonoBehaviour
    {
        [Header("글러브 색상")]
        public Color gloveColor = new Color(0.15f, 0.15f, 0.7f);   // 파란 글러브

        // 로컬 좌표 기준 rest/punch 위치
        static readonly Vector3 LeftRest   = new Vector3(-0.26f, -0.30f, 0.50f);
        static readonly Vector3 RightRest  = new Vector3( 0.26f, -0.30f, 0.50f);
        static readonly Vector3 PunchFwd   = new Vector3( 0.08f, -0.22f, 0.80f);
        static readonly Vector3 PunchHook  = new Vector3( 0.35f, -0.22f, 0.65f);
        static readonly Vector3 PunchUpper = new Vector3( 0.10f, -0.10f, 0.72f);
        static readonly Vector3 GuardL     = new Vector3(-0.16f, -0.18f, 0.62f);
        static readonly Vector3 GuardR     = new Vector3( 0.16f, -0.18f, 0.62f);

        Transform _left;
        Transform _right;
        Fighter   _fighter;
        FighterState _prevState;

        void Start()
        {
            _left  = MakeGlove("Glove_Left",  LeftRest);
            _right = MakeGlove("Glove_Right", RightRest);

            // Player fighter 탐색
            var go = GameObject.Find("Fighter1_Robert");
            if (go == null) go = GameObject.Find("Fighter1");
            if (go != null) _fighter = go.GetComponent<Fighter>();
        }

        void Update()
        {
            if (_fighter == null)
            {
                var go = GameObject.Find("Fighter1_Robert");
                if (go != null) _fighter = go.GetComponent<Fighter>();
                return;
            }

            Vector3 leftTarget  = LeftRest;
            Vector3 rightTarget = RightRest;

            var state = _fighter.State;

            if (state == FighterState.Blocking)
            {
                leftTarget  = GuardL;
                rightTarget = GuardR;
            }
            else if (state == FighterState.Attacking)
            {
                // Animator 파라미터로 어떤 공격인지 구분
                var anim = _fighter.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    int height = anim.GetInteger("AttackHeight");
                    bool isHook   = anim.GetCurrentAnimatorStateInfo(0).IsName("Hook");
                    bool isUpper  = anim.GetCurrentAnimatorStateInfo(0).IsName("Uppercut");

                    if (isUpper)       rightTarget = PunchUpper;
                    else if (isHook)   rightTarget = PunchHook;
                    else               rightTarget = PunchFwd;   // Jab / default
                }
                else
                {
                    rightTarget = PunchFwd;
                }
            }

            float speed = state == FighterState.Attacking ? 18f : 12f;
            _left.localPosition  = Vector3.Lerp(_left.localPosition,  leftTarget,  Time.deltaTime * speed);
            _right.localPosition = Vector3.Lerp(_right.localPosition, rightTarget, Time.deltaTime * speed);

            _prevState = state;
        }

        Transform MakeGlove(string glName, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = glName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = new Vector3(0.11f, 0.09f, 0.13f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 충돌 방지
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 머티리얼
            var r   = go.GetComponent<Renderer>();
            var mat = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor",   gloveColor);
            if (mat.HasProperty("_Color"))        mat.SetColor("_Color",        gloveColor);
            if (mat.HasProperty("_Smoothness"))   mat.SetFloat("_Smoothness",  0.4f);
            r.sharedMaterial = mat;

            return go.transform;
        }
    }
}
