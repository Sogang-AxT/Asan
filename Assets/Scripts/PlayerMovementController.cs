using System;
using RageRunGames.KayakController;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour {
    [Header("Arc space (axes used for rotation)")]
    public Transform arcFrameTransform; // 비우면 Paddle 축 기준
    
    [FormerlySerializedAs("legL")] [Header("Input (Joy-Con mapped cubes)")]
    public Transform joyconCubeLeft;
    [FormerlySerializedAs("legR")] public Transform joyconCubeRight;
    public bool isInvertedLeft;    // false
    public bool isInvertedRight;   // false

    [Header("Joy-Con Calibrate")] 
    public bool useAngleAutoCalibrator;       // true
    public KeyCode manualCalibrateKeyCode;    // Keycode.C
    public float deadZoneDegree;              // 2.0f
    
    [Header("Counting / Thresholds")] 
    public float minCountAngle = 10f;
    public float countResetAngle = 5f;
    
    private bool _isAngleCalibrated; // false
    
    private (bool, bool) _sideGateLockTuple;
    private (float, float) _localJoyconXTuple;  // 다리 로컬 기준 (좌, 우)
    private (float, float) _worldJoyconXTuple;  // 다리 월드 기준 (좌, 우)
    private (float, float) _deltaAngleTuple;    // 다리 델타 각 (좌, 우)
    private (float, float) _angleSumAbsTuple;   // 다리 각도 절대값 (좌, 우)
    private (int, int) _movementCountTuple;     // 다리 움직임 카운팅 (좌, 우)
    
    public float LegMovementAvgAngleLeft 
        => (_movementCountTuple.Item1 > 0) ? (_angleSumAbsTuple.Item1 / _movementCountTuple.Item1) : 0f;
    public float LegMovementAvgAngleRight 
        => (_movementCountTuple.Item2 > 0) ? (_angleSumAbsTuple.Item2 / _movementCountTuple.Item2) : 0f;
    public int LegStrokeCountLeft
        => _movementCountTuple.Item1;
    public int LegStrokeCountRight 
        => _movementCountTuple.Item2;
    
    // TODO: 용도 확인 후 정리
    private sbyte _domTrend;            // +1 up, -1 down, 0 hold
    private float _domXPrev;            // 왼쪽 다리가 더 많이 들렸는가? -> 그쪽으로 인식할까?
    private float _domBlend = 0.5f;
    private float _peakDomAngle;
    private float _phase, _phaseVel;
    private string _peakDomSide = "-";
    
    // Animation shared bases
    Vector3    _paddleBasePos;
    Quaternion _paddleBaseRot;
    Vector3    _chestBasePos;
    Quaternion _chestBaseRot;
    Vector3    _spineBasePos;
    Quaternion _spineBaseRot;
    Quaternion _neckBaseRot;
    // BodyRoot base
    Quaternion _bodyRootBaseRot;
    Vector3    _bodyRootBasePos;
    
    
    // -- Physics -- //
    
    [Header("Physics Assist")]
    public bool enablePhysicsAssist = true;

    [Header("Water Level / Buoyancy")]
    public float constantWaterLevel = 0f;
    public Transform[] buoyancyPoints;
    public float buoyancyStrength = 9.81f;
    public float buoyancyScale = 1.0f;

    [Header("Drag / Damping")]
    public float baseDrag = 1.5f;
    public float dragSpeedFactor = 0.05f;
    public float baseAngularDrag = 3.0f;
    public float angularDragFactor = 0.025f;
    public float lateralDampingMul = 0.8f;

    [Header("Clamp / Upright")]
    public float maxVelocity = 6f;
    public float maxAngularVelocity = 5f;
    public float uprightStartAngleDeg = 5f;
    public float uprightStability = 12f;
    public float uprightAngularDamping = 0.1f;

    [Header("(Optional) Center of Mass")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.1f, 0f);
    public bool applyCenterOfMass = true;
    
    [Header("Propel Target / Direction")]
    public Transform propelTargetTransform;              // 전진 대상 (연속 힘 적용)
    public Transform propelForwardRef;
    public bool useWorldSpaceForward = false;
    
    [Header("Angle-driven Propulsion")]
    public float propulsionDeadBandDeg = 3f;    // Δ각이 이 값(도) 미만이면 전진 힘 0
    public float propulsionGain = 1.2f;         // 전진 힘 스케일(값↑ = 더 세게)
    public float propulsionSmoothing = 0.15f;   // Δ각→전진 힘 저역통과(초)
    public float yawTorqueFromDelta = 0.25f;    // 좌/우 Δ각 차이에 비례하는 약한 Yaw 토크
    public bool scaleYawByPropulsion = true;    // Yaw 토크를 추진량에 비례시킬지

    private float _propulsion;   // 추친력
    
    private Animator _animator;
    private Rigidbody _rigidbody;

    
    private void Init() {
        this._animator = GetComponent<Animator>();
        this._rigidbody = GetComponent<Rigidbody>();

        this._sideGateLockTuple.Item1 = false;
        this._sideGateLockTuple.Item2 = false;
        this._isAngleCalibrated = false;

        if (this.useAngleAutoCalibrator && !this._isAngleCalibrated) {
            JoyconCalibrator();
        }
        
        if (this.applyCenterOfMass && this._rigidbody) {
            this._rigidbody.centerOfMass += this.centerOfMassOffset;
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
            // TODO: 코드 리뷰
            // ApplyBuoyancyAssist(this._rigidbody);
            // ApplyWaterDragAssist(this._rigidbody);
            // ClampVelocities(this._rigidbody);
            // UprightStabilization(this._rigidbody);
        }
        
        // Δ각 기반 연속 전진 & Yaw 살짝.. 
        if (this._propulsion > 1e-4f && this.propelTargetTransform) {
            var forwardDirection = 
                this.useWorldSpaceForward ? Vector3.forward : this.propelTargetTransform.forward;
            var horizonForwardDirection = 
                Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;
            var forceMagnitude = this.propulsionGain * this._propulsion;
            var delta = 
                Mathf.Clamp(this._deltaAngleTuple.Item1 - this._deltaAngleTuple.Item2, -45f, 45f);
            var yaw = this.yawTorqueFromDelta * (delta / 20f);  // TODO: 하드코딩

            if (horizonForwardDirection.sqrMagnitude < 1e-6f) {
                horizonForwardDirection = Vector3.forward;
            }
            
            if (this.scaleYawByPropulsion) {
                yaw *= this._propulsion;
            }
            
            if (this.propelTargetTransform.TryGetComponent<Rigidbody>(out var rb) && rb.isKinematic == false) {
                rb.AddForce(horizonForwardDirection * forceMagnitude, ForceMode.Force);

                if (Mathf.Abs(yaw) > 1e-5f) {
                    rb.AddForce(Vector3.up * yaw, ForceMode.Force);
                }
            }
            else {
                this.propelTargetTransform.position += horizonForwardDirection * (forceMagnitude * Time.fixedDeltaTime);

                if (Mathf.Abs(yaw) > 1e-5f) {
                    this.propelTargetTransform.Rotate(
                        0f, yaw * Time.fixedDeltaTime, 0f, Space.World);
                }
            }
        }
    }
    
    private void Update() {
        if (!GameStarter.GameStarted) return;
        
        // 수동 보정
        if (Input.GetKeyDown(this.manualCalibrateKeyCode)) {
            JoyconCalibrator();
        }
        
        // 입력 → Δ각
        
    }
    
    private void JoyconCalibrator() {
        // Joycon 로컬 X 값
        this._localJoyconXTuple.Item1 = ReadLocalX(this.joyconCubeLeft, this.isInvertedLeft);
        this._localJoyconXTuple.Item2 = ReadLocalX(this.joyconCubeRight, this.isInvertedRight);
        
        // Joycon 월드 X 값
        this._worldJoyconXTuple.Item1 = ReadWorldX(this.joyconCubeLeft, this.isInvertedLeft);
        this._worldJoyconXTuple.Item2 = ReadWorldX(this.joyconCubeRight, this.isInvertedRight);

        this._sideGateLockTuple.Item1 = this._sideGateLockTuple.Item2 = false;
        
        this._domXPrev = 0f;
        this._domTrend = 0;
        this._propulsion = 0f;
        
        // 보정 완료 알림
        this._isAngleCalibrated = true;
    }
    
    
    // TODO: 코드 리뷰
    // void ApplyBuoyancyAssist(Rigidbody rb) {
    //     if (this.buoyancyPoints is { Length: > 0 }) {
    //         foreach (var p in this.buoyancyPoints) {
    //             if (!p) continue;
    //             float depth = ConstantWaterLevel - p.position.y;
    //             if (depth > 0f)
    //                 rb.AddForceAtPosition(Vector3.up * depth * BuoyancyStrength * BuoyancyScale, p.position, ForceMode.Acceleration);
    //         }
    //     }
    //     else
    //     {
    //         float depth = ConstantWaterLevel - rb.worldCenterOfMass.y;
    //         if (depth > 0f)
    //             rb.AddForce(Vector3.up * depth * BuoyancyStrength * BuoyancyScale, ForceMode.Acceleration);
    //     }
    // }
    //
    // void ApplyWaterDragAssist(Rigidbody rb)
    // {
    //     float speed = rb.velocity.magnitude;
    //     rb.drag = BaseDrag + speed * DragSpeedFactor;
    //
    //     float angSpeed = rb.angularVelocity.magnitude;
    //     rb.angularDrag = BaseAngularDrag + angSpeed * AngularDragFactor;
    //
    //     // 로컬 X 횡미끄럼 억제
    //     Vector3 localV = rb.transform.InverseTransformDirection(rb.velocity);
    //     localV.x *= Mathf.Clamp01(LateralDampingMul);
    //     rb.velocity = rb.transform.TransformDirection(localV);
    // }
    //
    // void ClampVelocities(Rigidbody rb)
    // {
    //     if (rb.velocity.magnitude > MaxVelocity)
    //         rb.velocity = rb.velocity.normalized * MaxVelocity;
    //
    //     if (rb.angularVelocity.magnitude > MaxAngularVelocity)
    //         rb.angularVelocity = rb.angularVelocity.normalized * MaxAngularVelocity;
    // }
    //
    // void UprightStabilization(Rigidbody rb)
    // {
    //     Vector3 desiredFwdOnPlane =
    //         UseWorldSpaceForward ? Vector3.ProjectOnPlane(Vector3.forward, Vector3.up)
    //                              : Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up);
    //     if (desiredFwdOnPlane.sqrMagnitude < 1e-6f) desiredFwdOnPlane = Vector3.forward;
    //
    //     Quaternion targetRot = Quaternion.LookRotation(desiredFwdOnPlane.normalized, Vector3.up);
    //
    //     float tilt = Vector3.Angle(rb.transform.up, Vector3.up);
    //     if (tilt < UprightStartAngleDeg) return;
    //
    //     Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
    //     delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
    //     if (angleDeg > 180f) angleDeg -= 360f;
    //     float angleRad = angleDeg * Mathf.Deg2Rad;
    //
    //     Vector3 torque = axis.normalized * (angleRad * UprightStability)
    //                    - rb.angularVelocity * UprightAngularDamping;
    //
    //     rb.AddTorque(torque, ForceMode.Acceleration);
    // }

    
    
    
    
    private float ReadLocalX(Transform t, bool invert)
    {
        if (!t) return 0f;
        float x = t.localEulerAngles.x;
        if (x > 180f) x -= 360f;
        return invert ? -x : x;
    }

    private float ReadWorldX(Transform t, bool invert)
    {
        if (!t) return 0f;
        float x = t.eulerAngles.x;
        if (x > 180f) x -= 360f;
        return invert ? -x : x;
    }
}