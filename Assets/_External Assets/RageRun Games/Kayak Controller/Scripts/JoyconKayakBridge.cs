using UnityEngine;
using RageRunGames.KayakController;

public class JoyconKayakBridge : MonoBehaviour
{
    public enum JoyconSide { Left, Right }

    [Header("Setup")]
    public JoyconSide side;
    public KayakController Kayak;

    [Header("Delta thresholds (deg, relative to TOP baseline)")]
    public float smallTriggerAngle = -15f; // 기준에서 -15° (스몰)
    public float fullTriggerAngle  = -30f; // 기준에서 -30° (풀)

    [Tooltip("조이콘 X축 반전이 필요하면 체크")]
    public bool invertX = false;
    public float xOffset = 0f;

    [Header("Debounce / Lock")]
    [Tooltip("트리거 후 최소 휴지시간(글로벌 해제까지)")]
    public float cooldownSec = 0.30f; // 살짝 올림

    [Header("Noise Rejection")]
    [Tooltip("임계각보다 이 값만큼 더 깊게 내려가야 탭 인정")]
    public float minTapMargin = 5f;
    [Tooltip("내려갈 때 속도 게이트 사용 여부")]
    public bool useDownVelocityGate = true;
    [Tooltip("속도 게이트 ON일 때, 최소 하강 속도(dps)")]
    public float minDownSpeedDps = 110f;

    [Header("Re-arm (release 없이 재무장)")]
    [Tooltip("위로 움직이기 시작하면 재무장하는 최소 상승 속도(dps)")]
    public float rearmSpeedDps = 25f;
    [Tooltip("재무장 판정에 필요한 최소 지속시간(초)")]
    public float rearmMinHoldSec = 0.08f;
    [Tooltip("바닥(최저점)에서 이만큼 위로 올라오면 재무장 허용")]
    public float rearmDeltaUpDeg = 6f;

    [Header("Rising Confirmation / Smoothing")]
    [Tooltip("상승 전환으로 인정할 최소 상승 속도(dps)")]
    public float ascentSpeedMinDps = 15f;
    [Tooltip("최저점 대비 이만큼은 올라와야 발동(히스테리시스)")]
    public float liftHysteresisDeg = 3f;
    [Tooltip("각도 스무딩(0=없음, 1=즉시 반응)")]
    [Range(0.0f, 1.0f)] public float smoothAlpha = 0.2f;

    [Header("Reservation (동시 발동 봉인)")]
    [Tooltip("임계각 통과한 첫쪽을 잠깐 예약하여 다른쪽 봉인")]
    public float reservationWindowSec = 0.06f; // 60ms

    [Header("Hold (optional)")]
    public bool useHold = false;

    // ==== 내부 상태 ====
    private float baselineX = 0f;                // '윗점' 기준
    private bool  baselineSet = false;
    private bool  prevGameStarted = false;

    // ★ 전역(양쪽 공유) 쿨다운/락
    private static bool  s_isGlobalTriggerActive = false;
    private static float s_lastGlobalFireTime = -999f;
    private static int   s_lastTriggerFrame = -1;

    // 예약(임계 먼저 넘은 쪽 선점)
    private static bool        s_hasReservation = false;
    private static JoyconSide  s_reservedSide;
    private static float       s_reservationUntil = -1f;

    // 인스턴스 로컬 상태
    private float minXSinceBaseline = 999f;      // 기준 이후 최저 deltaX
    private float prevDeltaSmoothed = 0f;
    private float prevT = 0f;
    private float peakDownSpeed = 0f;            // 최댓 하강 속도(dps)
    private float risingAccum = 0f;              // 상승 상태 누적시간
    private bool  armed = true;                  // 현재 사이클 무장
    private bool  smallReady = false;            // 임계 통과 플래그
    private bool  fullReady  = false;

    private float smoothedDeltaX = 0f;           // 스무딩된 deltaX

    void Update()
    {
        // 게임 시작 에지에서 기준 잡기
        if (GameStarter.GameStarted && !prevGameStarted)
            SetBaselineNow();
        prevGameStarted = GameStarter.GameStarted;

        if (!GameStarter.GameStarted || Kayak == null) return;

        // 조이콘 로컬 X각 → baseline 대비 deltaX(아래로 음수)
        float x = Normalize180(transform.localEulerAngles.x);
        if (invertX) x = -x;
        x += xOffset;
        if (!baselineSet) SetBaselineNow();

        float now = Time.time;
        float dt  = (prevT == 0f) ? Time.deltaTime : (now - prevT);

        float rawDeltaX = Normalize180(x - baselineX);

        // === 스무딩 ===
        smoothedDeltaX = Mathf.Lerp(smoothedDeltaX, rawDeltaX, Mathf.Clamp01(smoothAlpha));

        // 속도 dps( +: 상승, -: 하강 ) — 스무딩된 값 기준
        float vel = (smoothedDeltaX - prevDeltaSmoothed) / Mathf.Max(0.0001f, dt);
        float downSpeed = -vel; // 하강 시 양수
        if (downSpeed > peakDownSpeed) peakDownSpeed = downSpeed;

        // === 글로벌 쿨다운 자동 해제 (전역 시간 기반) ===
        if (s_isGlobalTriggerActive && (now - s_lastGlobalFireTime) >= cooldownSec)
            s_isGlobalTriggerActive = false;

        // === 예약 만료 ===
        if (s_hasReservation && now >= s_reservationUntil)
            s_hasReservation = false;

        // === 재무장 로직 (절대 해제 각도 없음) ===
        if (!armed)
        {
            bool enoughRiseFromTrough = (smoothedDeltaX - minXSinceBaseline) >= rearmDeltaUpDeg;
            if (vel > rearmSpeedDps) risingAccum += dt; else risingAccum = 0f;

            if ((enoughRiseFromTrough || risingAccum >= rearmMinHoldSec) && !s_isGlobalTriggerActive)
            {
                // 새 '윗점' 기준으로 재무장
                baselineX = Normalize180(x);
                minXSinceBaseline = 999f;
                peakDownSpeed = 0f;
                armed = true;
                smallReady = false;
                fullReady  = false;

                if (useHold) ClearHolds();
            }
        }

        // 베이스라인 이후 최저점 갱신(스무딩 기준)
        if (smoothedDeltaX < minXSinceBaseline)
            minXSinceBaseline = smoothedDeltaX;

        // (선택) HOLD 상태 처리
        if (useHold && armed)
        {
            if (smoothedDeltaX <= fullTriggerAngle)
            {
                SetHold(full:true, on:true);
                SetHold(full:false, on:false);
            }
            else if (smoothedDeltaX <= smallTriggerAngle)
            {
                SetHold(full:false, on:true);
                SetHold(full:true,  on:false);
            }
            else
            {
                ClearHolds();
            }
        }

        // ===== TAP 판정 =====
        bool cooldownPassed = (now - s_lastGlobalFireTime) >= cooldownSec;
        if (armed && cooldownPassed && !s_isGlobalTriggerActive)
        {
            float fullGate  = fullTriggerAngle  - minTapMargin;
            float smallGate = smallTriggerAngle - minTapMargin;

            // 임계각 충분히 넘었으면 준비 플래그(하강 구간에서만 의미)
            if (minXSinceBaseline <= fullGate)
            {
                fullReady  = true; smallReady = false; // 풀이 우선 → 스몰 플래그 해제
                // 예약: 누구든 처음 넘는 쪽만 60ms 선점
                if (!s_hasReservation) { s_hasReservation = true; s_reservedSide = side; s_reservationUntil = now + reservationWindowSec; }
            }
            else if (minXSinceBaseline <= smallGate && !fullReady)
            {
                smallReady = true;
                if (!s_hasReservation) { s_hasReservation = true; s_reservedSide = side; s_reservationUntil = now + reservationWindowSec; }
            }

            bool downSpeedOK = !useDownVelocityGate || (peakDownSpeed >= minDownSpeedDps);
            bool risingConfirmed = (vel >= ascentSpeedMinDps) && ((smoothedDeltaX - minXSinceBaseline) >= liftHysteresisDeg);

            // 상승 전환 + 히스테리시스 + 속도 OK + 예약 일치 시에만 발동
            if (risingConfirmed && downSpeedOK && (!s_hasReservation || s_reservedSide == side))
            {
                if (fullReady)      FireFullTap();
                else if (smallReady) FireSmallTap();
            }
        }

        // 수동 캘리브레이션
        if (Input.GetKeyDown(KeyCode.C))
            SetBaselineNow();

        prevDeltaSmoothed = smoothedDeltaX;
        prevT = now;
    }

    // ==== 탭 발동 ====
    private void FireSmallTap()
    {
        // 같은 프레임 중복 발동 봉인
        if (s_lastTriggerFrame == Time.frameCount) return;
        s_lastTriggerFrame   = Time.frameCount;
        s_lastGlobalFireTime = Time.time;
        s_isGlobalTriggerActive = true;
        s_hasReservation = false; // 예약 해제

        // Left 센서 → Left 탭, Right 센서 → Right 탭
        if (side == JoyconSide.Left) Kayak.JoySmallRightTap();   // B 등가
        else                         Kayak.JoySmallLeftTap();  // V 등가

        armed = false;
        ResetStrokeCycle();
    }

    private void FireFullTap()
    {
        if (s_lastTriggerFrame == Time.frameCount) return;
        s_lastTriggerFrame   = Time.frameCount;
        s_lastGlobalFireTime = Time.time;
        s_isGlobalTriggerActive = true;
        s_hasReservation = false;

        if (side == JoyconSide.Left) Kayak.JoyFullRightTap();   // M 등가
        else                         Kayak.JoyFullLeftTap();    // N 등가

        armed = false;
        ResetStrokeCycle();
    }

    private void ResetStrokeCycle()
    {
        // 다음 사이클 준비 (기준은 TOP 유지)
        peakDownSpeed = 0f;
        risingAccum = 0f;
        smallReady = false;
        fullReady  = false;
        // 최저점은 다시 내려갈 때 갱신됨
        minXSinceBaseline = smoothedDeltaX + 0.001f;

        if (useHold) ClearHolds();
    }

    // ==== Hold helpers ====
    private void SetHold(bool full, bool on)
    {
        if (!useHold) return;
        if (side == JoyconSide.Left)
        {
            if (full) Kayak.JoyHoldLeft(on, small:false);
            else      Kayak.JoyHoldLeft(on, small:true);
        }
        else
        {
            if (full) Kayak.JoyHoldRight(on, small:false);
            else      Kayak.JoyHoldRight(on, small:true);
        }
    }

    private void ClearHolds()
    {
        if (side == JoyconSide.Left)
        {
            Kayak.JoyHoldLeft(false, small:true);
            Kayak.JoyHoldLeft(false, small:false);
        }
        else
        {
            Kayak.JoyHoldRight(false, small:true);
            Kayak.JoyHoldRight(false, small:false);
        }
    }

    // ==== Baseline / Utils ====
    private void SetBaselineNow()
    {
        float x = Normalize180(transform.localEulerAngles.x);
        if (invertX) x = -x;
        x += xOffset;

        baselineX = x;          // '윗점' 기준
        baselineSet = true;

        // 스무딩 상태 리셋
        smoothedDeltaX = 0f;
        prevDeltaSmoothed = 0f;

        minXSinceBaseline = 999f;
        peakDownSpeed = 0f;
        risingAccum = 0f;
        armed = true;
        smallReady = false;
        fullReady  = false;
         Debug.Log($"[JoyconKayakBridge-{side}] TOP baseline set: {baselineX:F1}");
    }

    private float Normalize180(float angle)
    {
        float a = angle % 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }
}
