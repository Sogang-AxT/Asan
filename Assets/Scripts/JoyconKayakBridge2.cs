using UnityEngine;
using RageRunGames.KayakController;

public class JoyconKayakBridge2 : MonoBehaviour
{
    public enum JoyconSide { Left, Right }

    [Header("Setup")]
    public JoyconSide side;
    public KayakController Kayak;

    [Header("Input Settings")]
    [Tooltip("X축 반전이 필요하면 체크")]
    public bool invertX = false;
    [Tooltip("기준 각도에서 보정할 오프셋 (도 단위)")]
    public float xOffset = 0f;

    [Header("Trigger Thresholds (deg, relative to baseline)")]
    [Tooltip("스몰 트리거 기준 각도 (음수는 아래 방향)")]
    public float smallTriggerAngle = -30f;
    [Tooltip("풀 트리거 기준 각도 (음수는 아래 방향, 스몰보다 더 큼)")]
    public float fullTriggerAngle = -60f;
    [Tooltip("해제 기준 각도 (트리거 후 올라올 때)")]
    public float releaseAngle = -15f;

    [Header("Cooldown / Sensitivity")]
    [Tooltip("트리거 후 다음 입력까지 대기시간(초)")]
    public float cooldownSec = 0.3f;
    [Tooltip("최소 하강 속도 (deg/sec) 이하이면 무시")]
    public float minDownSpeedDps = 80f;

    // 내부 상태
    private float baselineX = 0f;
    private bool baselineSet = false;
    private float prevX = 0f;
    private float prevTime = 0f;

    private bool isTriggered = false;     // releaseAngle 위로 복귀할 때까지 true
    private float lastFireTime = -999f;
    private bool prevGameStarted = false;

    // 방향/엣지 판정을 위한 이전 ΔX 저장
    private float prevDeltaFromBase = 0f;

    void Update()
    {
        // GameStart 전에는 동작 안 함
        if (Kayak == null || !GameStarter.GameStarted)
            return;

        // GameStart 되는 순간 기준 자동 세팅
        if (GameStarter.GameStarted && !prevGameStarted)
            SetBaselineNow();
        prevGameStarted = GameStarter.GameStarted;

        float rawX = transform.localEulerAngles.x;
        if (invertX) rawX = -rawX;
        rawX += xOffset;

        if (!baselineSet)
        {
            baselineX = rawX;
            baselineSet = true;
            prevX = rawX;
            prevTime = Time.time;
            prevDeltaFromBase = 0f;
            return;
        }

        float dt = Mathf.Max(Time.time - prevTime, 0.0001f);
        float delta = rawX - prevX;
        float velocity = delta / dt;               // +: 상승, -: 하강
        float deltaFromBase = rawX - baselineX;    // 기준 대비 현재 각도

        // 진행 방향(하강만 허용)
        bool movingDown = (deltaFromBase < prevDeltaFromBase);

        // 엣지 트리거: 위→아래로 문턱을 최초 통과하는 순간만
        bool crossedFullDown = (prevDeltaFromBase > fullTriggerAngle) && (deltaFromBase <= fullTriggerAngle) && movingDown;
        bool crossedSmallDown = (prevDeltaFromBase > smallTriggerAngle) && (deltaFromBase <= smallTriggerAngle) && movingDown;

        // 쿨다운/속도 체크
        bool cooldownOK = (Time.time - lastFireTime) >= cooldownSec;
        bool fastEnough = (-velocity) >= minDownSpeedDps;  // 하강 속도만 인정

        // === 트리거 ===
        if (!isTriggered && cooldownOK && fastEnough && velocity < 0f) // 👈 하강 중일 때만
        {
            if (crossedFullDown)
            {
                Fire(full: true);
                isTriggered = true;
                lastFireTime = Time.time;
            }
            else if (crossedSmallDown)
            {
                Fire(full: false);
                isTriggered = true;
                lastFireTime = Time.time;
            }
        }

        // === 해제(위로 충분히 복귀해야 다음 트리거 허용) ===
        else if (isTriggered && deltaFromBase >= releaseAngle)
        {
            isTriggered = false;
        }

        // 수동 기준 재설정 (C)
        if (Input.GetKeyDown(KeyCode.C))
        {
            SetBaselineNow();
            Debug.Log($"[{side}] Manual baseline recalibrated at {baselineX:F1}°");
            // 기준 재설정 시 즉시 반환해서 다음 프레임부터 새 기준 사용
            prevX = rawX;
            prevTime = Time.time;
            prevDeltaFromBase = 0f;
            return;
        }

        // 상태 갱신
        prevDeltaFromBase = deltaFromBase;
        prevX = rawX;
        prevTime = Time.time;
    }

    private void Fire(bool full)
    {
        // 카약 컨트롤러 호출 (기존 교차 매핑 유지)
        if (side == JoyconSide.Left)
        {
            if (full) Kayak.JoyFullRightTap();
            else Kayak.JoySmallRightTap();
        }
        else
        {
            if (full) Kayak.JoyFullLeftTap();
            else Kayak.JoySmallLeftTap();
        }
        // Debug.Log($"[{side}] {(full ? "FULL" : "SMALL")} fired (down-edge)");
    }

    private void SetBaselineNow()
    {
        float x = transform.localEulerAngles.x;
        if (invertX) x = -x;
        x += xOffset;

        baselineX = x;
        baselineSet = true;

        isTriggered = false;
        lastFireTime = -999f;
    }
}
