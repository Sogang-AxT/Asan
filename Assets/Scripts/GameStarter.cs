using System.Collections;
using UnityEngine;
using TMPro;

public class GameStarter : MonoBehaviour
{
    public static bool GameStarted = false;

    [Header("UI Elements")]
    public GameObject startButtonUI;
    public TextMeshProUGUI countdownText;

    [Header("Guide Object")]
    public GameObject guideObject;

    [Header("Audio (BGM)")]
    public AudioSource musicSource;            // BGM을 재생할 AudioSource (카메라/매니저 오브젝트 등)
    public AudioClip backgroundMusic;          // 배경음악 클립
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    public bool loopMusic = true;
    public float fadeInSeconds = 1.5f;         // 0 이면 즉시 재생
    [Header("Audio (SFX)")]
    public AudioSource sfxSource;              // 효과음 재생용 AudioSource
    public AudioClip startButtonSFX;  
    void Start()
    {
        startButtonUI.SetActive(true);
        countdownText.gameObject.SetActive(false);

        // 오디오 소스 안전설정
        if (musicSource)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = loopMusic;
            // 미리 클립을 넣어두되 볼륨 0으로 준비 (페이드인용)
            if (backgroundMusic) musicSource.clip = backgroundMusic;
            musicSource.volume = 0f;
        }
    }

    public void OnStartButtonPressed()
    {
        // 버튼 눌렀을 때 효과음 재생
        if (sfxSource && startButtonSFX)
            sfxSource.PlayOneShot(startButtonSFX);

        startButtonUI.SetActive(false);
        if (guideObject != null)
            guideObject.SetActive(false);

        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        // ▶ 카운트다운 동안 게임 멈춤 (UI는 숨김 상태)
        Time.timeScale = 0f;
        countdownText.gameObject.SetActive(true);

        int count = 3;
        while (count > 0)
        {
            countdownText.text = count.ToString();
            yield return WaitForRealSeconds(1f);   // unscaled 시간 사용
            count--;
        }

        countdownText.text = "START!";
        yield return WaitForRealSeconds(0.8f);

        countdownText.gameObject.SetActive(false);

        // ▶ 게임 시작!
        Time.timeScale = 1f;
        GameStarted = true;

        // ▶ BGM 재생 시작 (페이드인)
        StartBackgroundMusic();
    }

    private void StartBackgroundMusic()
    {
        if (!musicSource || !backgroundMusic) return;

        // 혹시 다른 소리 재생 중이면 정지 후 세팅
        musicSource.Stop();
        musicSource.clip = backgroundMusic;
        musicSource.loop = loopMusic;

        if (fadeInSeconds > 0f)
        {
            musicSource.volume = 0f;
            musicSource.Play();
            StartCoroutine(FadeInMusic());
        }
        else
        {
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }

    private IEnumerator FadeInMusic()
    {
        float t = 0f;
        // 타임스케일 영향을 받지 않도록 unscaledDeltaTime 사용
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fadeInSeconds);
            musicSource.volume = Mathf.Lerp(0f, musicVolume, u);
            yield return null;
        }
        musicSource.volume = musicVolume;
    }

    private IEnumerator WaitForRealSeconds(float time)
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime < start + time)
            yield return null;
    }
}
