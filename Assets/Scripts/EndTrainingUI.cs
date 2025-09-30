using UnityEngine;
using TMPro;
using System.Collections;

public class EndTrainingUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root; // 패널 전체 오브젝트

    [Header("Texts")]
    public TextMeshProUGUI timeText;     // "훈련 시간"
    public TextMeshProUGUI distanceText; // "이동 거리"
    //public TextMeshProUGUI countText;    // "운동 횟수"
    public TextMeshProUGUI CountText; // "운동 횟수"
    public TextMeshProUGUI AngleText;    // "운동 각도"
    public TextMeshProUGUI ScoreText;    // "총 점수"

    [Header("Score Settings")]
    private float baseTimeSec = 60f; 

    // 총 점수 애니메이션 설정
    [Header("Score Anim Settings")]
    [SerializeField] private float scoreAnimDuration = 2.0f;  // 총 소요 시간(초)
    [SerializeField] private int   scoreAnimStart = 0;        // 시작 표시 값(0 추천)

    Coroutine _scoreAnimCo; // 중복 방지용

    // 운동각도 평균 전용
    public void Show(float timeSec, float distanceM, float avgLeftDeg, float avgRightDeg, int leftCnt, int rightCnt)
    {
        if (root) root.SetActive(true);

        timeText.text     = FormatTime(timeSec);
        distanceText.text = $"{distanceM:0} m";
        //ountText.text    = $"{paddleCount} 개";
        if (CountText) CountText.text = $"좌-{leftCnt}회 / 우-{rightCnt}회";
        if (AngleText)  AngleText.text  = $"좌-{avgLeftDeg:0.#}° / 우-{avgRightDeg:0.#}°";
        // 점수 계산 (최종 목표값)
        float score = CalculateScore(avgLeftDeg, avgRightDeg, timeSec);
        int targetScore = Mathf.RoundToInt(score);

        // 즉시 텍스트를 넣는 대신, 애니메이션으로 올리기
        if (ScoreText)
        {
            // 기존 코루틴 정리
            if (_scoreAnimCo != null) StopCoroutine(_scoreAnimCo);
            _scoreAnimCo = StartCoroutine(AnimateScore(scoreAnimStart, targetScore, scoreAnimDuration));
        }

        // 게임 일시정지(점수 애니메이션은 unscaled time으로 동작)
        Time.timeScale = 0f;
    }


    public void Hide()
    {
        if (root) root.SetActive(false);
        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    static string FormatTime(float t)
    {
        int h = (int)(t / 3600f);          // 시간
        int m = (int)((t % 3600f) / 60f);  // 분
        int s = (int)(t % 60f);            // 초
        return $"{h:00}:{m:00}:{s:00}";
    }

    float CalculateScore(float avgLeftDeg, float avgRightDeg, float elapsedTimeSec)
    {
        float left  = Mathf.Round(avgLeftDeg  * 10f) / 10f;
        float right = Mathf.Round(avgRightDeg * 10f) / 10f;

        int   usedTimeInt = (int)elapsedTimeSec;
        float usedTime    = Mathf.Max(usedTimeInt, 1); // 0 나눗셈 보호

        float angleSum   = left + right;
        float timeFactor = baseTimeSec / usedTime; 
        return angleSum * 2f * timeFactor * 10f;
    }

    // 숫자 카운트업 코루틴 (ease-out 포함, unscaled time)
    IEnumerator AnimateScore(int from, int to, float duration)
    {
        if (duration <= 0f)
        {
            ScoreText.text = $"{to}점";
            yield break;
        }

        float t = 0f;
        int lastShown = from;
        ScoreText.text = $"{from}점";

        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // TimeScale=0에서도 진행
            float u = Mathf.Clamp01(t / duration);

            // easeOutCubic
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            int val = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            if (val != lastShown)
            {
                lastShown = val;
                ScoreText.text = $"{val}점";
            }
            yield return null;
        }

        ScoreText.text = $"{to}점";
        _scoreAnimCo = null;
    }
}
