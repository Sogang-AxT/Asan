using UnityEngine;
using System;

public class JoyconInputReader : MonoBehaviour
{
    [Header("Input Sources")]
    public Transform joyconCubeLeft;
    public Transform joyconCubeRight;
    public bool isInvertedLeft;   // false
    public bool isInvertedRight;  // false

    [Header("Joy-Con Calibrate")]
    public bool useAngleAutoCalibrator = true;
    public KeyCode manualCalibrateKeyCode = KeyCode.C;
    public float deadZoneDegree = 2.0f;

    [Header("Smoothing / Phase")]
    public float fullAngleDeg = 20f;
    public float phaseSmoothUp = 0.06f;
    public float phaseSmoothDown = 0.18f;
    public float deadzone = 0.02f;

    // --- 내부 상태 변수 ---
    private (float, float) _localJoyconXTuple;
    private (float, float) _deltaAngleTuple;
    private bool _isAngleCalibrated;

    // 피크 감지용
    private sbyte _domTrend;
    private float _domXPrev;

    // 상태값 (원본 로직 보존을 위해 구분)
    private bool _leftDominant;    // [실시간] 현재 프레임에서 더 많이 기울어진 쪽 (애니메이션용)
    private bool _lastPeakWasLeft; // [고정] 마지막으로 스트로크 피크가 터진 쪽 (추진력 계산용)

    // 위상값 (애니메이션 연동 가능성 대비 원본 유지)
    private float _phase;
    private float _phaseVel;

    // --- 외부 공개 프로퍼티 ---
    public float DeltaLeft => _deltaAngleTuple.Item1;
    public float DeltaRight => _deltaAngleTuple.Item2;
    public bool IsLeftDominant => _leftDominant;     // 애니메이션은 이걸 씁니다.
    public bool IsLastPeakLeft => _lastPeakWasLeft;  // 추진력 계산은 이걸 씁니다.
    public float Phase => _phase;

    // 이벤트: (왼쪽여부, 절대각도)
    public event Action<bool, float> OnStrokePeakDetected;

    private void Start()
    {
        if (useAngleAutoCalibrator) JoyconCalibrator();
    }

    private void Update()
    {
        // 수동 보정
        if (Input.GetKeyDown(manualCalibrateKeyCode)) JoyconCalibrator();

        // 입력 처리 및 피크 로직
        JoyconGyroInput();
        PeakTrendCheck();
        CalculatePhase();
    }

    public void JoyconCalibrator()
    {
        _localJoyconXTuple.Item1 = ReadLocalX(joyconCubeLeft, isInvertedLeft);
        _localJoyconXTuple.Item2 = ReadLocalX(joyconCubeRight, isInvertedRight);

        // 초기화
        _domXPrev = 0f;
        _domTrend = 0;
        _phase = 0f;
        _isAngleCalibrated = true;

        Debug.Log("Calibration Completed");
    }

    private void JoyconGyroInput()
    {
        // 원본의 ReadLocalX 로직 그대로 사용
        float rawL = ReadLocalX(joyconCubeLeft, isInvertedLeft);
        float rawR = ReadLocalX(joyconCubeRight, isInvertedRight);

        float deltaL = rawL - _localJoyconXTuple.Item1;
        float deltaR = rawR - _localJoyconXTuple.Item2;

        _deltaAngleTuple.Item1 = Mathf.Abs(deltaL) < deadZoneDegree ? 0f : deltaL;
        _deltaAngleTuple.Item2 = Mathf.Abs(deltaR) < deadZoneDegree ? 0f : deltaR;
    }

    private void PeakTrendCheck()
    {
        // 1. 실시간 우세 방향 판별
        _leftDominant = Mathf.Abs(_deltaAngleTuple.Item1) >= Mathf.Abs(_deltaAngleTuple.Item2);

        float domX = _leftDominant ? _deltaAngleTuple.Item1 : _deltaAngleTuple.Item2;
        float magPrev = Mathf.Abs(_domXPrev);
        float magCurr = Mathf.Abs(domX);

        sbyte domTrendNow = _domTrend;
        if (magCurr > magPrev + 0.5f) domTrendNow = 1;
        else if (magCurr < magPrev - 0.5f) domTrendNow = -1;

        // 2. 피크 발생 (Trend가 +1에서 -1로 꺾임)
        if (_domTrend == 1 && domTrendNow == -1)
        {
            float peakAngle = _domXPrev;

            // [중요] 피크 시점의 방향을 저장 (Sticky) -> 원본의 _peakDomSide 역할
            _lastPeakWasLeft = _leftDominant;

            // 컨트롤러에 알림
            OnStrokePeakDetected?.Invoke(_lastPeakWasLeft, Mathf.Abs(peakAngle));
        }

        _domTrend = domTrendNow;
        _domXPrev = domX;
    }

    private void CalculatePhase()
    {
        float target = Mathf.Clamp01(Mathf.Abs(_domXPrev) / Mathf.Max(1f, fullAngleDeg));
        float smoothTime = (_domTrend == -1) ? phaseSmoothDown : phaseSmoothUp;

        _phase = Mathf.SmoothDamp(_phase, target, ref _phaseVel, Mathf.Max(1e-3f, smoothTime));
        if (_phase < deadzone) _phase = 0f;
    }

    private float ReadLocalX(Transform t, bool invert)
    {
        if (!t) return 0f;
        float x = t.localEulerAngles.x;
        if (x > 180f) x -= 360f;
        return invert ? -x : x;
    }
}