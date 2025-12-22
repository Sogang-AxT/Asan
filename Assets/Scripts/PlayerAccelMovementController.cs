using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerAccelMovementController : MonoBehaviour
{
    [Header("Input Settings")]
    public JoyconDemo joyconScriptLeft;
    public JoyconDemo joyconScriptRight;
    public bool isInvertedLeft;
    public bool isInvertedRight;

    // =========================================================
    // [1] 필터 및 센서 설정
    // =========================================================
    [Header("Sensor Stabilization")]
    public float cutoffFrequency = 5.0f;
    public float filterSampleRate = 60f;

    // 적응형 중력
    public float gravityAdaptSpeedIdle = 2.0f;
    public float gravityAdaptSpeedActive = 0.05f;
    public float motionThreshold = 0.15f;

    private AccelButterworthFilter _bwLX, _bwLY, _bwLZ;
    private AccelButterworthFilter _bwRX, _bwRY, _bwRZ;
    private Vector3 _adaptiveGravityL, _adaptiveGravityR;

    // =========================================================
    // [2] 물리 이동 설정 (데드존 분리됨)
    // =========================================================
    [Header("Physics Movement")]
    [Tooltip("최대 속도로 인정할 입력값 (성인:1.5 / 재활:0.6)")]
    public float maxInputLimit = 1.5f;

    [Tooltip("전진 힘 (추천: 60~80)")]
    public float propulsionGain = 70f;

    [Tooltip("관성: 클수록 반응이 느리고 묵직함 (0.15~0.2)")]
    public float propulsionSmoothing = 0.2f;

    [Tooltip("손 놨을 때 멈추는 속도 (3.0)")]
    public float stopDecaySpeed = 3.0f;

    // [수정] 데드존 분리
    [Header("Deadzone Settings (Separate)")]
    [Tooltip("왼쪽 조이콘 데드존 (노이즈 심하면 높이세요)")]
    public float deadZoneLeft = 0.1f;

    [Tooltip("오른쪽 조이콘 데드존 (노이즈 심하면 높이세요)")]
    public float deadZoneRight = 0.1f;

    // =========================================================
    // [3] 카운팅 설정
    // =========================================================
    [Header("Counting Logic")]
    public float minCountInput = 0.6f;
    public float resetCountInput = 0.2f;
    public float strokeCooldown = 0.6f;

    private float _currentCooldownL = 0f;
    private float _currentCooldownR = 0f;

    // 내부 변수
    private float _inputL, _inputR;
    private float _propulsion;
    public float Propulsion => _propulsion;

    private bool _gateLockL, _gateLockR;
    private bool _leftDominant;
    public bool LeftDominant => _leftDominant;

    public bool autoCalibrateOnStart = true;
    public KeyCode manualCalibrateKeyCode = KeyCode.C;

    // 물리/UI 컴포넌트
    [Header("Physics Helpers & UI")]
    public bool enablePhysicsAssist = true;
    public float constantWaterLevel = 0f;
    public Transform[] buoyancyPoints;
    public float buoyancyStrength = 9.81f;
    public float buoyancyScale = 1.0f;
    public float baseDrag = 1.5f;
    public float dragSpeedFactor = 0.05f;
    public float baseAngularDrag = 3.0f;
    public float angularDragFactor = 0.025f;
    public float lateralDampingMultiply = 0.8f;
    public float maxVelocity = 8f;
    public float maxAngularVelocity = 5f;
    public float uprightStartAngleDeg = 5f;
    public float uprightStability = 12f;
    public float uprightAngularDamping = 0.1f;
    public bool applyCenterOfMass = true;
    private Vector3 _centerOfMassOffset = new Vector3(0f, -0.1f, 0f);

    [Header("Steering")]
    public float yawTorqueFromDelta = 0.3f;
    public bool scaleYawByPropulsion = true;

    public Transform propelTargetTransform;
    public bool useWorldSpaceForward = false;
    public int distanceMeters = 0;
    public int paddleCount = 0;
    public TMP_Text distanceText;
    public TMP_Text paddleCountText;
    [SerializeField] private ScoreManager scoreManager;
    private Rigidbody _rigidbody;

    private float _debugRawL, _debugRawR;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (!propelTargetTransform) propelTargetTransform = transform;
        if (applyCenterOfMass && _rigidbody) _rigidbody.centerOfMass += _centerOfMassOffset;
        InitializeFilters();
    }

    private void Start()
    {
        if (autoCalibrateOnStart) JoyconCalibrator();
    }

    private void Update()
    {
        if (!GameStarter.GameStarted) return;
        if (Input.GetKeyDown(manualCalibrateKeyCode)) JoyconCalibrator();

        // 1. 센서 읽기 (각각의 데드존 적용)
        ReadSensorInput();

        // 2. 떨림 방지
        ApplyDominanceSuppression();
        CheckDominantHand();

        // 3. 추진력 계산
        CalculatePropulsionContinuous();

        // 4. 카운팅 로직
        UpdateStrokeCountingLogic();
    }

    private void FixedUpdate()
    {
        if (enablePhysicsAssist && _rigidbody)
        {
            ApplyBuoyancyAssist(_rigidbody);
            ApplyWaterDragAssist(_rigidbody);
            ClampVelocities(_rigidbody);
            UprightStabilization(_rigidbody);
        }
        ApplyPhysicsForce();
    }

    private void InitializeFilters()
    {
        _bwLX = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwLY = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwLZ = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRX = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRY = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRZ = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
    }

    // =========================================================
    // [수정] 개별 데드존 적용 로직
    // =========================================================
    private void ReadSensorInput()
    {
        if (!joyconScriptLeft || !joyconScriptRight) return;
        float dt = Time.deltaTime;

        // --- Left ---
        Vector3 rawL = joyconScriptLeft.accel;
        Vector3 filtL = new Vector3(_bwLX.Update(rawL.x), _bwLY.Update(rawL.y), _bwLZ.Update(rawL.z));
        Vector3 worldL = joyconScriptLeft.orientation * filtL;
        if (isInvertedLeft) worldL = -worldL;

        float diffL = (worldL - _adaptiveGravityL).magnitude;
        float adaptL = (diffL > motionThreshold) ? gravityAdaptSpeedActive : gravityAdaptSpeedIdle;
        _adaptiveGravityL = Vector3.Lerp(_adaptiveGravityL, worldL, dt * adaptL);
        float valL = (worldL - _adaptiveGravityL).y;

        // --- Right ---
        Vector3 rawR = joyconScriptRight.accel;
        Vector3 filtR = new Vector3(_bwRX.Update(rawR.x), _bwRY.Update(rawR.y), _bwRZ.Update(rawR.z));
        Vector3 worldR = joyconScriptRight.orientation * filtR;
        if (isInvertedRight) worldR = -worldR;

        float diffR = (worldR - _adaptiveGravityR).magnitude;
        float adaptR = (diffR > motionThreshold) ? gravityAdaptSpeedActive : gravityAdaptSpeedIdle;
        _adaptiveGravityR = Vector3.Lerp(_adaptiveGravityR, worldR, dt * adaptR);
        float valR = (worldR - _adaptiveGravityR).y;

        // [NEW] 데드존 처리 전의 값을 디버깅용으로 저장
        _debugRawL = Mathf.Abs(valL);
        _debugRawR = Mathf.Abs(valR);

        // 데드존 적용
        if (_debugRawL < deadZoneLeft) valL = 0f;
        if (_debugRawR < deadZoneRight) valR = 0f;

        // 최종 입력값 저장
        _inputL = Mathf.Abs(valL);
        _inputR = Mathf.Abs(valR);
    }

    private void ApplyDominanceSuppression()
    {
        if (_inputL > _inputR + 0.2f) _inputR = 0f;
        else if (_inputR > _inputL + 0.2f) _inputL = 0f;
    }

    private void CheckDominantHand()
    {
        if (_inputL >= _inputR) _leftDominant = true;
        else _leftDominant = false;
    }

    // =========================================================
    // [수정] 개별 데드존을 고려한 추진력 계산
    // =========================================================
    private void CalculatePropulsionContinuous()
    {
        float targetDrive = 0f;
        float maxInput = Mathf.Max(_inputL, _inputR);

        // 현재 더 큰 입력값을 가진 쪽의 데드존을 기준점으로 삼음
        float currentDeadZone = (_inputL > _inputR) ? deadZoneLeft : deadZoneRight;

        // ReadSensorInput에서 이미 0으로 컷팅되었으므로, 0보다 크면 유효 입력임
        if (maxInput > 0)
        {
            // InverseLerp의 시작점을 해당 손의 데드존으로 설정
            targetDrive = Mathf.InverseLerp(currentDeadZone, maxInputLimit, maxInput);
        }

        float currentSmoothing = (targetDrive > 0) ? propulsionSmoothing : (propulsionSmoothing / stopDecaySpeed);
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float t = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, currentSmoothing));

        _propulsion = Mathf.Lerp(_propulsion, targetDrive, t);

        if (_propulsion < 0.01f) _propulsion = 0f;
    }

    private void UpdateStrokeCountingLogic()
    {
        if (_currentCooldownL > 0) _currentCooldownL -= Time.deltaTime;
        if (_currentCooldownR > 0) _currentCooldownR -= Time.deltaTime;
        // 쿨타임(_currentCooldown)이 0일 때만 카운팅 시도
        CheckGateAndCount(ref _gateLockL, _inputL, true, ref _currentCooldownL);
        CheckGateAndCount(ref _gateLockR, _inputR, false, ref _currentCooldownR);
    }

    private void CheckGateAndCount(ref bool gate, float input, bool isLeft, ref float cooldownTimer)
    {
        // 쿨타임 중이면 카운트 안 함
        if (cooldownTimer > 0)
        {
            gate = false;
            return;
        }

        if (!gate && input > minCountInput)
        {
            gate = true;
            ProcessStroke(isLeft, input);

            // [핵심] 카운트 됐으면 쿨타임 시작!
            cooldownTimer = strokeCooldown;
        }

        if (gate && input <= resetCountInput)
        {
            gate = false;
        }
    }

    private void ProcessStroke(bool leftSide, float val)
    {
        int addDist = Mathf.RoundToInt(val * 10f);
        if (addDist < 1) addDist = 1;
        distanceMeters += addDist;
        paddleCount++;

        if (distanceText) distanceText.text = distanceMeters + "m";
        if (paddleCountText) paddleCountText.text = "x " + paddleCount;
        if (scoreManager) scoreManager.RecordStroke(leftSide, val);
    }

    private void ApplyPhysicsForce()
    {
        if (!propelTargetTransform || _propulsion <= 0.001f) return;

        var forwardDir = useWorldSpaceForward ? Vector3.forward : propelTargetTransform.forward;
        var horizonForward = Vector3.ProjectOnPlane(forwardDir, Vector3.up).normalized;
        if (horizonForward.sqrMagnitude < 1e-6f) horizonForward = Vector3.forward;

        float force = propulsionGain * _propulsion;

        if (propelTargetTransform.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
        {
            rb.AddForce(horizonForward * force, ForceMode.Force);

            float steerDelta = _inputL - _inputR;
            steerDelta = Mathf.Clamp(steerDelta, -maxInputLimit, maxInputLimit);

            float yaw = yawTorqueFromDelta * (steerDelta / maxInputLimit);

            if (scaleYawByPropulsion) yaw *= _propulsion;
            if (Mathf.Abs(yaw) > 1e-5f) rb.AddTorque(Vector3.up * yaw, ForceMode.Force);
        }
    }

    public void JoyconCalibrator()
    {
        InitializeFilters();
        if (joyconScriptLeft) _adaptiveGravityL = joyconScriptLeft.orientation * joyconScriptLeft.accel;
        if (joyconScriptRight) _adaptiveGravityR = joyconScriptRight.orientation * joyconScriptRight.accel;
        _inputL = 0; _inputR = 0; _propulsion = 0;
        _gateLockL = false; _gateLockR = false;
        Debug.Log("Calibrated (Separate Deadzones)");
    }

    // --- Physics Helpers (동일) ---
    void ApplyBuoyancyAssist(Rigidbody rb) { if (buoyancyPoints != null) foreach (var p in buoyancyPoints) { if (p) { float d = constantWaterLevel - p.position.y; if (d > 0) rb.AddForceAtPosition(Vector3.up * d * buoyancyStrength * buoyancyScale, p.position, ForceMode.Acceleration); } } else { float d = constantWaterLevel - rb.worldCenterOfMass.y; if (d > 0) rb.AddForce(Vector3.up * d * buoyancyStrength * buoyancyScale, ForceMode.Acceleration); } }
    void ApplyWaterDragAssist(Rigidbody rb) { var lv = rb.transform.InverseTransformDirection(rb.velocity); rb.drag = baseDrag + (rb.velocity.magnitude * dragSpeedFactor); rb.angularDrag = baseAngularDrag + (rb.angularVelocity.magnitude * angularDragFactor); lv.x *= lateralDampingMultiply; rb.velocity = rb.transform.TransformDirection(lv); }
    void ClampVelocities(Rigidbody rb) { if (rb.velocity.magnitude > maxVelocity) rb.velocity = rb.velocity.normalized * maxVelocity; if (rb.angularVelocity.magnitude > maxAngularVelocity) rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity; }
    void UprightStabilization(Rigidbody rb) { var worldForwardOnPlane = Vector3.ProjectOnPlane(Vector3.forward, Vector3.up); var localForwardOnPlane = Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up); var desiredForwardOnPlane = useWorldSpaceForward ? worldForwardOnPlane : localForwardOnPlane; if (desiredForwardOnPlane.sqrMagnitude < 1e-6f) desiredForwardOnPlane = Vector3.forward; var targetRot = Quaternion.LookRotation(desiredForwardOnPlane.normalized, Vector3.up); if (Vector3.Angle(rb.transform.up, Vector3.up) < uprightStartAngleDeg) return; var delta = targetRot * Quaternion.Inverse(rb.rotation); delta.ToAngleAxis(out var angleDeg, out var axis); if (angleDeg > 180f) angleDeg -= 360f; var torque = axis.normalized * (angleDeg * Mathf.Deg2Rad * uprightStability) - rb.angularVelocity * uprightAngularDamping; rb.AddTorque(torque, ForceMode.Acceleration); }

    // =========================================================
    // [GUI] 실시간 데드존 튜너
    // =========================================================
    private void OnGUI()
    {
        // 1. 스타일 설정
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;

        // 배경 박스 (잘 보이게)
        GUI.Box(new Rect(10, 10, 400, 350), "Deadzone Tuner");

        // ----------------- LEFT Controller -----------------
        GUILayout.BeginArea(new Rect(20, 40, 380, 150));

        // 왼쪽 상태 표시 (노이즈가 데드존을 넘으면 초록색, 안 넘으면 노란색)
        float currentL = _debugRawL;
        bool isActiveL = currentL > deadZoneLeft;
        labelStyle.normal.textColor = isActiveL ? Color.green : Color.yellow;

        GUILayout.Label($"LEFT Raw Input: {currentL:F4}", labelStyle);
        GUILayout.Label(isActiveL ? "Status: ACTIVE (Input ON)" : "Status: NOISE (Blocked)", labelStyle);

        // 왼쪽 슬라이더
        labelStyle.normal.textColor = Color.white;
        GUILayout.Space(10);
        GUILayout.Label($"Deadzone L: {deadZoneLeft:F3}", labelStyle);
        deadZoneLeft = GUILayout.HorizontalSlider(deadZoneLeft, 0.0f, 0.5f); // 0.0 ~ 0.5 사이 조절

        GUILayout.EndArea();

        // ----------------- RIGHT Controller -----------------
        GUILayout.BeginArea(new Rect(20, 180, 380, 150));

        // 오른쪽 상태 표시
        float currentR = _debugRawR;
        bool isActiveR = currentR > deadZoneRight;
        labelStyle.normal.textColor = isActiveR ? Color.green : Color.yellow;

        GUILayout.Label($"RIGHT Raw Input: {currentR:F4}", labelStyle);
        GUILayout.Label(isActiveR ? "Status: ACTIVE (Input ON)" : "Status: NOISE (Blocked)", labelStyle);

        // 오른쪽 슬라이더
        labelStyle.normal.textColor = Color.white;
        GUILayout.Space(10);
        GUILayout.Label($"Deadzone R: {deadZoneRight:F3}", labelStyle);
        deadZoneRight = GUILayout.HorizontalSlider(deadZoneRight, 0.0f, 0.5f);

        GUILayout.EndArea();
    }
}

// 필터 클래스 (동일)
public class AccelButterworthFilter
{
    private class Biquad
    {
        private float a0, a1, a2, b1, b2;
        private float[] x = new float[3];
        private float[] y = new float[3];
        public Biquad(float cutoffFreq, float sampleRate)
        {
            float c = Mathf.Tan(Mathf.PI * cutoffFreq / sampleRate);
            float a = 1.0f + Mathf.Sqrt(2.0f) * c + c * c;
            a0 = c * c / a; a1 = 2 * a0; a2 = a0;
            b1 = 2.0f * (c * c - 1.0f) / a; b2 = (1.0f - Mathf.Sqrt(2.0f) * c + c * c) / a;
        }
        public float Update(float input)
        {
            x[0] = input;
            y[0] = a0 * x[0] + a1 * x[1] + a2 * x[2] - b1 * y[1] - b2 * y[2];
            x[2] = x[1]; x[1] = x[0]; y[2] = y[1]; y[1] = y[0];
            if (float.IsNaN(y[0]) || float.IsInfinity(y[0])) y[0] = 0;
            return y[0];
        }
    }
    private List<Biquad> sections = new List<Biquad>();
    public AccelButterworthFilter(float cutoffFreq, float sampleRate, int order = 2)
    {
        order = Mathf.Max(2, order);
        int numSections = Mathf.CeilToInt(order / 2.0f);
        for (int i = 0; i < numSections; i++) sections.Add(new Biquad(cutoffFreq, sampleRate));
    }
    public float Update(float input)
    {
        float output = input;
        foreach (var s in sections) output = s.Update(output);
        return output;
    }
}