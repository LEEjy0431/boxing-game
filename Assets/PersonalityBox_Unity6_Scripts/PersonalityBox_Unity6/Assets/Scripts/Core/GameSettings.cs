using UnityEngine;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 씬 간 유지되는 싱글턴. 모드 선택·파이터 선택·오디오·화면 설정을 보관.
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        [Header("Match")]
        public GameMode    mode;
        public FighterData fighter1Data;
        public FighterData fighter2Data;

        [Header("Audio")]
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Display")]
        public int  resolutionIndex = 0;
        public bool fullscreen      = true;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ApplyDisplay()
        {
            Resolution[] res = Screen.resolutions;
            if (resolutionIndex >= 0 && resolutionIndex < res.Length)
                Screen.SetResolution(res[resolutionIndex].width, res[resolutionIndex].height, fullscreen);
            else
                Screen.fullScreen = fullscreen;
        }
    }
}
