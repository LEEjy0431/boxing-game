using System.Collections;
using UnityEngine;

namespace PersonalityBox.Core
{
    /// <summary>
    /// 게임 전체 사운드 관리 싱글턴.
    /// BGM(루프) + SFX(원샷) 두 채널을 관리하며 씬 간 유지됨.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        // ── BGM ──────────────────────────────────────────────────────────────
        [Header("BGM")]
        public AudioClip lobbyBGM;
        public AudioClip fightBGM;

        // ── 링 효과음 ────────────────────────────────────────────────────────
        [Header("Ring SFX")]
        public AudioClip bellStart;     // 경기/라운드 시작 종소리
        public AudioClip bellEnd;       // 라운드 종료 종소리 (선택)
        public AudioClip crowdCheer;    // 관중 환호 (선택)

        // ── UI 효과음 ────────────────────────────────────────────────────────
        [Header("UI SFX")]
        public AudioClip uiClick;       // 버튼 클릭음

        // ── 볼륨 ─────────────────────────────────────────────────────────────
        [Header("Volume")]
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        AudioSource _bgm;
        AudioSource _sfx;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bgm        = gameObject.AddComponent<AudioSource>();
            _bgm.loop   = true;
            _bgm.volume = bgmVolume;

            _sfx        = gameObject.AddComponent<AudioSource>();
            _sfx.volume = sfxVolume;
        }

        void Start()
        {
            // 로비 BGM 자동 시작 (SoundManager 자체에서 관리)
            if (lobbyBGM != null)
                StartCoroutine(FadeInBGM(lobbyBGM));
        }

        // ── BGM 제어 ─────────────────────────────────────────────────────────

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || _bgm.clip == clip) return;
            _bgm.clip = clip;
            _bgm.Play();
        }

        public void StopBGM() => _bgm.Stop();

        public IEnumerator FadeInBGM(AudioClip clip, float duration = 1f)
        {
            _bgm.clip   = clip;
            _bgm.volume = 0f;
            _bgm.Play();
            float t = 0f;
            while (t < duration)
            {
                t           += Time.unscaledDeltaTime;
                _bgm.volume  = Mathf.Lerp(0f, bgmVolume, t / duration);
                yield return null;
            }
            _bgm.volume = bgmVolume;
        }

        // ── SFX 제어 ─────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfx == null) return;
            _sfx.PlayOneShot(clip, sfxVolume);
        }

        public void PlayBell()    => PlaySFX(bellStart);
        public void PlayUIClick() => PlaySFX(uiClick);

        // ── 볼륨 조절 ────────────────────────────────────────────────────────

        public void SetBGMVolume(float v)
        {
            bgmVolume   = Mathf.Clamp01(v);
            _bgm.volume = bgmVolume;
            if (GameSettings.Instance != null) GameSettings.Instance.bgmVolume = bgmVolume;
        }

        public void SetSFXVolume(float v)
        {
            sfxVolume   = Mathf.Clamp01(v);
            _sfx.volume = sfxVolume;
            if (GameSettings.Instance != null) GameSettings.Instance.sfxVolume = sfxVolume;
        }

        public void ApplySavedVolume()
        {
            if (GameSettings.Instance == null) return;
            SetBGMVolume(GameSettings.Instance.bgmVolume);
            SetSFXVolume(GameSettings.Instance.sfxVolume);
        }
    }
}
