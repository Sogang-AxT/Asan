using UnityEngine;
using TMPro;

public class PlayTimer : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timeText;

    [Header("옵션")]
    [Tooltip("GameStarter.GameStarted가 true 되는 순간 자동 시작")]
    public bool autoStartOnGameStarted = true;

    private float elapsedTime = 0f;
    private bool running = false;

    public float ElapsedTime => elapsedTime;

    void Awake()
    {
        // 초기 화면에 00:00:00 표시 (running 여부와 무관)
        if (timeText) timeText.text = "00:00:00";
    }

    void Start()
    {
        // 자동시작 옵션이면 GameStarted 기다렸다가 스타트
        if (autoStartOnGameStarted) StartCoroutine(WaitAndStartWhenGameStarts());
    }

    private System.Collections.IEnumerator WaitAndStartWhenGameStarts()
    {
        // GameStarted가 true가 될 때까지 대기
        while (!GameStarter.GameStarted) yield return null;
        ResetAndStart();
    }

    void Update()
    {
        if (!running || !GameStarter.GameStarted) return;

        // 일시정지 중에는 멈추게 하려면 deltaTime, 
        // 일시정지와 무관하게 계속 가게 하려면 unscaledDeltaTime 사용
        elapsedTime += Time.deltaTime;

        int hours   = Mathf.FloorToInt(elapsedTime / 3600f);
        int minutes = Mathf.FloorToInt((elapsedTime % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);

        if (timeText)
            timeText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    public void ResetAndStart()
    {
        elapsedTime = 0f;
        running = true;
        if (timeText) timeText.text = "00:00:00";
    }

    public void Stop()
    {
        running = false;
    }
}
