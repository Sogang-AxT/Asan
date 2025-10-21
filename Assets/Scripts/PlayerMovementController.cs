using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour {
    [Header("Arc space (axes used for rotation)")]
    // public Transform arcFrameTransform; // 비우면 Paddle 축 기준
    
    [Header("Input (Joy-Con mapped cubes)")]
    public Transform joyconCubeLeft;
    public Transform joyconCubeRight;
    public bool isInvertedLeft;    // false
    public bool isInvertedRight;   // false
    private (float, float) _rawInputTuple;
    
    [Header("Joy-Con Calibrate")] 
    public bool useAngleAutoCalibrator;       // true
    public KeyCode manualCalibrateKeyCode;    // Keycode.C
    public float deadZoneDegree;              // 2.0f
    
    [Header("Counting / Thresholds")] 
    public float minCountAngle;         // 10f
    public float resetCountAngle;       // 5f
    
    private bool _isAngleCalibrated;    // false
    
    private (bool, bool) _gateLockTuple;        // gateL, gateR; gate -> 한 번만 카운트하도록 제한하는 스위치
    private (float, float) _localJoyconXTuple;  // 다리 로컬 기준 (좌, 우); _baseLX, _baseRX
    private (float, float) _worldJoyconXTuple;  // 다리 월드 기준 (좌, 우)
    private (float, float) _deltaAngleTuple;    // 다리 델타 각 (좌, 우); _lx, _rx
    private (float, float) _angleSumAbsTuple;   // 다리 각도 절대값 (좌, 우); _sumLeft/RightAngleAbs
    private (int, int) _movementCountTuple;     // 다리 움직임 카운팅 (좌, 우); _countLeft/Right
    
    // public float LegMovementAvgAngleLeft 
    //     => (_movementCountTuple.Item1 > 0) ? (_angleSumAbsTuple.Item1 / _movementCountTuple.Item1) : 0f;
    // public float LegMovementAvgAngleRight 
    //     => (_movementCountTuple.Item2 > 0) ? (_angleSumAbsTuple.Item2 / _movementCountTuple.Item2) : 0f;
    // public int LegStrokeCountLeft
    //     => _movementCountTuple.Item1;
    // public int LegStrokeCountRight 
    //     => _movementCountTuple.Item2;
    
    // 다리 움직임 피크 계산
    private sbyte _domTrend;            // +1 up, -1 down, 0 hold
    private float _domXPrev;            // 왼쪽 다리가 더 많이 들렸는가? -> 그쪽으로 인식할까?
    private float _domBlend;            // 0.5f
    private float _peakDomAngle;
    private float _phase, _phaseVel;
    private string _peakDomSide;        // "-"
    
    // Animation shared bases
    // Vector3    _paddleBasePos;
    // Quaternion _paddleBaseRot;
    // Vector3    _chestBasePos;
    // Quaternion _chestBaseRot;
    // Vector3    _spineBasePos;
    // Quaternion _spineBaseRot;
    // Quaternion _neckBaseRot;
    // // BodyRoot base
    // Quaternion _bodyRootBaseRot;
    // Vector3    _bodyRootBasePos;
    
    
    // -- Physics -- //
    
    [Header("Physics Assist")]
    public bool enablePhysicsAssist = true;

    [Header("Water Level / Buoyancy")]
    public float constantWaterLevel;    // 0f // TODO: Water Surface cs component;
    public Transform[] buoyancyPoints;
    public float buoyancyStrength;      // 9.81f
    public float buoyancyScale;         // 1.0f

    [Header("Drag / Damping")]
    public float baseDrag;                  // 1.5f
    public float dragSpeedFactor;           // 0.05f
    public float baseAngularDrag;           // 3.0f
    public float angularDragFactor;         // 0.025f
    public float lateralDampingMultiply;    // 0.8f;

    [Header("Clamp / Upright")]
    public float maxVelocity;               // 6f
    public float maxAngularVelocity;        // 5f
    public float uprightStartAngleDeg;      // 5f
    public float uprightStability;          // 12f
    public float uprightAngularDamping;     // 0.1f

    [Header("(Optional) Center of Mass")] 
    public bool applyCenterOfMass;          // true
    private Vector3 _centerOfMassOffset;
    
    [Header("Propel Target / Direction")]
    // public Transform propelForwardRef;
    public Transform propelTargetTransform; // 전진 대상 (연속 힘 적용)
    public bool useWorldSpaceForward;       // false
    
    [Header("Angle-driven Propulsion")]
    public float propulsionDeadBandDeg;         // Δ각이 이 값(도) 미만이면 전진 힘 0;          // 3f
    public float propulsionGain;                // 전진 힘 스케일(값↑ = 더 세게);             // 10f
    public float propulsionSmoothing;           // Δ각→전진 힘 저역통과(초);                 // 0.15f
    public float yawTorqueFromDelta;            // 좌/우 Δ각 차이에 비례하는 약한 Yaw 토크;   // 0.25f
    public bool scaleYawByPropulsion;           // Yaw 토크를 추진량에 비례시킬지;          // true
    public float fullAngleDeg;                  // 20f
    private float _propulsion;                  // 추친력
    
    [Header("Smoothing")]
    // public float dominantLerp = 8f;
    public float phaseSmoothUp;             // 0.06f
    public float phaseSmoothDown;           // 0.18f
    // public float returnLerp = 10f;
    // public float paddleRotLerp = 10f;
    // public float paddlePosLerp = 12f;
    public float deadzone;                  // 0.02f
    public int distanceMeters;              // 0
    public int paddleCount;                 // 0
    // public TMP_Text distanceText;
    // public TMP_Text paddleCountText;
    
    private Rigidbody _rigidbody;
    
    
    private void Init() {
        this._rigidbody = GetComponent<Rigidbody>();

        this._gateLockTuple.Item1 = false;
        this._gateLockTuple.Item2 = false;
        this._isAngleCalibrated = false;

        this._domBlend = 0.5f;
        this._peakDomSide = "-";
        
        this._centerOfMassOffset = new Vector3(0f, -0.1f, 0f);
        
        if (this.useAngleAutoCalibrator && !this._isAngleCalibrated) {
            JoyconCalibrator();
        }
        
        if (this.applyCenterOfMass && this._rigidbody) {
            this._rigidbody.centerOfMass += this._centerOfMassOffset;
        }

        if (!this.propelTargetTransform) {
            this.propelTargetTransform = this._rigidbody.transform;
        }
    }

    private void Awake() {
        Init();
    }

    private void FixedUpdate() {
        if (this.enablePhysicsAssist && this._rigidbody) {
            ApplyBuoyancyAssist(this._rigidbody);
            ApplyWaterDragAssist(this._rigidbody);
            ClampVelocities(this._rigidbody);
            UprightStabilization(this._rigidbody);
        }
        
        // Δ각 기반 연속 전진 & Yaw 살짝.. 
        ApplyPropulsionAndYaw();
    }
    
    private void Update() {
        if (!GameStarter.GameStarted) {
            return;
        }
        
        // 수동 보정
        if (Input.GetKeyDown(this.manualCalibrateKeyCode)) {
            JoyconCalibrator();
        }
        
        // Joycon 자이로 입력
        JoyconGyroInput();
        
        // 움직임 피크 지점 찾기; 피크 트렌드
        PeakTrendCheck();
        
        // 위상값 계산
        CalculatePhase();   // TODO: 애니메이션 처리 외에는 용도가?
        
        // 추진량 계산
        CalculatePropulsion();
    }

    // TODO: 다른 클래스로 이전
    private void JoyconGyroInput() {
        // 로우 데이터 입력 → Δ각
        this._rawInputTuple.Item1 = ReadLocalX(this.joyconCubeLeft, this.isInvertedLeft);
        this._rawInputTuple.Item2 = ReadLocalX(this.joyconCubeRight, this.isInvertedRight);
        
        // 움직임 크기가 데드존 초과인가?
        var deltaValueLeft = this._rawInputTuple.Item1 - this._localJoyconXTuple.Item1;
        this._deltaAngleTuple.Item1 = Mathf.Abs(deltaValueLeft) < this.deadZoneDegree ? 0f : deltaValueLeft;
        
        // Debug.Log(this._deltaAngleTuple.Item1);
        
        var deltaValueRight = this._rawInputTuple.Item2 - this._localJoyconXTuple.Item2;
        this._deltaAngleTuple.Item2 = Mathf.Abs(deltaValueRight) < this.deadZoneDegree ? 0f : deltaValueRight;
    }
    
    // 움직임 피크 지점 찾기; 피크 트렌드
    private void PeakTrendCheck() {
        var leftDominant = Mathf.Abs(this._deltaAngleTuple.Item1) >= Mathf.Abs(this._deltaAngleTuple.Item2);
        var domX = leftDominant ? this._deltaAngleTuple.Item1 : this._deltaAngleTuple.Item2;
        var magPrev = Mathf.Abs(this._domXPrev);
        var magCurr = Mathf.Abs(domX);
        var domTrendNow =
            (magCurr > magPrev + 0.5f) ? (sbyte)+1 :
            (magCurr < magPrev - 0.5f) ? (sbyte)-1 :
            this._domTrend;
        
        if (this._domTrend == +1 && domTrendNow == -1) {    // Peak!
            this._peakDomAngle = this._domXPrev;
            this._peakDomSide  = leftDominant ? "Left" : "Right";
            
            TryTrigger(this._peakDomSide == "Left", Mathf.Abs(_peakDomAngle));
        }
        
        this._domTrend = domTrendNow;
        this._domXPrev = domX;
    }
    
    // 게이트 해제; gate -> 한 번만 카운트하도록 제한하는 스위치
    private void TryTrigger(bool leftSide, float angleAbsDeg) {
        if (angleAbsDeg < this.minCountAngle) {
            return;
        }

        if (leftSide) {
            if (this._gateLockTuple.Item1) {
                return;
            }
            
            this._gateLockTuple.Item1 = true;
        }
        else {
            if (this._gateLockTuple.Item2) {
                return;
            }

            this._gateLockTuple.Item2 = true;
        }
        
        ProcessStroke(leftSide, angleAbsDeg);
    }
    
    // 스트로크 처리 -> 이동 거리, 통계(거리, 패들 횟수) 계산 
    private void ProcessStroke(bool leftSide, float angleAbsDeg) {
        var addDist = Mathf.RoundToInt(angleAbsDeg / 10f);
        
        if (addDist < 1) {
            addDist = 1;
        }

        this.distanceMeters += addDist;
        this.paddleCount += 1;

        if (leftSide) {
            this._angleSumAbsTuple.Item1 += angleAbsDeg; 
            this._movementCountTuple.Item1++;
        }
        else {
            this._angleSumAbsTuple.Item2 += angleAbsDeg; 
            this._movementCountTuple.Item2++;
        }
        
        // TODO: DEBUG
        // Debug.Log($"[Stroke++] side={(leftSide ? "Left" : "Right")}, angleAbs={angleAbsDeg:0.0}°, +{addDist}m, count={this.paddleCount}");
        // RefreshStatsUI();
    }

    // 위상값 계산 (0 ~ 1)
    private void CalculatePhase() {
        var target = Mathf.Clamp01(Mathf.Abs(this._domXPrev) / Mathf.Max(1f, this.fullAngleDeg));
        var smoothTime = (this._domTrend == -1) ? this.phaseSmoothDown : this.phaseSmoothUp;
        this._phase = 
            Mathf.SmoothDamp(_phase, target, ref _phaseVel, Mathf.Max(1e-3f, smoothTime));

        var strength = this._phase;
        
        if (strength < this.deadzone) {
            strength = 0f;
        }
        
        var theta = strength * Mathf.PI;
        var sinT  = Mathf.Sin(theta);
        var cosT  = Mathf.Cos(theta);
    }

    // 추진량 계산
    private void CalculatePropulsion() {
        var drive = 0f;
        var absDom = 
            Mathf.Abs(this._peakDomSide == "Left" ? this._deltaAngleTuple.Item1 : this._deltaAngleTuple.Item2);
        
        if (absDom > this.propulsionDeadBandDeg) {
            drive = Mathf.InverseLerp(
                this.propulsionDeadBandDeg, 
                Mathf.Max(this.propulsionDeadBandDeg + 1f, this.fullAngleDeg), 
                absDom);
        }
        
        var dt = Mathf.Max(Time.deltaTime, 1e-4f);
        var t  = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, this.propulsionSmoothing));
        
        this._propulsion = Mathf.Lerp(this._propulsion, drive, t);
        
        // 피크 초기화
        if (this._gateLockTuple.Item1 && Mathf.Abs(this._deltaAngleTuple.Item1) <= this.resetCountAngle) {
            this._gateLockTuple.Item1 = false;
        }

        if (this._gateLockTuple.Item2 && Mathf.Abs(this._deltaAngleTuple.Item2) <= resetCountAngle) {
            this._gateLockTuple.Item2 = false;
        }
    }
    
    private void StrokeProcessReset() {
        this.distanceMeters = 0;
        this.paddleCount = 0;
        this._angleSumAbsTuple.Item1 = 0f;
        this._angleSumAbsTuple.Item2 = 0f;
        this._movementCountTuple.Item1 = 0;
        this._movementCountTuple.Item2 = 0;
        this._gateLockTuple.Item1 = false;
        this._gateLockTuple.Item2 = false;
    }
    
    // Δ각 기반 연속 전진 & Yaw 살짝.. 
    private void ApplyPropulsionAndYaw() {
        if (!(this._propulsion > 1e-4f) || !this.propelTargetTransform) {
            return;
        }

        var forwardDirection = this.useWorldSpaceForward ? Vector3.forward : this.propelTargetTransform.forward;
        var horizonForwardDirection = 
            Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;
        
        if (horizonForwardDirection.sqrMagnitude < 1e-6f) {
            horizonForwardDirection = Vector3.forward;
        }    
        
        var forceMagnitude = this.propulsionGain * this._propulsion;
        
        // 회전 처리
        if (this.propelTargetTransform.TryGetComponent<Rigidbody>(out var rb) && rb.isKinematic == false) {
            rb.AddForce(horizonForwardDirection * forceMagnitude, ForceMode.Force);
            
            var delta = 
                Mathf.Clamp(this._deltaAngleTuple.Item2 - this._deltaAngleTuple.Item1, -45f, 45f);
            var yaw = this.yawTorqueFromDelta * (delta / this.fullAngleDeg);

            if (this.scaleYawByPropulsion) {
                yaw *= this._propulsion;
            }

            if (Mathf.Abs(yaw) > 1e-5f) {
                rb.AddTorque(Vector3.up * yaw, ForceMode.Force);
            }
        }
        else {
            this.propelTargetTransform.position += horizonForwardDirection * (forceMagnitude * Time.fixedDeltaTime);

            var delta = 
                Mathf.Clamp(this._deltaAngleTuple.Item2 - this._deltaAngleTuple.Item1, -45f, 45f);
            var yaw = this.yawTorqueFromDelta * (delta / this.fullAngleDeg);
            
            if (Mathf.Abs(yaw) > 1e-5f) {
                this.propelTargetTransform.Rotate(
                    0f, yaw * Time.fixedDeltaTime, 0f, Space.World);
            }
        }
    }
    
    // 조이콘 입력 보정자
    private void JoyconCalibrator() {
        // Joycon 로컬 X 값
        this._localJoyconXTuple.Item1 = ReadLocalX(this.joyconCubeLeft, this.isInvertedLeft);
        this._localJoyconXTuple.Item2 = ReadLocalX(this.joyconCubeRight, this.isInvertedRight);
        
        // Joycon 월드 X 값
        this._worldJoyconXTuple.Item1 = ReadWorldX(this.joyconCubeLeft, this.isInvertedLeft);
        this._worldJoyconXTuple.Item2 = ReadWorldX(this.joyconCubeRight, this.isInvertedRight);

        this._gateLockTuple.Item1 = this._gateLockTuple.Item2 = false;
        
        this._domXPrev = 0f;
        this._domTrend = 0;
        this._propulsion = 0f;
        
        // 보정 완료 알림
        this._isAngleCalibrated = true;
    }
    
    // 부력 시뮬레이션
    void ApplyBuoyancyAssist(Rigidbody rb) {
        if (this.buoyancyPoints is { Length: > 0 }) {
            foreach (var point in this.buoyancyPoints) {
                if (point) {
                    var depth = this.constantWaterLevel - point.position.y;
                
                    if (depth > 0f) {
                        rb.AddForceAtPosition(
                            Vector3.up * (depth * this.buoyancyStrength * this.buoyancyScale), 
                            point.position, 
                            ForceMode.Acceleration);
                    }
                }
            }
        }
        else {
            var depth = this.constantWaterLevel - rb.worldCenterOfMass.y;

            if (depth > 0f) {
                rb.AddForce(Vector3.up * (depth * this.buoyancyStrength * this.buoyancyScale), ForceMode.Acceleration);
            }
        }
    }
    
    // 물 위 미끄러짐 시뮬레이션
    void ApplyWaterDragAssist(Rigidbody rb) {
        var speed = rb.velocity.magnitude;
        var angSpeed = rb.angularVelocity.magnitude;
        var localV = rb.transform.InverseTransformDirection(rb.velocity);

        rb.drag = this.baseDrag + (speed * this.dragSpeedFactor);
        rb.angularDrag = this.baseAngularDrag + (angSpeed * this.angularDragFactor);
    
        // 로컬 X 횡미끄럼 억제
        localV.x *= Mathf.Clamp01(this.lateralDampingMultiply);
        rb.velocity = rb.transform.TransformDirection(localV);
    }
    
    // 속도 제한
    void ClampVelocities(Rigidbody rb) {
        if (rb.velocity.magnitude > this.maxVelocity) {
            rb.velocity = rb.velocity.normalized * this.maxVelocity;
        }

        if (rb.angularVelocity.magnitude > this.maxAngularVelocity) {
            rb.angularVelocity = rb.angularVelocity.normalized * this.maxAngularVelocity;
        }
    }
    
    // 카약 전복 방지
    void UprightStabilization(Rigidbody rb) {
        var worldForwardOnPlane = Vector3.ProjectOnPlane(Vector3.forward, Vector3.up);
        var localForwardOnPlane = Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up);
        var desiredForwardOnPlane = this.useWorldSpaceForward ? worldForwardOnPlane : localForwardOnPlane;

        if (desiredForwardOnPlane.sqrMagnitude < 1e-6f) {
            desiredForwardOnPlane = Vector3.forward;
        }
    
        var targetRot = Quaternion.LookRotation(desiredForwardOnPlane.normalized, Vector3.up);
        var tiltAngle = Vector3.Angle(rb.transform.up, Vector3.up);

        if (tiltAngle < this.uprightStartAngleDeg) {
            return;
        }
    
        var delta = targetRot * Quaternion.Inverse(rb.rotation);
        
        delta.ToAngleAxis(out var angleDeg, out var axis);

        if (angleDeg > 180f) {
            angleDeg -= 360f;
        }
        
        var angleRad = angleDeg * Mathf.Deg2Rad;
        var torque = 
            axis.normalized * (angleRad * this.uprightStability) - rb.angularVelocity * this.uprightAngularDamping;
    
        rb.AddTorque(torque, ForceMode.Acceleration);
    }
    
    // 로컬 X 값 읽기
    private float ReadLocalX(Transform t, bool invert) {
        if (!t) {
            return 0f;
        }
        
        var x = t.localEulerAngles.x;

        if (x > 180f) {
            x -= 360f;
        }
        
        return invert ? -x : x;
    }

    // 월드 X 값 읽기
    private float ReadWorldX(Transform t, bool invert) {
        if (!t) {
            return 0f;
        }
        
        var x = t.eulerAngles.x;
        
        if (x > 180f) {
            x -= 360f;
        }
        
        return invert ? -x : x;
    }
}