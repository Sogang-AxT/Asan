using UnityEngine;
using TMPro;

[RequireComponent(typeof(BoatPhysics))]
[RequireComponent(typeof(JoyconInputReader))]
public class PlayerBoatController : MonoBehaviour
{
    [Header("Modules")]
    private BoatPhysics _physics;
    private JoyconInputReader _input;

    [Header("Propulsion Logic")]
    public float propulsionDeadBandDeg = 3f;
    public float propulsionGain = 10f;
    public float propulsionSmoothing = 0.15f;
    public float fullAngleDeg = 20f;

    // Yaw 제어
    public float yawTorqueFromDelta = 0.25f;
    public bool scaleYawByPropulsion = true;

    [Header("Counting / Thresholds")]
    public float minCountAngle = 10f;
    public float resetCountAngle = 5f;

    [Header("UI & Score")]
    public TMP_Text distanceText;
    public TMP_Text paddleCountText;
    [SerializeField] private ScoreManager scoreManager;

    // --- 내부 상태 (원본과 동일) ---
    private float _propulsion;
    private (bool, bool) _gateLockTuple;
    public int _distanceMeters;
    public int _paddleCount;
    private (int, int) _movementCountTuple;
    private (float, float) _angleSumAbsTuple;

    // --- 외부 공개 프로퍼티 (CharacterAnimationController 연동용) ---
    // 애니메이션은 이 값을 읽어가면 됩니다.
    public float Propulsion => _propulsion;
    public bool LeftDominant => _input.IsLeftDominant; // 실시간 방향
    public int LegStrokeCountLeft => _movementCountTuple.Item1;
    public int LegStrokeCountRight => _movementCountTuple.Item2;

    private void Awake()
    {
        _physics = GetComponent<BoatPhysics>();
        _input = GetComponent<JoyconInputReader>();

        // 초기화
        _gateLockTuple = (false, false);
    }

    private void OnEnable()
    {
        _input.OnStrokePeakDetected += HandleStrokePeak;
    }

    private void OnDisable()
    {
        _input.OnStrokePeakDetected -= HandleStrokePeak;
    }

    private void Update()
    {
        if (!GameStarter.GameStarted) return;

        // 1. 추진력 계산 (원본 로직: CalculatePropulsion)
        CalculatePropulsionLogic();

        // 2. 게이트 리셋 (원본 로직: Update 마지막 부분)
        CheckGateReset();

        // 3. UI 업데이트
        UpdateUI();
    }

    private void FixedUpdate()
    {
        // 4. 물리 엔진에 힘 전달 (원본 로직: ApplyPropulsionAndYaw)
        ApplyMovementToPhysics();
    }

    private void CalculatePropulsionLogic()
    {
        float drive = 0f;

        // [핵심] 원본은 _peakDomSide(마지막 피크)를 기준으로 계산함.
        // 따라서 _input.IsLastPeakLeft를 사용해야 원본과 느낌이 동일함.
        float targetDelta = _input.IsLastPeakLeft ? _input.DeltaLeft : _input.DeltaRight;
        float absDom = Mathf.Abs(targetDelta);

        if (absDom > propulsionDeadBandDeg)
        {
            drive = Mathf.InverseLerp(
                propulsionDeadBandDeg,
                Mathf.Max(propulsionDeadBandDeg + 1f, fullAngleDeg),
                absDom);
        }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float t = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, propulsionSmoothing));

        _propulsion = Mathf.Lerp(_propulsion, drive, t);
    }

    private void CheckGateReset()
    {
        if (_gateLockTuple.Item1 && Mathf.Abs(_input.DeltaLeft) <= resetCountAngle)
            _gateLockTuple.Item1 = false;
        if (_gateLockTuple.Item2 && Mathf.Abs(_input.DeltaRight) <= resetCountAngle)
            _gateLockTuple.Item2 = false;
    }

    private void ApplyMovementToPhysics()
    {
        if (_propulsion <= 1e-4f) return;

        // Yaw 토크 계산 (원본 로직)
        float deltaDiff = Mathf.Clamp(_input.DeltaRight - _input.DeltaLeft, -45f, 45f);
        float yaw = yawTorqueFromDelta * (deltaDiff / fullAngleDeg);

        if (scaleYawByPropulsion) yaw *= _propulsion;

        // 물리 스크립트에 명령 하달
        _physics.ApplyPhysicsForce(_propulsion, propulsionGain, yaw);
    }

    // 스트로크 피크 이벤트 수신
    private void HandleStrokePeak(bool isLeft, float angleAbs)
    {
        if (angleAbs < minCountAngle) return;

        if (isLeft)
        {
            if (_gateLockTuple.Item1) return;
            _gateLockTuple.Item1 = true;
        }
        else
        {
            if (_gateLockTuple.Item2) return;
            _gateLockTuple.Item2 = true;
        }

        // 통계 및 점수 처리
        int addDist = Mathf.RoundToInt(angleAbs / 10f);
        if (addDist < 1) addDist = 1;

        _distanceMeters += addDist;
        _paddleCount++;

        if (isLeft)
        {
            _angleSumAbsTuple.Item1 += angleAbs;
            _movementCountTuple.Item1++;
        }
        else
        {
            _angleSumAbsTuple.Item2 += angleAbs;
            _movementCountTuple.Item2++;
        }

        if (scoreManager != null) scoreManager.RecordStroke(isLeft, angleAbs);
    }

    private void UpdateUI()
    {
        if (distanceText) distanceText.text = _distanceMeters + "m";
        if (paddleCountText) paddleCountText.text = "x " + _paddleCount;
    }
}