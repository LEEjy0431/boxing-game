using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using PersonalityBox.Characters;
using PersonalityBox.Core;
using PersonalityBox.AI;
using PersonalityBox.UI;   // BoxingHUD 참조용 (UI 생성은 런타임에서 처리)

/// <summary>
/// Tools ▶ PersonalityBox 메뉴 모음
///   ① Setup Boxing Scene         — 씬 전체 자동 구성
///   ② Apply Cel Shader           — 파이터에 툰 셰이더 적용 (URP)
///   ③ Create Toon Materials      — 로봇별 툰 머티리얼 생성
///   ④ Fix Arena Materials (URP)  — Boxing Arena Built-In → URP 머티리얼 업그레이드
///   ⑤ Scan Robot Animations      — 로봇 FBX에 내장된 클립 목록 출력
///   ⑥ Auto-Wire Animations       — Animator Controller에 클립 자동 연결
/// </summary>
public static class GameSetup
{
    // ── 경로 상수 ───────────────────────────────────────────────────────────
    const string SceneSavePath    = "Assets/Scenes/BoxingGame.unity";
    const string StagePrefabPath  = "Assets/MarpaStudio/Built-In/Prefabs/Stage .prefab";
    const string RingFloorPrefab  = "Assets/MarpaStudio/Built-In/Prefabs/RingFloor.prefab";
    const string F1PrefabPath    = "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Prefabs/Robert.prefab";
    const string F2PrefabPath    = "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Prefabs/Engie.prefab";
    const string F1DataPath      = "Assets/ScriptableObjects/Will_DATA.asset";
    const string F2DataPath      = "Assets/ScriptableObjects/Echo_DATA.asset";
    // URP 머티리얼 사용 (프로젝트가 URP이므로)
    const string F1MatPath       = "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Materials/URP/Robert.mat";
    const string F2MatPath       = "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Materials/URP/Engie.mat";
    const string AnimCtrlPath    = "Assets/Animations/FighterAnimCtrl.controller";

    // ════════════════════════════════════════════════════════════════════════
    // 🔥 완전 자동 수정 — 처음 실행할 때 이것만 누르면 됩니다
    //    경기장 설정 + 머티리얼 URP 변환 + 파이터 배치 + 애니메이션 연결 + 저장
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/🔥 COMPLETE FIX (여기서 시작!)")]
    static void CompleteFixAll()
    {
        Debug.Log("════════════════════════════════════════════════════");
        Debug.Log("[COMPLETE FIX] 시작 — 모든 문제를 자동으로 수정합니다...");

        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        // ── 1. 중복 오브젝트 제거 ────────────────────────────────────────────
        Debug.Log("[COMPLETE FIX] 1/7 — 중복 오브젝트 제거...");
        KillDuplicates();

        // ── 2. 경기장(BoxingArena) 씬에 배치 + URP 머티리얼 변환 ─────────────
        Debug.Log("[COMPLETE FIX] 2/7 — 경기장 설정 & 전체 씬 머티리얼 URP 변환...");
        EnsureArenaInScene();
        // BoxingArena 이름과 무관하게 씬 전체 비URP 머티리얼을 변환
        ForceSceneWideMaterialsToURP();

        // ── 3. 파이터 + GameManager + 카메라 설정 ────────────────────────────
        Debug.Log("[COMPLETE FIX] 3/7 — 파이터 & 매니저 설정...");
        RepairScene();

        // ── 4. 플레이스홀더 애니메이션 클립 생성 ─────────────────────────────
        Debug.Log("[COMPLETE FIX] 4/7 — 플레이스홀더 애니메이션 클립 생성...");
        CreatePlaceholderClips();

        // ── 5. FBX 클립 자동 연결 시도 ──────────────────────────────────────
        Debug.Log("[COMPLETE FIX] 5/7 — FBX 애니메이션 클립 자동 연결...");
        try { AutoWireAnimations(); } catch (System.Exception e) { Debug.LogWarning("[COMPLETE FIX] FBX 클립 연결 중 오류 (무시됨): " + e.Message); }

        // ── 6. 스케일 & 위치 최종 보정 ───────────────────────────────────────
        Debug.Log("[COMPLETE FIX] 6/7 — 스케일 & 위치 보정...");
        FixScaleAndClean();

        // ── 7. 저장 ──────────────────────────────────────────────────────────
        Debug.Log("[COMPLETE FIX] 7/7 — 씬 저장...");
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.path.Contains(".unity"))
        {
            EditorSceneManager.SaveScene(activeScene, SceneSavePath);
        }
        else
        {
            EditorSceneManager.SaveScene(activeScene);
        }
        AssetDatabase.Refresh();

        Debug.Log("[COMPLETE FIX] ✓ 완료! 이제 Play ▶ 버튼을 누르세요.");
        Debug.Log("[COMPLETE FIX] 조작법: WASD=이동 | J=잽 K=훅 L=어퍼컷 Space=필살기 | Q=가드 Shift=회피");
        Debug.Log("════════════════════════════════════════════════════");
    }

    // 씬 전체 비URP 머티리얼을 URP Lit으로 변환 (경기장 이름과 무관)
    static void ForceSceneWideMaterialsToURP()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Lit");
        if (urpLit == null) return;

        int count = 0;
        var visited = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            // 파이터 스킵 (별도 머티리얼 사용)
            if (r.GetComponentInParent<Fighter>() != null) continue;

            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || visited.Contains(mats[i])) continue;
                visited.Add(mats[i]);
                string sname = mats[i].shader.name;
                if (sname.StartsWith("Universal Render Pipeline") || sname == "Lit") continue;

                Texture mainTex   = mats[i].HasProperty("_MainTex")       ? mats[i].GetTexture("_MainTex")       : null;
                Color   col       = mats[i].HasProperty("_Color")         ? mats[i].GetColor("_Color")           : Color.white;
                Texture normalMap = mats[i].HasProperty("_BumpMap")       ? mats[i].GetTexture("_BumpMap")       : null;
                Texture emissMap  = mats[i].HasProperty("_EmissionMap")   ? mats[i].GetTexture("_EmissionMap")   : null;
                Color   emissCol  = mats[i].HasProperty("_EmissionColor") ? mats[i].GetColor("_EmissionColor")   : Color.black;

                mats[i].shader = urpLit;
                if (mainTex   != null && mats[i].HasProperty("_BaseMap"))     mats[i].SetTexture("_BaseMap",     mainTex);
                if (mats[i].HasProperty("_BaseColor"))                         mats[i].SetColor("_BaseColor",    col);
                if (normalMap != null && mats[i].HasProperty("_BumpMap"))     mats[i].SetTexture("_BumpMap",     normalMap);
                if (emissMap  != null && mats[i].HasProperty("_EmissionMap")) mats[i].SetTexture("_EmissionMap", emissMap);
                if (mats[i].HasProperty("_EmissionColor"))                     mats[i].SetColor("_EmissionColor", emissCol);
                count++;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[COMPLETE FIX] 씬 전체 머티리얼 URP 변환: {count}개");
    }

    // ── 경기장을 씬에 배치하고 URP 머티리얼로 변환 ──────────────────────────
    static void EnsureArenaInScene()
    {
        var arena = GameObject.Find("BoxingArena");

        // 없으면 Stage 프리팹 인스턴스화
        if (arena == null)
        {
            var stagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
            if (stagePrefab != null)
            {
                arena = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab);
                arena.name = "BoxingArena";
                arena.transform.position = Vector3.zero;
                arena.transform.localScale = Vector3.one;
                Debug.Log("[Arena] Stage 프리팹 배치 완료");
            }
            else
            {
                Debug.LogWarning("[Arena] Stage 프리팹 없음: " + StagePrefabPath + " — 간이 링 생성");
                arena = CreateSimpleArenaPlaceholder();
            }
        }

        // 모든 머티리얼을 URP로 강제 변환
        ForceAllMaterialsToURP(arena);

        // 링 바닥 콜라이더 확보
        EnsureRingFloorCollider(arena);

        // RingBoundary 확보
        var rb = arena.GetComponent<RingBoundary>() ?? arena.AddComponent<RingBoundary>();
        rb.ringRadius = 5f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // 씬에 있는 모든 Renderer의 머티리얼을 URP Lit으로 변환 (경기장 오브젝트 한정)
    static void ForceAllMaterialsToURP(GameObject root)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Lit");
        if (urpLit == null)
        {
            Debug.LogError("[Arena] 'Universal Render Pipeline/Lit' 셰이더를 찾지 못했습니다. " +
                           "Edit > Rendering > Materials > Convert All Built-in Materials to SRP 를 먼저 실행하세요.");
            return;
        }

        int count = 0;
        var visited = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || visited.Contains(mats[i])) continue;
                visited.Add(mats[i]);

                // 이미 URP 셰이더면 건너뜀
                string sname = mats[i].shader.name;
                if (sname.StartsWith("Universal Render Pipeline") || sname == "Lit") continue;

                // 기존 프로퍼티 백업
                Texture mainTex   = mats[i].HasProperty("_MainTex")       ? mats[i].GetTexture("_MainTex")       : null;
                Color   col       = mats[i].HasProperty("_Color")         ? mats[i].GetColor("_Color")           : Color.white;
                Texture emissMap  = mats[i].HasProperty("_EmissionMap")   ? mats[i].GetTexture("_EmissionMap")   : null;
                Color   emissCol  = mats[i].HasProperty("_EmissionColor") ? mats[i].GetColor("_EmissionColor")   : Color.black;
                Texture normalMap = mats[i].HasProperty("_BumpMap")       ? mats[i].GetTexture("_BumpMap")       : null;
                float   metallic  = mats[i].HasProperty("_Metallic")      ? mats[i].GetFloat("_Metallic")        : 0f;
                float   smooth    = mats[i].HasProperty("_Glossiness")    ? mats[i].GetFloat("_Glossiness")      : 0.5f;

                mats[i].shader = urpLit;

                if (mainTex   != null && mats[i].HasProperty("_BaseMap"))       mats[i].SetTexture("_BaseMap",    mainTex);
                if (mats[i].HasProperty("_BaseColor"))                           mats[i].SetColor("_BaseColor",   col);
                if (emissMap  != null && mats[i].HasProperty("_EmissionMap"))   mats[i].SetTexture("_EmissionMap", emissMap);
                if (mats[i].HasProperty("_EmissionColor"))                       mats[i].SetColor("_EmissionColor", emissCol);
                if (normalMap != null && mats[i].HasProperty("_BumpMap"))       mats[i].SetTexture("_BumpMap",    normalMap);
                if (mats[i].HasProperty("_Metallic"))                            mats[i].SetFloat("_Metallic",    metallic);
                if (mats[i].HasProperty("_Smoothness"))                          mats[i].SetFloat("_Smoothness",  smooth);

                count++;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Arena] ✓ {count}개 머티리얼 URP 변환 완료");
    }

    // 링 바닥에 콜라이더가 없으면 추가 (파이터가 떨어지지 않게)
    static void EnsureRingFloorCollider(GameObject arena)
    {
        // 이미 콜라이더가 있으면 건너뜀
        if (arena.GetComponentInChildren<Collider>() != null) return;

        // "RingFloor", "Stage", "Floor", "Canvas" 이름의 MeshFilter에 MeshCollider 추가
        string[] floorKeywords = { "ringfloor", "ring_floor", "stage", "canvas", "floor" };
        bool added = false;
        foreach (var mf in arena.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name.ToLower();
            bool isFloor = false;
            foreach (var kw in floorKeywords) if (n.Contains(kw)) { isFloor = true; break; }
            if (!isFloor) continue;

            if (mf.GetComponent<Collider>() == null)
            {
                mf.gameObject.AddComponent<MeshCollider>();
                Debug.Log($"[Arena] MeshCollider 추가: {mf.gameObject.name}");
            }
            added = true;
        }

        if (!added)
        {
            // 경계박스로 링 높이 추정 후 BoxCollider 추가
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }

            // 링 바닥 = 전체 오브젝트 높이의 약 35~50% 지점
            float floorY = first ? 0.9f : Mathf.Lerp(bounds.min.y, bounds.max.y, 0.4f);

            var existing = arena.transform.Find("_FloorCollider");
            if (existing == null)
            {
                var fc = new GameObject("_FloorCollider");
                fc.transform.SetParent(arena.transform);
                fc.transform.position = new Vector3(bounds.center.x, floorY, bounds.center.z);
                var bc = fc.AddComponent<BoxCollider>();
                bc.size = new Vector3(14f, 0.3f, 14f);
                Debug.Log($"[Arena] _FloorCollider 추가 (Y={floorY:F2}) — 파이터가 뜨면 GameManager > Ring Floor Y Override 조정");
            }
        }
    }

    // 간이 링 대체 (Stage 프리팹 없을 때)
    static GameObject CreateSimpleArenaPlaceholder()
    {
        var arena = new GameObject("BoxingArena");

        // 바닥 플랫폼
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "RingFloor";
        platform.transform.SetParent(arena.transform);
        platform.transform.localPosition = new Vector3(0f, 0f, 0f);
        platform.transform.localScale    = new Vector3(10f, 0.3f, 10f);

        // 로프 기둥 (4개 코너)
        Vector3[] corners = {
            new Vector3(-4.5f, 1.5f, -4.5f), new Vector3( 4.5f, 1.5f, -4.5f),
            new Vector3(-4.5f, 1.5f,  4.5f), new Vector3( 4.5f, 1.5f,  4.5f)
        };
        foreach (var pos in corners)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(arena.transform);
            post.transform.localPosition = pos;
            post.transform.localScale    = new Vector3(0.15f, 1.5f, 0.15f);
        }
        Debug.Log("[Arena] 간이 복싱 링 생성 완료 (Stage 프리팹 없음)");
        return arena;
    }

    // ════════════════════════════════════════════════════════════════════════
    // ♻️ BoxingArena 제거 + 심플 바닥 재설정
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/♻️ BoxingArena 제거 + 바닥 재설정 (이거 먼저!)")]
    static void RemoveArenaAndResetFloor()
    {
        // ── 1. BoxingArena 제거 ──────────────────────────────────────────
        int removed = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "BoxingArena") { Object.DestroyImmediate(go); removed++; }
        }
        Debug.Log($"[Reset] BoxingArena {removed}개 제거");

        // ── 2. 기존 _FloorCollider / RingMat 모두 제거 ──────────────────
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "_FloorCollider" || go.name == "RingMat")
                Object.DestroyImmediate(go);
        }

        // ── 3. 바닥 높이 결정 (SpawnPoint 또는 스폰 포인트 기준, 부동 파이터 위치 무시) ──
        var f1go = GameObject.Find("Fighter1_Robert");
        var f2go = GameObject.Find("Fighter2_Engie");
        var mm2  = Object.FindAnyObjectByType<MatchManager>();

        // SpawnPoint가 설정돼 있으면 그 Y를 사용 (에디터에서 설정한 값이 가장 신뢰)
        float floorY = 0f;
        if (mm2 != null && mm2.spawnPoint1 != null && mm2.spawnPoint1.position.y > -50f)
            floorY = mm2.spawnPoint1.position.y;
        else if (f1go != null && Mathf.Abs(f1go.transform.position.y) < 30f)
            floorY = f1go.transform.position.y;
        else if (f2go != null && Mathf.Abs(f2go.transform.position.y) < 30f)
            floorY = f2go.transform.position.y;
        // 바닥 콜라이더 중심 = 표면보다 0.2 아래
        float colliderCenterY = floorY - 0.2f;

        // ── 4. 새 바닥 생성 (BoxCollider + 비주얼 Plane) ─────────────────
        var floorRoot = new GameObject("_FloorCollider");
        floorRoot.transform.position = new Vector3(0f, colliderCenterY, 0f);
        var bc = floorRoot.AddComponent<BoxCollider>();
        bc.size = new Vector3(30f, 0.4f, 30f);

        // ── 5. MatchManager 스폰포인트 Y 보정 ───────────────────────────
        if (mm2 != null)
        {
            mm2.ringFloorYOverride = floorY;
            if (mm2.spawnPoint1 != null)
                mm2.spawnPoint1.position = new Vector3(mm2.spawnPoint1.position.x, floorY, 0f);
            if (mm2.spawnPoint2 != null)
                mm2.spawnPoint2.position = new Vector3(mm2.spawnPoint2.position.x, floorY, 0f);
            Debug.Log($"[Reset] MatchManager ringFloorYOverride = {floorY:F2}");
        }

        // ── 6. 파이터 Y 보정 ─────────────────────────────────────────────
        if (f1go != null) f1go.transform.position = new Vector3(f1go.transform.position.x, floorY, 0f);
        if (f2go != null) f2go.transform.position = new Vector3(f2go.transform.position.x, floorY, 0f);

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Reset] ✓ 완료 — 바닥Y={floorY:F2} | Ctrl+S 저장 → Play ▶");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 🧹 씬 정리 — 중복 제거 + 파이터 스케일/위치 자동 조정
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/🧹 Fix Scale & Clean Scene")]
    static void FixScaleAndClean()
    {
        // ── 1. 중복 BoxingArena 제거 ─────────────────────────────────────
        var allArenas = new System.Collections.Generic.List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == "BoxingArena") allArenas.Add(go);

        if (allArenas.Count > 1)
        {
            allArenas.Sort((a, b) => a.transform.childCount.CompareTo(b.transform.childCount));
            Object.DestroyImmediate(allArenas[0]);
            Debug.Log("[Fix] 중복 BoxingArena 제거");
        }

        // ── 2. 링 바닥 위치 스캔 ────────────────────────────────────────
        // 전략: "RingFloor" 또는 "Stage" 이름을 우선 탐색
        //       없으면 5~30m 사이 크기의 납작한 오브젝트 중 가장 높은 것
        Renderer ringRenderer = null;

        // 1순위: 이름이 정확히 맞는 것
        string[] priority = { "ringfloor", "ring floor", "ringplatform" };
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = r.gameObject.name.ToLower();
            foreach (var p in priority)
                if (n.Contains(p)) { ringRenderer = r; break; }
            if (ringRenderer != null) break;
        }

        // 2순위: 5~30m 크기이고 납작한(플랫폼 같은) 오브젝트 중 Y가 가장 높은 것
        if (ringRenderer == null)
        {
            float bestY = float.MinValue;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                float  w = Mathf.Max(b.size.x, b.size.z);
                float  h = b.size.y;
                // 5~40m 너비, 납작한(높이 < 너비/3), 이름에 대형 바닥 단어 없음
                string n = r.gameObject.name.ToLower();
                if (n.Contains("floor") || n.Contains("ceiling") || n.Contains("wall") ||
                    n.Contains("seat")  || n.Contains("chair")   || n.Contains("screen")) continue;
                if (w < 4f || w > 40f) continue;
                if (h > w * 0.5f) continue; // 너무 두꺼운 건 제외
                if (b.max.y > bestY)
                {
                    bestY = b.max.y;
                    ringRenderer = r;
                }
            }
        }

        Vector3 ringCenter;
        float   ringTopY;
        if (ringRenderer != null)
        {
            ringCenter = new Vector3(ringRenderer.bounds.center.x, 0, ringRenderer.bounds.center.z);
            ringTopY   = ringRenderer.bounds.max.y;
            Debug.Log($"[Fix] 링 발견: {ringRenderer.gameObject.name} | 중심={ringCenter} | 상단Y={ringTopY:F2} | 너비={ringRenderer.bounds.size.x:F1}m");
        }
        else
        {
            ringCenter = Vector3.zero;
            ringTopY   = 0f;
            Debug.LogWarning("[Fix] 링을 자동 탐지 못했습니다 — Y=0 기본값 사용.");
        }

        // ── 3. 파이터 배치 (스케일 1.0 — 실물 크기) ──────────────────────
        // 링 너비의 1/4 거리에서 마주봄
        float halfRingW  = ringRenderer != null
            ? Mathf.Max(ringRenderer.bounds.size.x, ringRenderer.bounds.size.z) * 0.35f
            : 3f;
        halfRingW = Mathf.Clamp(halfRingW, 2f, 5f);

        var f1 = GameObject.Find("Fighter1_Robert");
        var f2 = GameObject.Find("Fighter2_Engie");

        void PlaceFighter(GameObject go, float xOff, float rotY)
        {
            if (go == null) return;
            go.transform.localScale = Vector3.one;
            go.transform.position   = new Vector3(ringCenter.x + xOff, ringTopY + 0.05f, ringCenter.z);
            go.transform.rotation   = Quaternion.Euler(0f, rotY, 0f);
            var col = go.GetComponent<CapsuleCollider>();
            if (col != null) { col.height = 2f; col.radius = 0.35f; col.center = new Vector3(0, 1f, 0); }
        }

        PlaceFighter(f1, -halfRingW,  90f);
        PlaceFighter(f2,  halfRingW, -90f);

        // ── 4. SpawnPoint 동기화 ─────────────────────────────────────────
        var mm = Object.FindAnyObjectByType<MatchManager>();
        if (mm != null)
        {
            if (mm.spawnPoint1 != null)
                mm.spawnPoint1.position = new Vector3(ringCenter.x - halfRingW, ringTopY + 0.05f, ringCenter.z);
            if (mm.spawnPoint2 != null)
                mm.spawnPoint2.position = new Vector3(ringCenter.x + halfRingW, ringTopY + 0.05f, ringCenter.z);
            mm.fighter1 = f1 != null ? f1.GetComponent<Fighter>() : mm.fighter1;
            mm.fighter2 = f2 != null ? f2.GetComponent<Fighter>() : mm.fighter2;
        }

        // ── 5. RingBoundary 반경 링 크기에 맞춤 ─────────────────────────
        var rb = Object.FindAnyObjectByType<RingBoundary>();
        if (rb != null)
        {
            rb.ringRadius         = halfRingW * 1.8f;
            rb.transform.position = new Vector3(ringCenter.x, ringTopY, ringCenter.z);
        }

        // ── 6. PunchOrigin 흰 공 제거 ────────────────────────────────────
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name != "PunchOrigin") continue;
            foreach (var comp in new System.Type[]{typeof(MeshRenderer), typeof(MeshFilter), typeof(SphereCollider)})
            {
                var c = go.GetComponent(comp);
                if (c != null) Object.DestroyImmediate(c);
            }
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Fix] ✓ 완료 | 파이터 간격={halfRingW*2:F1}m | 링 Y={ringTopY:F2}");
        Debug.Log("[Fix] Ctrl+S 저장 → Game 탭 클릭 → Play ▶");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ☢ 중복 제거 전용 (이것 먼저 실행 후 Repair)
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/☢ 0. Kill Duplicates (먼저 실행)")]
    static void KillDuplicates()
    {
        int removed = 0;

        // ── 중복 GameManager 제거 ─────────────────────────────────────────
        var allMM = Object.FindObjectsByType<MatchManager>(FindObjectsSortMode.None);
        if (allMM.Length > 1)
        {
            // spawnPoint1이 있는 것을 우선 보존
            MatchManager keep = null;
            foreach (var mm in allMM)
                if (mm.spawnPoint1 != null && keep == null) keep = mm;
            if (keep == null) keep = allMM[0];

            foreach (var mm in allMM)
            {
                if (mm == keep) continue;
                Debug.Log($"[Kill] 중복 GameManager 제거: {mm.gameObject.name}");
                Object.DestroyImmediate(mm.gameObject);
                removed++;
            }
        }

        // ── 중복 Fighter1_Robert 제거 ─────────────────────────────────────
        removed += KillDuplicateFighters("Fighter1_Robert");
        removed += KillDuplicateFighters("Fighter2_Engie");

        // ── 중복 Main Camera 제거 ─────────────────────────────────────────
        var allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 1; i < allCams.Length; i++)
        {
            Debug.Log($"[Kill] 중복 카메라 제거: {allCams[i].gameObject.name}");
            Object.DestroyImmediate(allCams[i].gameObject);
            removed++;
        }

        // ── 중복 Directional Light 제거 ──────────────────────────────────
        var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int dirCount = 0;
        foreach (var l in allLights)
        {
            if (l.type != LightType.Directional) continue;
            if (dirCount > 0) { Object.DestroyImmediate(l.gameObject); removed++; }
            dirCount++;
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Kill] ✓ 중복 {removed}개 제거 완료. 이제 🔧 Repair Current Scene 실행하세요.");
    }

    static int KillDuplicateFighters(string fighterName)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == fighterName && go.scene.IsValid()) found.Add(go);

        if (found.Count <= 1) return 0;

        // Fighter 컴포넌트가 있는 것 우선 보존
        GameObject keep = null;
        foreach (var go in found)
            if (go.GetComponent<Fighter>() != null && keep == null) keep = go;
        if (keep == null) keep = found[0];

        int removed = 0;
        foreach (var go in found)
        {
            if (go == keep) continue;
            Debug.Log($"[Kill] 중복 파이터 제거: {go.name} at {go.transform.position}");
            Object.DestroyImmediate(go);
            removed++;
        }
        return removed;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 🔧 현재 씬 수리 (카메라·Fighter2·GameManager 누락 시)
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/🔧 Repair Current Scene")]
    static void RepairScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");

        var animCtrl = CreateAnimatorController();

        // ── 중복 Main Camera 제거 ─────────────────────────────────────────
        var allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (allCams.Length > 1)
        {
            // "Main Camera (1)" 같은 중복 카메라 제거
            for (int i = 1; i < allCams.Length; i++)
                Object.DestroyImmediate(allCams[i].gameObject);
            Debug.Log($"[Repair] 중복 카메라 {allCams.Length - 1}개 제거");
        }

        // ── 중복 Fighter 제거 ────────────────────────────────────────────
        // 같은 이름의 오브젝트가 여러 개면 첫 번째만 남기고 삭제
        RemoveDuplicatesByName("Fighter1_Robert");
        RemoveDuplicatesByName("Fighter2_Engie");

        // ── Fighter1/2 찾기 or 생성 ──────────────────────────────────────
        Fighter fighter1 = FindOrCreateFighter(
            searchName: "Fighter1_Robert",
            prefabPath: F1PrefabPath,
            spawnPos:   new Vector3(-1.8f, 2f, 0f),
            faceAngle:  90f,
            dataPath:   F1DataPath, matPath: F1MatPath,
            animCtrl:   animCtrl, isPlayer: true);

        Fighter fighter2 = FindOrCreateFighter(
            searchName: "Fighter2_Engie",
            prefabPath: F2PrefabPath,
            spawnPos:   new Vector3(1.8f, 2f, 0f),
            faceAngle:  -90f,
            dataPath:   F2DataPath, matPath: F2MatPath,
            animCtrl:   animCtrl, isPlayer: false);

        // Animator Controller 연결 (프리팹 자식 포함)
        FixAnimator(fighter1, animCtrl);
        FixAnimator(fighter2, animCtrl);

        // 상대 트랜스폼 + 레이어 연결
        fighter1.opponentTransform = fighter2.transform;
        fighter2.opponentTransform = fighter1.transform;
        int layer = LayerMask.GetMask("Default");
        fighter1.hitLayer = layer;
        fighter2.hitLayer = layer;

        // ── GameManager ──────────────────────────────────────────────────
        var mm = Object.FindAnyObjectByType<MatchManager>();
        if (mm == null)
        {
            var mmGo = new GameObject("GameManager");
            mm = mmGo.AddComponent<MatchManager>();
            if (mmGo.GetComponent<HitFeedback>() == null)
                mmGo.AddComponent<HitFeedback>();
            mm.spawnPoint1 = MakeSpawnPoint(mmGo, "SpawnPoint1", new Vector3(-1.8f, 2f, 0f),  90f);
            mm.spawnPoint2 = MakeSpawnPoint(mmGo, "SpawnPoint2", new Vector3( 1.8f, 2f, 0f), -90f);
            Debug.Log("[Repair] GameManager 생성");
        }
        mm.fighter1      = fighter1;
        mm.fighter2      = fighter2;
        mm.roundsToWin   = 2;
        mm.roundDuration = 99f;

        // ── 링 크기·높이 감지 후 SpawnPoint 위치 보정 ──────────────────
        float detectedRingY     = DetectRingFloorY();
        float detectedHalfWidth = DetectRingHalfWidth();
        float spawnY = detectedRingY > 0.01f ? detectedRingY : 2f;
        float spawnX = detectedHalfWidth > 0.1f ? detectedHalfWidth : 3f;

        mm.ringFloorYOverride = spawnY;

        if (mm.spawnPoint1 != null)
            mm.spawnPoint1.position = new Vector3(-spawnX, spawnY, 0f);
        if (mm.spawnPoint2 != null)
            mm.spawnPoint2.position = new Vector3( spawnX, spawnY, 0f);

        // 파이터 초기 위치도 스폰 포인트에 맞게 이동
        fighter1.transform.position = new Vector3(-spawnX, spawnY, 0f);
        fighter1.transform.rotation = Quaternion.Euler(0f,  90f, 0f);
        fighter2.transform.position = new Vector3( spawnX, spawnY, 0f);
        fighter2.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        if (detectedRingY > 0.01f)
            Debug.Log($"[Repair] 링 Y={spawnY:F2}, 반폭={spawnX:F2} → 파이터 간격 {spawnX*2:F1}m");
        else
            Debug.LogWarning($"[Repair] 링 자동 감지 실패 — Y=2, 간격={spawnX*2:F1}m 기본값. 어색하면 GameManager > 'Ring Floor Y Override' 조정.");

        // ── 바닥 콜라이더 정렬 (상단 = spawnY 정확히) ──────────────────────
        // 기존 _FloorCollider를 제거하고 스폰 높이에 맞게 재생성
        // BoxCollider 중심을 spawnY-0.2f에 두면 상단(+0.2)이 spawnY가 됨
        var oldFloor = GameObject.Find("_FloorCollider");
        if (oldFloor != null) Object.DestroyImmediate(oldFloor);
        var floorGo = new GameObject("_FloorCollider");
        floorGo.transform.position = new Vector3(0f, spawnY - 0.2f, 0f);
        var bc = floorGo.AddComponent<BoxCollider>();
        bc.size = new Vector3(22f, 0.4f, 22f);
        Debug.Log($"[Repair] _FloorCollider 재생성: 상단 Y={spawnY:F2}");

        // ── 카메라 — "MainCamera" 태그 오브젝트를 찾고, 없으면 강제 생성 ──
        GameObject camGo = GameObject.FindWithTag("MainCamera");
        if (camGo == null)
        {
            // 태그 없는 Camera 컴포넌트도 검색
            var anyCam = Object.FindAnyObjectByType<Camera>();
            camGo = anyCam != null ? anyCam.gameObject : null;
        }
        if (camGo == null)
        {
            camGo = new GameObject("Main Camera");
            camGo.AddComponent<Camera>().fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();
            Debug.Log("[Repair] Main Camera 생성");
        }
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        if (cam != null) cam.nearClipPlane = 0.05f;

        // 1인칭 카메라 (Fighter1 시점) — CameraFollow 제거
        var oldFollow = camGo.GetComponent<CameraFollow>();
        if (oldFollow != null) Object.DestroyImmediate(oldFollow);

        var fpCam = camGo.GetComponent<FirstPersonCamera>() ?? camGo.AddComponent<FirstPersonCamera>();
        fpCam.fighter1          = fighter1;
        fpCam.eyeHeight         = 1.65f;
        fpCam.eyeForwardOffset  = 0.12f;
        fpCam.verticalAngle     = -5f;
        if (cam != null) { cam.fieldOfView = 80f; cam.nearClipPlane = 0.01f; }

        // FirstPersonCamera가 LateUpdate에서 위치를 자동 갱신 — 초기값만 설정
        float camY = detectedRingY > 0.01f ? detectedRingY + 1.65f : 3.65f;
        camGo.transform.position = new Vector3(-spawnX, camY, 0f);
        camGo.transform.rotation = Quaternion.Euler(-5f, 90f, 0f);

        // ── HUD 자동 생성 ────────────────────────────────────────────────
        EnsureHUD(fighter1, fighter2);

        // ── RingBoundary 설정 ────────────────────────────────────────────
        var ringBound = Object.FindAnyObjectByType<RingBoundary>();
        if (ringBound != null)
        {
            ringBound.fighters  = new Fighter[] { fighter1, fighter2 };
            ringBound.ringRadius = spawnX * 1.5f;   // 스폰X의 1.5배 = 이동 공간 확보
            // 링 경계 중심을 스폰 Y높이로 맞춤
            ringBound.transform.position = new Vector3(0f, spawnY, 0f);
        }

        // ── PunchOrigin 확보 ─────────────────────────────────────────────
        EnsurePunchOrigin(fighter1);
        EnsurePunchOrigin(fighter2);

        // ── 절차적 모션 컴포넌트 추가 ────────────────────────────────────
        EnsureComponent<FighterBodyAnim>(fighter1.gameObject);
        EnsureComponent<FighterBodyAnim>(fighter2.gameObject);

        // ── Fighter 렌더러 복원 (FirstPersonCamera가 ShadowsOnly로 만들었을 수 있음) ──
        foreach (var fighter in new[] { fighter1, fighter2 })
        {
            foreach (var r in fighter.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.enabled = true;
            }
        }

        // ── Fighter 활성화 확인 ──────────────────────────────────────────
        fighter1.gameObject.SetActive(true);
        fighter2.gameObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Repair] ✓ 완료 — Ctrl+S 로 저장하세요.");
        Debug.Log($"[Repair]   Fighter1={fighter1.name} at {fighter1.transform.position}");
        Debug.Log($"[Repair]   Fighter2={fighter2.name} at {fighter2.transform.position}");
        Debug.Log($"[Repair]   SpawnPoint1={mm.spawnPoint1?.position}, SpawnPoint2={mm.spawnPoint2?.position}");
    }

    // ── 헬퍼: 이름으로 Fighter 찾거나 없으면 새로 생성 ──────────────────────
    static Fighter FindOrCreateFighter(string searchName, string prefabPath,
        Vector3 spawnPos, float faceAngle, string dataPath, string matPath,
        AnimatorController animCtrl, bool isPlayer)
    {
        var go = GameObject.Find(searchName);
        if (go != null)
        {
            // CharacterController 먼저 제거 (Rigidbody 와 공존 불가)
            foreach (var cc2 in go.GetComponentsInChildren<CharacterController>(true))
                Object.DestroyImmediate(cc2);

            var f = go.GetComponent<Fighter>() ?? go.AddComponent<Fighter>();

            // FighterData 연결
            if (f.data == null)
            {
                var d = AssetDatabase.LoadAssetAtPath<FighterData>(dataPath);
                if (d != null) f.data = d;
            }

            // CapsuleCollider 수정
            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            if (col != null) { col.height = 2f; col.radius = 0.35f; col.center = new Vector3(0f, 1f, 0f); }

            // Rigidbody 수정
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            if (rb != null)
            {
                rb.freezeRotation         = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.linearDamping          = 1f;
            }

            // 스케일 강제 1.0
            go.transform.localScale = Vector3.one;

            // 플레이어/AI 컴포넌트 확인 및 추가
            if (isPlayer)
            {
                // AI가 있으면 비활성화
                var ai = go.GetComponent<AIController>();
                if (ai != null) ai.enabled = false;

                var input = go.GetComponent<PlayerInputHandler>();
                if (input == null) input = go.AddComponent<PlayerInputHandler>();
                input.scheme  = PlayerInputHandler.InputScheme.Player1;
                input.enabled = true;
            }
            else
            {
                // PlayerInputHandler 비활성화
                var input = go.GetComponent<PlayerInputHandler>();
                if (input != null) input.enabled = false;

                var ai = go.GetComponent<AIController>();
                if (ai == null) ai = go.AddComponent<AIController>();
                ai.behaviour = AIBehaviour.Balanced;
                ai.enabled   = true;
            }

            return f;
        }
        // 없으면 새로 만들기
        var newGo = CreateFighter(prefabPath, searchName, spawnPos, faceAngle,
                                  dataPath, matPath, animCtrl, isPlayer);
        Debug.Log($"[Repair] {searchName} 생성");
        return newGo.GetComponent<Fighter>();
    }

    // ── 헬퍼: Animator Controller가 없으면 연결 (자식 포함 탐색) ────────────
    static void FixAnimator(Fighter f, AnimatorController ctrl)
    {
        if (f == null) return;
        // 루트 또는 자식에서 Animator 탐색
        var anim = f.GetComponent<Animator>() ?? f.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            anim = f.gameObject.AddComponent<Animator>();
            Debug.Log($"[Repair] Animator 추가: {f.name}");
        }
        if (anim.runtimeAnimatorController == null)
            anim.runtimeAnimatorController = ctrl;
    }

    // ── 헬퍼: 같은 이름 중복 오브젝트 제거 (첫 번째만 유지) ────────────────
    static void RemoveDuplicatesByName(string objName)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == objName && go.scene.IsValid()) found.Add(go);

        for (int i = 1; i < found.Count; i++)
        {
            Object.DestroyImmediate(found[i]);
            Debug.Log($"[Repair] 중복 제거: {objName}");
        }
    }

    static void EnsurePunchOrigin(Fighter f)
    {
        if (f == null || f.punchOrigin != null) return;
        var go = new GameObject("PunchOrigin");
        go.transform.SetParent(f.transform);
        go.transform.localPosition = new Vector3(0.4f, 1.4f, 0.7f);
        f.punchOrigin = go.transform;
    }

    // ════════════════════════════════════════════════════════════════════════
    // ⓪ DemoScene을 기반으로 완전한 복싱 경기장 씬 생성
    //    MarpaStudio DemoScene 로드 → 카메라/조명 정리 → 파이터·GameManager 추가
    //    → 머티리얼 전체 URP 변환 → BoxingGame.unity 저장
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/⓪ Build Full Arena (DemoScene Base)")]
    static void BuildFullArenaScene()
    {
        const string demoPath = "Assets/MarpaStudio/Scene/DemoScene.unity";
        if (!System.IO.File.Exists(System.IO.Path.Combine(
                Application.dataPath.Replace("Assets",""), demoPath)))
        {
            Debug.LogError("[BuildArena] DemoScene을 찾지 못했습니다: " + demoPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))     AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");

        // ── 1. DemoScene을 어디티브로 열기 ──────────────────────────────
        var demoScene = EditorSceneManager.OpenScene(demoPath, OpenSceneMode.Additive);

        // ── 2. 새 게임 씬 생성 ──────────────────────────────────────────
        var gameScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(gameScene);

        // ── 3. DemoScene 오브젝트를 게임 씬으로 이동 ────────────────────
        foreach (var go in demoScene.GetRootGameObjects())
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameScene);
        EditorSceneManager.CloseScene(demoScene, true);

        // ── 4. DemoScene의 기존 카메라 제거 (우리가 새 카메라를 추가할 것)
        // "Main Camera" 태그가 없는 카메라만 비활성화 (이미 있는 우리 카메라는 유지)
        foreach (var demoCam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (demoCam.gameObject.name == "Main Camera") continue; // 우리 카메라 보존
            demoCam.gameObject.SetActive(false);  // 컴포넌트 대신 오브젝트 자체를 비활성
            var al = demoCam.GetComponent<AudioListener>();
            if (al) Object.DestroyImmediate(al);
        }

        // ── 5. 링 중심 찾기 ─────────────────────────────────────────────
        Vector3 ringCenter  = Vector3.zero;
        float   spawnHeight = 2f;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            string n = go.name.ToLower();
            if ((n.Contains("stage") || n.Contains("ringfloor") || n.Contains("ring floor"))
                && go.GetComponent<Renderer>() != null)
            {
                ringCenter  = go.transform.position;
                spawnHeight = ringCenter.y + 2f;
                break;
            }
        }
        Debug.Log($"[BuildArena] 링 중심: {ringCenter}, 파이터 스폰 Y: {spawnHeight}");

        // 링 바닥 콜라이더 (없으면 추가)
        var arenaRoot = GameObject.Find("BoxingArena") ?? new GameObject("BoxingArena");
        var existingFloor = arenaRoot.transform.Find("_FloorCollider");
        if (existingFloor == null)
        {
            var fc = new GameObject("_FloorCollider");
            fc.transform.SetParent(arenaRoot.transform);
            fc.transform.position = new Vector3(ringCenter.x, ringCenter.y + 0.1f, ringCenter.z);
            var bc = fc.AddComponent<BoxCollider>();
            bc.size = new Vector3(8f, 0.2f, 8f);
        }
        if (arenaRoot.GetComponent<RingBoundary>() == null)
        {
            var rb2 = arenaRoot.AddComponent<RingBoundary>();
            rb2.ringRadius = 4f;
        }

        // ── 6. Animator Controller 생성 ─────────────────────────────────
        var animCtrl = CreateAnimatorController();

        // ── 7. 파이터 배치 ──────────────────────────────────────────────
        var spawnL = new Vector3(ringCenter.x - 1.8f, spawnHeight, ringCenter.z);
        var spawnR = new Vector3(ringCenter.x + 1.8f, spawnHeight, ringCenter.z);

        var f1Go = CreateFighter(F1PrefabPath, "Fighter1_Robert", spawnL,  90f,
                                 F1DataPath, F1MatPath, animCtrl, isPlayer: true);
        var f2Go = CreateFighter(F2PrefabPath, "Fighter2_Engie",  spawnR, -90f,
                                 F2DataPath, F2MatPath, animCtrl, isPlayer: false);

        var fighter1 = f1Go.GetComponent<Fighter>();
        var fighter2 = f2Go.GetComponent<Fighter>();
        fighter1.opponentTransform = f2Go.transform;
        fighter2.opponentTransform = f1Go.transform;
        int defaultLayer = LayerMask.GetMask("Default");
        fighter1.hitLayer = defaultLayer;
        fighter2.hitLayer = defaultLayer;

        // RingBoundary 등록
        var ringBound = arenaRoot.GetComponent<RingBoundary>();
        if (ringBound != null)
            ringBound.fighters = new Fighter[] { fighter1, fighter2 };

        // FighterBodyAnim 추가
        EnsureComponent<FighterBodyAnim>(f1Go);
        EnsureComponent<FighterBodyAnim>(f2Go);

        // ── 8. GameManager ───────────────────────────────────────────────
        var mmGo = new GameObject("GameManager");
        var mm = mmGo.AddComponent<MatchManager>();
        mm.fighter1      = fighter1;
        mm.fighter2      = fighter2;
        mm.roundsToWin   = 2;
        mm.roundDuration = 99f;
        mm.spawnPoint1   = MakeSpawnPoint(mmGo, "SpawnPoint1", spawnL,  90f);
        mm.spawnPoint2   = MakeSpawnPoint(mmGo, "SpawnPoint2", spawnR, -90f);
        mmGo.AddComponent<HitFeedback>();

        // ── 9. 1인칭 카메라 ──────────────────────────────────────────────
        var camGo  = new GameObject("Main Camera");
        camGo.tag  = "MainCamera";
        var mainCam = camGo.AddComponent<Camera>();
        mainCam.fieldOfView   = 80f;
        mainCam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        var fpCam = camGo.AddComponent<FirstPersonCamera>();
        fpCam.fighter1 = fighter1;

        // ── 10. 전체 머티리얼 URP 변환 ──────────────────────────────────
        UpgradeAllSceneMaterials();

        // ── 11. 저장 ────────────────────────────────────────────────────
        // 절대 경로로 저장 (상대 경로가 Save Dialog를 유발할 때 대비)
        string absPath = System.IO.Path.Combine(
            Application.dataPath, "Scenes", "BoxingGame.unity");
        string relPath = "Assets/Scenes/BoxingGame.unity";

        bool saved = EditorSceneManager.SaveScene(gameScene, relPath);
        if (!saved)
        {
            // 상대 경로 실패 시 절대 경로 시도
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absPath));
            EditorSceneManager.SaveScene(gameScene, absPath);
        }
        AssetDatabase.Refresh();
        Debug.Log("[BuildArena] ✓ 완전한 경기장 씬 생성 완료 → Assets/Scenes/BoxingGame.unity");
        Debug.Log("[BuildArena] ⚠ Mesh Root가 None이면 Inspector에서 로봇 자식 오브젝트를 직접 드래그하세요.");
    }

    // 씬 전체 Standard→URP 머티리얼 강제 변환
    static void UpgradeAllSceneMaterials()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Lit");
        if (urpLit == null) { Debug.LogWarning("[UpgradeMat] URP Lit 셰이더 없음"); return; }

        var visited = new System.Collections.Generic.HashSet<Material>();
        int count = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || visited.Contains(mats[i])) continue;
                visited.Add(mats[i]);
                if (!mats[i].shader.name.Contains("Standard")) continue;

                Texture tex   = mats[i].HasProperty("_MainTex") ? mats[i].GetTexture("_MainTex") : null;
                Color   col   = mats[i].HasProperty("_Color")   ? mats[i].GetColor("_Color")     : Color.white;
                Texture emiss = mats[i].HasProperty("_EmissionMap") ? mats[i].GetTexture("_EmissionMap") : null;
                Color emCol   = mats[i].HasProperty("_EmissionColor") ? mats[i].GetColor("_EmissionColor") : Color.black;

                mats[i].shader = urpLit;
                if (tex   != null && mats[i].HasProperty("_BaseMap"))    mats[i].SetTexture("_BaseMap",    tex);
                if (emiss != null && mats[i].HasProperty("_EmissionMap")) mats[i].SetTexture("_EmissionMap", emiss);
                if (mats[i].HasProperty("_BaseColor"))   mats[i].SetColor("_BaseColor",   col);
                if (mats[i].HasProperty("_EmissionColor")) mats[i].SetColor("_EmissionColor", emCol);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[UpgradeMat] ✓ {count}개 머티리얼 URP 변환");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ① 씬 자동 생성
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/① Setup Boxing Scene")]
    static void SetupScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))     AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 조명
        var lightGo = new GameObject("Directional Light");
        var dl = lightGo.AddComponent<Light>();
        dl.type      = LightType.Directional;
        dl.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 복싱 링
        RingBoundary ringBoundary = null;
        var stagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
        GameObject stageGo;
        if (stagePrefab != null)
        {
            stageGo = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab);
            stageGo.name = "BoxingArena";
        }
        else
        {
            stageGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            stageGo.name = "BoxingArena_Placeholder";
            stageGo.transform.localScale = Vector3.one * 3f;
            Debug.LogWarning("[GameSetup] Stage 프리팹 없음 — Plane으로 대체");
        }

        // 바닥 콜라이더 추가 (없으면 파이터가 떨어짐)
        AddFloorCollider(stageGo);
        ringBoundary = stageGo.GetComponent<RingBoundary>() ?? stageGo.AddComponent<RingBoundary>();
        ringBoundary.ringRadius = 5f;

        // Animator Controller 생성
        var animCtrl = CreateAnimatorController();

        // Fighter1 (플레이어 — Robert)
        var f1Go = CreateFighter(F1PrefabPath, "Fighter1_Robert", new Vector3(-1.8f, 2f, 0f),  90f,
                                 F1DataPath, F1MatPath, animCtrl, isPlayer: true);
        // Fighter2 (AI — Engie)
        var f2Go = CreateFighter(F2PrefabPath, "Fighter2_Engie",  new Vector3( 1.8f, 2f, 0f), -90f,
                                 F2DataPath, F2MatPath, animCtrl, isPlayer: false);

        // 상대 트랜스폼 + RingBoundary 연결
        var fighter1 = f1Go.GetComponent<Fighter>();
        var fighter2 = f2Go.GetComponent<Fighter>();
        fighter1.opponentTransform = f2Go.transform;
        fighter2.opponentTransform = f1Go.transform;
        int defaultLayer = LayerMask.GetMask("Default");
        fighter1.hitLayer = defaultLayer;
        fighter2.hitLayer = defaultLayer;
        if (ringBoundary != null)
            ringBoundary.fighters = new Fighter[] { fighter1, fighter2 };

        // GameManager
        var mmGo = new GameObject("GameManager");
        var mm = mmGo.AddComponent<MatchManager>();
        mm.fighter1      = fighter1;
        mm.fighter2      = fighter2;
        mm.roundsToWin   = 2;
        mm.roundDuration = 99f;
        mm.spawnPoint1   = MakeSpawnPoint(mmGo, "SpawnPoint1", new Vector3(-1.8f, 2f, 0f),  90f);
        mm.spawnPoint2   = MakeSpawnPoint(mmGo, "SpawnPoint2", new Vector3( 1.8f, 2f, 0f), -90f);
        mmGo.AddComponent<HitFeedback>();

        // 1인칭 카메라
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView   = 75f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        var fpCam = camGo.AddComponent<FirstPersonCamera>();
        fpCam.fighter1 = fighter1;

        EditorSceneManager.SaveScene(scene, SceneSavePath);
        AssetDatabase.Refresh();
        Debug.Log("[GameSetup] ✓ 씬 생성 완료 → " + SceneSavePath);
        Debug.Log("[GameSetup] 다음 단계: ⑥ Auto-Wire Animations 실행 후 플레이");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ② Cel Shader 적용
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/② Apply Cel Shader to Fighters")]
    static void ApplyCelShader()
    {
        // PersonalityBox/ToonLit (URP) 우선, 없으면 Flexible Cel Shader
        var celShader = Shader.Find("PersonalityBox/ToonLit")
                     ?? Shader.Find("Flexible Cel Shader/Cel Shaded Lit")
                     ?? Shader.Find("Toon/Lit");

        if (celShader == null)
        {
            Debug.LogError("[GameSetup] 셰이더를 찾지 못했습니다. 프로젝트를 재컴파일하세요 (Ctrl+R).");
            return;
        }

        int count = 0;
        foreach (var fighter in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None))
        {
            foreach (var r in fighter.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    // 기존 텍스처 백업
                    Texture albedoTex = mats[i].HasProperty("_BaseMap")    ? mats[i].GetTexture("_BaseMap")
                                      : mats[i].HasProperty("_MainTex")    ? mats[i].GetTexture("_MainTex")
                                      : null;
                    Texture emissTex  = mats[i].HasProperty("_EmissionMap")? mats[i].GetTexture("_EmissionMap") : null;
                    Color   baseCol   = mats[i].HasProperty("_BaseColor")   ? mats[i].GetColor("_BaseColor")
                                      : mats[i].HasProperty("_Color")       ? mats[i].GetColor("_Color")
                                      : Color.white;

                    mats[i].shader = celShader;

                    // 텍스처·색상 복원
                    if (albedoTex != null && mats[i].HasProperty("_BaseMap"))
                        mats[i].SetTexture("_BaseMap", albedoTex);
                    if (emissTex  != null && mats[i].HasProperty("_EmissionMap"))
                        mats[i].SetTexture("_EmissionMap", emissTex);
                    if (mats[i].HasProperty("_BaseColor"))
                        mats[i].SetColor("_BaseColor", baseCol);

                    count++;
                }
                r.sharedMaterials = mats;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameSetup] ✓ Cel Shader({celShader.name}) 적용 — {count}개 머티리얼");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ③ 툰 머티리얼 생성
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/③ Create Toon Materials for Robots")]
    static void CreateToonMaterials()
    {
        var shader = Shader.Find("PersonalityBox/ToonLit");
        if (shader == null) { Debug.LogError("PersonalityBox/ToonLit 셰이더 없음. 프로젝트를 재컴파일 후 재시도."); return; }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))         AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Fighters")) AssetDatabase.CreateFolder("Assets/Materials", "Fighters");

        string[] robots = { "Robert", "Engie", "Catherine", "Paperman" };
        foreach (var robot in robots)
        {
            string savePath = $"Assets/Materials/Fighters/Toon_{robot}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(savePath) != null) continue;

            var mat = new Material(shader) { name = $"Toon_{robot}" };

            // Base Color 텍스처
            string basePath  = $"Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Textures/{robot}/{robot} Base Color.png";
            string emissPath = $"Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Textures/{robot}/{robot} Emission.png";

            var baseTex  = AssetDatabase.LoadAssetAtPath<Texture>(basePath);
            var emissTex = AssetDatabase.LoadAssetAtPath<Texture>(emissPath);

            if (baseTex  != null) mat.SetTexture("_BaseMap", baseTex);
            if (emissTex != null)
            {
                mat.SetTexture("_EmissionMap", emissTex);
                mat.SetColor("_EmissionColor", Color.white * 0.8f);
            }

            AssetDatabase.CreateAsset(mat, savePath);
            Debug.Log($"[GameSetup] ✓ 머티리얼 생성: {savePath}");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ════════════════════════════════════════════════════════════════════════
    // ④ Boxing Arena 머티리얼 강제 URP 변환 + 스케일 확대
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/④ Fix Arena (Materials + Scale)")]
    static void FixArena()
    {
        var arena = GameObject.Find("BoxingArena");
        if (arena == null) { Debug.LogError("[Fix Arena] BoxingArena 오브젝트를 찾을 수 없습니다."); return; }

        // ── 스케일 확대 (링이 너무 작으면 여기서 조절) ──────────────────
        arena.transform.localScale = new Vector3(3f, 1f, 3f);

        // 플로어 콜라이더도 크기 맞춤
        var floor = arena.transform.Find("_FloorCollider");
        if (floor != null)
        {
            var bc = floor.GetComponent<BoxCollider>();
            if (bc != null) bc.size = new Vector3(12f, 0.25f, 12f);
        }

        // ── 셰이더 강제 교체: 모든 비URP 셰이더 → URP Lit ──────────────────
        ForceAllMaterialsToURP(arena);

        // 스폰 포인트도 스케일에 맞게 이동
        var mm = Object.FindAnyObjectByType<MatchManager>();
        if (mm != null)
        {
            if (mm.spawnPoint1 != null) mm.spawnPoint1.position = new Vector3(-2.5f, 2f, 0f);
            if (mm.spawnPoint2 != null) mm.spawnPoint2.position = new Vector3( 2.5f, 2f, 0f);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Fix Arena] ✓ 스케일 3× 확대 + 머티리얼 URP 변환 완료");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ⑤ 로봇 FBX 내장 애니메이션 클립 목록 출력
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/⑤ Scan Robot Animations")]
    static void ScanAnimations()
    {
        string[] fbxPaths =
        {
            "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Models/Robert.fbx",
            "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Models/Engie.fbx",
            "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Models/Catherine.fbx",
            "Assets/Same Gev Dudios/Sci-Fi Robots Bundle/Models/Paperman.fbx",
        };

        Debug.Log("──── 로봇 FBX 내장 애니메이션 클립 목록 ────");
        foreach (var path in fbxPaths)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var clips  = assets.OfType<AnimationClip>()
                               .Where(c => !c.name.StartsWith("__preview__"))
                               .ToArray();

            if (clips.Length == 0)
                Debug.Log($"  {System.IO.Path.GetFileNameWithoutExtension(path)}: 클립 없음");
            else
                foreach (var c in clips)
                    Debug.Log($"  {System.IO.Path.GetFileNameWithoutExtension(path)} → [{c.name}]  ({c.length:F2}s, loop={c.isLooping})");
        }
        Debug.Log("──── 위 클립 이름으로 ⑥ Auto-Wire 실행 ────");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ⑥ Animator Controller에 클립 자동 연결
    //   FBX 클립 이름에 키워드(idle, walk, run, hit, die 등)가 포함되면 상태에 매핑
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/⑥ Auto-Wire Animations")]
    static void AutoWireAnimations()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimCtrlPath);
        if (ctrl == null)
        {
            Debug.LogError("[GameSetup] FighterAnimCtrl.controller 없음. 먼저 ① Setup Boxing Scene 실행.");
            return;
        }

        // 프로젝트 전체 AnimationClip 수집 (FBX 서브에셋 포함)
        string[] scanFolders = { "Assets/Same Gev Dudios", "Assets/Animations" };
        var allClips = AssetDatabase.FindAssets("t:AnimationClip", scanFolders)
            .SelectMany(guid =>
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>();
            })
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        if (allClips.Length == 0)
        {
            Debug.LogWarning("[GameSetup] 클립 없음. Animations 폴더에 FBX 클립이 있는지 확인하세요.");
            return;
        }

        Debug.Log($"[GameSetup] 클립 {allClips.Length}개 발견: " +
                  string.Join(", ", allClips.Take(8).Select(c => c.name)));

        // 상태 이름 → 클립 키워드 매핑 테이블
        var stateKeywords = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "Idle",      new[]{"idle","stand","neutral","t-pose","tpose"} },
            { "Move",      new[]{"walk","run","move","strafe","locomotion"} },
            { "Jab",       new[]{"jab","punch","attack","light","quick","cross"} },
            { "Hook",      new[]{"hook","swing","heavy"} },
            { "Uppercut",  new[]{"upper","uppercut","rising","lift"} },
            { "Block",     new[]{"block","guard","defend"} },
            { "Dodge",     new[]{"dodge","evade","roll","step","dash"} },
            { "Special",   new[]{"special","super","ultimate","power","hook","cross"} },
            { "Hit",       new[]{"hit","hurt","damage","stagger","react","big hit"} },
            { "KO",        new[]{"ko","knock","death","die","fall","lose"} },
            { "Awaken",    new[]{"awaken","power","rage","burst","awake"} },
        };

        var sm   = ctrl.layers[0].stateMachine;
        int wired = 0;

        foreach (var state in sm.states)
        {
            if (!stateKeywords.TryGetValue(state.state.name, out var keywords)) continue;
            if (state.state.motion != null) continue; // 이미 연결된 건 건드리지 않음

            // 클립 이름에 키워드가 포함된 것 찾기
            AnimationClip best = allClips.FirstOrDefault(c =>
                keywords.Any(k => c.name.ToLower().Contains(k)));

            if (best != null)
            {
                state.state.motion = best;
                wired++;
                Debug.Log($"[GameSetup] {state.state.name} ← {best.name}");
            }
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        if (wired == 0)
            Debug.LogWarning("[GameSetup] 매핑된 클립 없음. ⑤ Scan으로 클립 이름 확인 후 수동 연결하세요.");
        else
            Debug.Log($"[GameSetup] ✓ {wired}개 상태에 클립 자동 연결 완료");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ⑦ Animator 트랜지션 추가 + 클립 강제 연결
    //   CreateAnimatorController()가 상태만 만들고 트랜지션은 안 만들었으므로
    //   이 도구로 보완. 클립이 없으면 트랜지션만 추가.
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/⑦ Fix Animator Transitions")]
    static void FixAnimatorTransitions()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimCtrlPath);
        if (ctrl == null)
        {
            Debug.LogError("[GameSetup] FighterAnimCtrl.controller 없음. ① Setup Boxing Scene 먼저 실행.");
            return;
        }

        var sm = ctrl.layers[0].stateMachine;

        // ── 상태 참조 수집 ───────────────────────────────────────────────────
        AnimatorState idle = null, move = null, jab = null, hook = null,
                      upper = null, block = null, dodge = null, special = null,
                      hit = null, ko = null, awaken = null;
        foreach (var cs in sm.states)
        {
            switch (cs.state.name)
            {
                case "Idle":     idle    = cs.state; break;
                case "Move":     move    = cs.state; break;
                case "Jab":      jab     = cs.state; break;
                case "Hook":     hook    = cs.state; break;
                case "Uppercut": upper   = cs.state; break;
                case "Block":    block   = cs.state; break;
                case "Dodge":    dodge   = cs.state; break;
                case "Special":  special = cs.state; break;
                case "Hit":      hit     = cs.state; break;
                case "KO":       ko      = cs.state; break;
                case "Awaken":   awaken  = cs.state; break;
            }
        }

        // ── AnyState 트랜지션 초기화 ─────────────────────────────────────────
        foreach (var t in sm.anyStateTransitions)
            sm.RemoveAnyStateTransition(t);

        // AnyState → Hit / KO / Awaken (트리거)
        void AddAny(AnimatorState to, string param)
        {
            if (to == null) return;
            var t = sm.AddAnyStateTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0, param);
            t.duration = 0f;
            t.canTransitionToSelf = false;
            t.hasExitTime = false;
        }
        AddAny(hit,    "Hit");
        AddAny(ko,     "KO");
        AddAny(awaken, "Awaken");

        // AnyState → 공격/회피 (트리거)
        string[] atkTriggers = { "Jab", "Hook", "Uppercut", "Dodge", "Special" };
        AnimatorState[] atkStates = { jab, hook, upper, dodge, special };
        for (int i = 0; i < atkTriggers.Length; i++)
        {
            if (atkStates[i] == null) continue;
            var t = sm.AddAnyStateTransition(atkStates[i]);
            t.AddCondition(AnimatorConditionMode.If, 0, atkTriggers[i]);
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            t.hasExitTime = false;

            // 공격/회피 → Idle (exitTime)
            if (idle != null)
            {
                var back = atkStates[i].AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime    = 0.85f;
                back.duration    = 0.15f;
            }
        }

        // Hit → Idle
        if (hit != null && idle != null)
        {
            var t = hit.AddTransition(idle);
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
        }

        // Idle ↔ Move
        if (idle != null && move != null)
        {
            // 기존 트랜지션 제거 후 재생성 (중복 방지)
            foreach (var t in idle.transitions) idle.RemoveTransition(t);
            var toMove = idle.AddTransition(move);
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "MoveSpeed");
            toMove.duration = 0.1f; toMove.hasExitTime = false;

            foreach (var t in move.transitions) move.RemoveTransition(t);
            var toIdle = move.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");
            toIdle.duration = 0.1f; toIdle.hasExitTime = false;
        }

        // Idle/Move ↔ Block
        if (block != null)
        {
            foreach (AnimatorState src in new[]{ idle, move })
            {
                if (src == null) continue;
                var toBlock = src.AddTransition(block);
                toBlock.AddCondition(AnimatorConditionMode.If, 0, "IsBlocking");
                toBlock.duration = 0.05f; toBlock.hasExitTime = false;
            }
            foreach (AnimatorState dst in new[]{ idle, move })
            {
                if (dst == null) continue;
                var fromBlock = block.AddTransition(dst == move ? idle : idle);
                fromBlock.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBlocking");
                fromBlock.duration = 0.05f; fromBlock.hasExitTime = false;
                break; // Idle 하나로만 복귀
            }
        }

        // ── 클립 연결 (Assets/Same Gev Dudios + Assets/Animations 스캔) ─────
        var allClips = AssetDatabase.FindAssets("t:AnimationClip",
            new[] { "Assets/Same Gev Dudios", "Assets/Animations" })
            .SelectMany(guid =>
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>();
            })
            .Where(c => !c.name.StartsWith("__preview__"))
            .OrderBy(c => c.name)
            .ToArray();

        if (allClips.Length > 0)
        {
            // 키워드 매핑 우선 시도
            var stateKeywords = new System.Collections.Generic.Dictionary<string, string[]>
            {
                { "Idle",      new[]{"idle","stand","neutral","tpose","t-pose","default"} },
                { "Move",      new[]{"walk","run","move","strafe","locomotion"} },
                { "Jab",       new[]{"jab","punch","attack","light","quick","cross"} },
                { "Hook",      new[]{"hook","heavy","combo","swing"} },
                { "Uppercut",  new[]{"upper","uppercut","rising","lift","kick"} },
                { "Block",     new[]{"block","guard","defend","shield"} },
                { "Dodge",     new[]{"dodge","evade","roll","step","dash"} },
                { "Special",   new[]{"special","super","power","ultimate","hook"} },
                { "Hit",       new[]{"hit","hurt","damage","stagger","react","pain","big hit"} },
                { "KO",        new[]{"ko","knock","death","die","fall","lose","down"} },
                { "Awaken",    new[]{"awaken","rage","burst","awake","power"} },
            };

            int wired = 0;
            foreach (var cs in sm.states)
            {
                if (cs.state.motion != null) continue;
                if (!stateKeywords.TryGetValue(cs.state.name, out var kws)) continue;
                var clip = allClips.FirstOrDefault(c =>
                    kws.Any(k => c.name.ToLower().Contains(k)));
                if (clip != null)
                { cs.state.motion = clip; wired++; Debug.Log($"[Anim] {cs.state.name} ← {clip.name}"); }
            }

            // Idle이 여전히 비어 있으면 첫 번째 클립으로 채우기
            if (idle != null && idle.motion == null)
            { idle.motion = allClips[0]; Debug.Log($"[Anim] Idle(fallback) ← {allClips[0].name}"); wired++; }

            Debug.Log($"[GameSetup] ✓ 트랜지션 추가 완료 | 클립 {wired}개 연결");
        }
        else
        {
            Debug.Log("[GameSetup] ✓ 트랜지션 추가 완료 (클립 없음 — ⑤ Scan으로 FBX 확인 필요)");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 📷 카메라 전환: 1인칭 ↔ 3인칭 관전
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/📷 Switch to Spectator Camera (3인칭 관전)")]
    static void SwitchToSpectatorCamera()
    {
        var camGo = GameObject.FindWithTag("MainCamera")
                 ?? Object.FindAnyObjectByType<Camera>()?.gameObject;
        if (camGo == null) { Debug.LogError("[Camera] Main Camera를 찾을 수 없습니다."); return; }

        var fp = camGo.GetComponent<FirstPersonCamera>();
        if (fp != null) Object.DestroyImmediate(fp);

        var mm = Object.FindAnyObjectByType<MatchManager>();
        var follow = camGo.GetComponent<CameraFollow>() ?? camGo.AddComponent<CameraFollow>();

        if (mm != null)
        {
            follow.fighter1 = mm.fighter1;
            follow.fighter2 = mm.fighter2;
        }
        else
        {
            var fighters = Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None);
            if (fighters.Length >= 1) follow.fighter1 = fighters[0];
            if (fighters.Length >= 2) follow.fighter2 = fighters[1];
        }

        float ringY  = DetectRingFloorY();
        float halfW  = DetectRingHalfWidth();
        float camDist = halfW > 0.1f ? halfW * 3f : 9f;
        camGo.transform.position = new Vector3(0f, (ringY > 0.01f ? ringY : 0f) + 4.5f, -camDist);
        camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        if (camGo.GetComponent<Camera>() != null)
            camGo.GetComponent<Camera>().fieldOfView = 60f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Camera] 3인칭 관전 카메라로 전환 완료. Fighter1={follow.fighter1?.name}, Fighter2={follow.fighter2?.name}");
        Debug.Log("[Camera] Ctrl+S 저장 → Play ▶");
    }

    [MenuItem("Tools/PersonalityBox/📷 Switch to First-Person Camera (1인칭)")]
    static void SwitchToFirstPersonCamera()
    {
        var camGo = GameObject.FindWithTag("MainCamera")
                 ?? Object.FindAnyObjectByType<Camera>()?.gameObject;
        if (camGo == null) { Debug.LogError("[Camera] Main Camera를 찾을 수 없습니다."); return; }

        var follow = camGo.GetComponent<CameraFollow>();
        if (follow != null) Object.DestroyImmediate(follow);

        var mm = Object.FindAnyObjectByType<MatchManager>();
        var fp = camGo.GetComponent<FirstPersonCamera>() ?? camGo.AddComponent<FirstPersonCamera>();

        Fighter player = null;
        if (mm != null) player = mm.fighter1;
        if (player == null)
        {
            foreach (var f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None))
            {
                var ph = f.GetComponent<PlayerInputHandler>();
                if (ph != null && ph.enabled) { player = f; break; }
            }
        }
        fp.fighter1 = player;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Camera] 1인칭 카메라로 전환 완료. Fighter1={player?.name}");
        Debug.Log("[Camera] Ctrl+S 저장 → Play ▶");
    }

    // ════════════════════════════════════════════════════════════════════════
    // ⑧ 루트 트랜스폼 임시 클립 생성 (FBX 클립 없을 때 대체용)
    //   골격 애니메이션은 없지만 캐릭터 전체가 앞뒤로 움직여 반응을 표현.
    //   클립을 만든 뒤 ⑦ Fix Animator Transitions 를 재실행하면 자동 연결.
    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/PersonalityBox/⑧ Create Root-Motion Placeholder Clips")]
    static void CreatePlaceholderClips()
    {
        const string dir = "Assets/Animations/Placeholder";
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Animations", "Placeholder");

        // ── 클립 생성 헬퍼 ────────────────────────────────────────────────
        AnimationClip Make(string clipName, float dur, bool loop,
            (float t, float v)[] py = null,
            (float t, float v)[] pz = null,
            (float t, float v)[] rx = null)
        {
            var clip = new AnimationClip { name = clipName };
            clip.frameRate = 30f;
            if (loop)
            {
                var s = AnimationUtility.GetAnimationClipSettings(clip);
                s.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, s);
            }
            void SetCurve(string prop, (float t, float v)[] keys)
            {
                if (keys == null) return;
                var kfs = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++) kfs[i] = new Keyframe(keys[i].t, keys[i].v);
                clip.SetCurve("", typeof(Transform), prop, new AnimationCurve(kfs));
            }
            SetCurve("localPosition.y", py);
            SetCurve("localPosition.z", pz);
            SetCurve("localEulerAngles.x", rx);

            string path = $"{dir}/{clipName}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        float T = 1.6f;  // 숨쉬기 주기

        // Idle — 상하 호흡
        var clipIdle = Make("Idle", T, true,
            py: new[]{(0f,0f),(T*0.4f,0.025f),(T*0.8f,-0.01f),(T,0f)});

        // Move — 좌우+상하 걷기 밥
        var clipMove = Make("Move", 0.6f, true,
            py: new[]{(0f,0f),(0.15f,0.02f),(0.3f,0f),(0.45f,0.02f),(0.6f,0f)},
            pz: new[]{(0f,0f),(0.3f,0.015f),(0.6f,0f)});

        // Jab — 앞으로 찌르기
        var clipJab = Make("Jab", 0.4f, false,
            pz: new[]{(0f,0f),(0.12f,0.25f),(0.28f,0.25f),(0.4f,0f)});

        // Hook — 크게 앞으로
        var clipHook = Make("Hook", 0.5f, false,
            pz: new[]{(0f,0f),(0.15f,0.32f),(0.32f,0.32f),(0.5f,0f)});

        // Uppercut — 위로 들어올리기
        var clipUpper = Make("Uppercut", 0.45f, false,
            py: new[]{(0f,0f),(0.1f,0.18f),(0.25f,0.18f),(0.45f,0f)},
            pz: new[]{(0f,0f),(0.1f,0.2f),(0.25f,0.2f),(0.45f,0f)});

        // Hit — 뒤로 밀림
        var clipHit = Make("Hit", 0.35f, false,
            pz: new[]{(0f,0f),(0.08f,-0.28f),(0.2f,-0.28f),(0.35f,0f)});

        // Block — 앞으로 숙임
        var clipBlock = Make("Block", 0.3f, false,
            pz: new[]{(0f,0f),(0.15f,0.1f),(0.3f,0.1f)},
            rx: new[]{(0f,0f),(0.15f,-12f),(0.3f,-12f)});

        // Dodge — 뒤로 빠짐
        var clipDodge = Make("Dodge", 0.3f, false,
            pz: new[]{(0f,0f),(0.1f,-0.35f),(0.2f,-0.35f),(0.3f,0f)});

        // KO — 앞으로 쓰러짐
        var clipKO = Make("KO", 0.6f, false,
            rx: new[]{(0f,0f),(0.3f,50f),(0.6f,80f)},
            py: new[]{(0f,0f),(0.3f,-0.4f),(0.6f,-0.9f)});

        // Awaken — 점프 이펙트
        var clipAwaken = Make("Awaken", 0.5f, false,
            py: new[]{(0f,0f),(0.15f,0.2f),(0.35f,0.2f),(0.5f,0f)});

        AssetDatabase.SaveAssets();

        // ── Animator Controller에 연결 ────────────────────────────────────
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimCtrlPath);
        if (ctrl == null) { Debug.LogWarning("[Clip] AnimCtrl 없음. ① Setup 먼저 실행."); return; }

        var pairs = new System.Collections.Generic.Dictionary<string, AnimationClip>
        {
            {"Idle",     clipIdle},   {"Move",     clipMove},
            {"Jab",      clipJab},    {"Hook",     clipHook},   {"Uppercut", clipUpper},
            {"Hit",      clipHit},    {"Block",    clipBlock},  {"Dodge",    clipDodge},
            {"KO",       clipKO},     {"Awaken",   clipAwaken}, {"Special",  clipJab},
        };

        foreach (var cs in ctrl.layers[0].stateMachine.states)
        {
            if (pairs.TryGetValue(cs.state.name, out var c))
            { cs.state.motion = c; Debug.Log($"[Clip] {cs.state.name} ← {c.name}"); }
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameSetup] ✓ 임시 클립 생성 + 연결 완료 → Play로 확인");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 내부 헬퍼
    // ════════════════════════════════════════════════════════════════════════

    static void AddFloorCollider(GameObject arenaGo)
    {
        // 기존 콜라이더 있으면 스킵
        if (arenaGo.GetComponentInChildren<Collider>() != null) return;

        // 메시 콜라이더 전략: MeshFilter가 있는 하위 오브젝트에 추가
        // 너무 많아지면 링 바닥처럼 보이는 것만 선택
        var meshFilters = arenaGo.GetComponentsInChildren<MeshFilter>();
        bool added = false;
        foreach (var mf in meshFilters)
        {
            string nameLow = mf.gameObject.name.ToLower();
            if (nameLow.Contains("floor") || nameLow.Contains("ring") || nameLow.Contains("stage") || nameLow.Contains("ground"))
            {
                mf.gameObject.AddComponent<MeshCollider>();
                added = true;
                Debug.Log($"[GameSetup] MeshCollider 추가: {mf.gameObject.name}");
            }
        }

        // 이름으로 찾지 못하면 평면 BoxCollider로 대응
        if (!added)
        {
            var floorGo = new GameObject("_FloorCollider");
            floorGo.transform.SetParent(arenaGo.transform);
            floorGo.transform.localPosition = new Vector3(0f, 0.9f, 0f); // 링 높이 근사치
            var bc = floorGo.AddComponent<BoxCollider>();
            bc.size   = new Vector3(12f, 0.25f, 12f);
            bc.center = Vector3.zero;
            Debug.Log("[GameSetup] _FloorCollider BoxCollider 추가 (y=0.9). 파이터가 뜨거나 묻히면 y값 조정 필요.");
        }
    }

    static GameObject CreateFighter(
        string prefabPath, string name, Vector3 position, float faceAngle,
        string dataPath, string matPath, AnimatorController animCtrl, bool isPlayer)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // PrefabUtility.InstantiatePrefab 대신 Object.Instantiate 사용
        // → 프리팹 연결이 끊겨 AddComponent/DestroyImmediate 제한 없이 자유롭게 수정 가능
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            // 이름에서 "(Clone)" 제거
            go.name = name;
        }
        else
        {
            go = new GameObject(name);
        }

        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, faceAngle, 0f);
        go.transform.localScale = Vector3.one;

        // URP 머티리얼 적용
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;

        // Animator 연결
        var anim = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        if (anim.runtimeAnimatorController == null)
            anim.runtimeAnimatorController = animCtrl;

        // CharacterController 와 Rigidbody 는 공존 불가 — 모든 계층에서 제거
        foreach (var cc in go.GetComponentsInChildren<CharacterController>(true))
            Object.DestroyImmediate(cc);

        // Rigidbody — 루트에 추가 (없으면)
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation         = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearDamping          = 1f;
        }
        else
        {
            Debug.LogError($"[CreateFighter] {name}: Rigidbody 추가 실패.");
        }

        // CapsuleCollider — 루트에 추가 (없으면)
        CapsuleCollider col = go.GetComponent<CapsuleCollider>();
        if (col == null) col = go.AddComponent<CapsuleCollider>();
        if (col != null)
        {
            col.height = 2f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 1f, 0f);
        }
        else
        {
            Debug.LogError($"[CreateFighter] {name}: CapsuleCollider 추가 실패.");
        }

        // Fighter 스크립트 — 루트에 추가
        Fighter fighter = go.GetComponent<Fighter>();
        if (fighter == null) fighter = go.AddComponent<Fighter>();
        var fdata = AssetDatabase.LoadAssetAtPath<FighterData>(dataPath);
        if (fdata != null) fighter.data = fdata;
        fighter.moveSpeed = 4f;

        // bodyRenderer 자동 설정
        if (fighter.bodyRenderer == null)
            fighter.bodyRenderer = go.GetComponentInChildren<Renderer>();

        // PunchOrigin (기존 있으면 재사용)
        var existingPO = go.transform.Find("PunchOrigin");
        if (existingPO == null)
        {
            var poGo = new GameObject("PunchOrigin");
            poGo.transform.SetParent(go.transform);
            poGo.transform.localPosition = new Vector3(0.4f, 1.4f, 0.7f);
            fighter.punchOrigin = poGo.transform;
        }
        else
        {
            fighter.punchOrigin = existingPO;
        }

        // 플레이어 / AI 컴포넌트
        if (isPlayer)
        {
            // AI 제거
            foreach (var ai in go.GetComponents<AIController>())
                Object.DestroyImmediate(ai);
            var inp = go.GetComponent<PlayerInputHandler>() ?? go.AddComponent<PlayerInputHandler>();
            inp.scheme  = PlayerInputHandler.InputScheme.Player1;
            inp.enabled = true;
        }
        else
        {
            // PlayerInput 제거
            foreach (var inp in go.GetComponents<PlayerInputHandler>())
                Object.DestroyImmediate(inp);
            var ai = go.GetComponent<AIController>() ?? go.AddComponent<AIController>();
            ai.behaviour = AIBehaviour.Balanced;
            ai.enabled   = true;
        }

        return go;
    }

    static AnimatorController CreateAnimatorController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimCtrlPath);
        if (existing != null) return existing;

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(AnimCtrlPath);
        var root = ctrl.layers[0].stateMachine;

        ctrl.AddParameter("MoveSpeed",    AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsBlocking",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("AttackHeight", AnimatorControllerParameterType.Int);
        foreach (var t in new[]{"Jab","Hook","Uppercut","Dodge","Special","Hit","KO","Awaken"})
            ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

        foreach (var s in new[]{"Idle","Move","Jab","Hook","Uppercut","Block","Dodge","Special","Hit","KO","Awaken"})
            root.AddState(s);

        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static Transform MakeSpawnPoint(GameObject parent, string n, Vector3 pos, float yRot)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        return go.transform;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        // Unity-null (파괴된 컴포넌트 참조) 처리
        if (c == null || c.Equals(null)) c = go.AddComponent<T>();
        return c;
    }

    // 링 반폭 (파이터 스폰 X 거리) 감지
    static float DetectRingHalfWidth()
    {
        string[] priority = { "ringfloor", "ring floor", "ringplatform", "canvas", "stage", "ring" };
        Renderer bestR = null;
        float bestArea = 0f;

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = r.gameObject.name.ToLower();
            foreach (var p in priority)
            {
                if (!n.Contains(p)) continue;
                float area = r.bounds.size.x * r.bounds.size.z;
                if (area > bestArea) { bestArea = area; bestR = r; }
            }
        }

        // 이름으로 못 찾으면 납작하고 4~40m 너비인 최상위 메시 탐색
        if (bestR == null)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.gameObject.name.ToLower();
                if (n.Contains("seat") || n.Contains("bleacher") || n.Contains("wall") ||
                    n.Contains("ceiling") || n.Contains("sky")) continue;
                Bounds b = r.bounds;
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w < 4f || w > 40f || b.size.y > w * 0.25f) continue;
                float area = b.size.x * b.size.z;
                if (area > bestArea) { bestArea = area; bestR = r; }
            }
        }

        if (bestR != null)
        {
            float halfW = Mathf.Max(bestR.bounds.size.x, bestR.bounds.size.z) * 0.35f;
            halfW = Mathf.Clamp(halfW, 2f, 5f);
            Debug.Log($"[Repair] 링 크기: {bestR.gameObject.name} W={Mathf.Max(bestR.bounds.size.x,bestR.bounds.size.z):F1}m → 스폰X=±{halfW:F2}");
            return halfW;
        }
        return 0f;
    }

    // 씬의 렌더러를 스캔해 링 바닥 Y를 추정 (에디터 전용, 레이캐스트 없음)
    // ════════════════════════════════════════════════════════════════════════
    // ════════════════════════════════════════════════════════════════════════
    // HUD 오브젝트 배치 — UI 생성은 BoxingHUD.cs가 런타임에 자동 처리
    // ════════════════════════════════════════════════════════════════════════
    static void EnsureHUD(Fighter fighter1, Fighter fighter2)
    {
        var hud = Object.FindAnyObjectByType<PersonalityBox.UI.BoxingHUD>();
        if (hud == null)
        {
            var go = new GameObject("HUD_Canvas");
            hud = go.AddComponent<PersonalityBox.UI.BoxingHUD>();
            Debug.Log("[Repair] BoxingHUD 추가 완료 — 런타임 시작 시 UI 자동 생성");
        }
        hud.fighter1 = fighter1;
        hud.fighter2 = fighter2;
    }

    static float DetectRingFloorY()
    {
        // 1순위: "ring", "stage", "canvas", "floor" 포함 이름 중 가장 Y가 높고 납작한 것
        string[] priority = { "ringfloor", "ring floor", "ringplatform", "canvas", "stage", "ring" };
        float best = float.MinValue;
        Renderer bestR = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = r.gameObject.name.ToLower();
            foreach (var p in priority)
            {
                if (!n.Contains(p)) continue;
                if (r.bounds.max.y > best)
                {
                    best = r.bounds.max.y;
                    bestR = r;
                }
            }
        }
        if (bestR != null)
        {
            Debug.Log($"[Repair] 링 오브젝트: {bestR.gameObject.name}, 상단Y={best:F2}");
            return best;
        }

        // 2순위: 4~40m 너비이고 납작한(Y 두께 < 너비/4) 최상위 오브젝트
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("seat") || n.Contains("bleacher") || n.Contains("wall") ||
                n.Contains("ceiling") || n.Contains("sky")) continue;
            Bounds b = r.bounds;
            float w = Mathf.Max(b.size.x, b.size.z);
            if (w < 4f || w > 40f) continue;
            if (b.size.y > w * 0.25f) continue;
            if (b.max.y > best)
            {
                best = b.max.y;
                bestR = r;
            }
        }
        if (bestR != null)
        {
            Debug.Log($"[Repair] 링 후보: {bestR.gameObject.name}, 상단Y={best:F2}");
            return best;
        }
        return 0f;
    }
}
