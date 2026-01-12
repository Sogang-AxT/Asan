using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatPhysics : MonoBehaviour
{
    // =========================================================
    // [1] 물리 설정 (기존과 동일)
    // =========================================================
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
    public float lateralDampingMultiply = 0.8f;

    [Header("Clamp / Upright")]
    public float maxVelocity = 6f;
    public float maxAngularVelocity = 5f;
    public float uprightStartAngleDeg = 5f;
    public float uprightStability = 12f;
    public float uprightAngularDamping = 0.1f;

    [Header("Center of Mass")]
    public bool applyCenterOfMass = true;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.1f, 0f);

    [Header("Propel Target / Direction")]
    public Transform propelTargetTransform;
    public bool useWorldSpaceForward = false;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!propelTargetTransform) propelTargetTransform = transform;

        if (applyCenterOfMass && _rb)
        {
            _rb.centerOfMass += centerOfMassOffset;
        }
    }

    private void FixedUpdate()
    {
        if (enablePhysicsAssist && _rb)
        {
            ApplyBuoyancyAssist(_rb);
            ApplyWaterDragAssist(_rb);
            ClampVelocities(_rb);
            UprightStabilization(_rb);
        }
    }

    // =========================================================
    // [2] 외부 힘 적용 함수들 (호환성 유지 구역)
    // =========================================================

    // 1. [신규] 가속도 컨트롤러용 (PlayerAccelBoatController에서 사용)
    // 인자: 최종 힘(Force), 회전 토크(Torque)
    public void ApplyPropulsionForce(float forceMagnitude, float yawTorque)
    {
        InternalApplyForce(forceMagnitude, yawTorque);
    }

    // 2. [구형] 자이로 컨트롤러용 (기존 PlayerMovementController 등에서 사용)
    // 인자: 추진력(0~1), 게인(배율), 회전 토크
    // 설명: 기존 코드와의 호환성을 위해 남겨둡니다. 내부적으로 계산해서 위 함수와 똑같이 동작합니다.
    public void ApplyPhysicsForce(float propulsionValue, float gain, float yawTorque)
    {
        float calculatedForce = propulsionValue * gain;
        InternalApplyForce(calculatedForce, yawTorque);
    }

    // 3. [구형 이름 호환용] 혹시 ApplyPropulsionAndYaw라는 이름을 쓰던 곳이 있다면 이것도 연결
    public void ApplyPropulsionAndYaw(float force, float yaw)
    {
        InternalApplyForce(force, yaw);
    }

    // [실제 동작] 모든 힘 적용 로직은 여기서 통합 처리
    private void InternalApplyForce(float forceMagnitude, float yawTorque)
    {
        if (forceMagnitude <= 1e-4f && Mathf.Abs(yawTorque) <= 1e-5f) return;

        Vector3 forwardDirection = useWorldSpaceForward ? Vector3.forward : propelTargetTransform.forward;
        Vector3 horizonForwardDirection = Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;

        if (horizonForwardDirection.sqrMagnitude < 1e-6f) horizonForwardDirection = Vector3.forward;

        if (propelTargetTransform.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
        {
            rb.AddForce(horizonForwardDirection * forceMagnitude, ForceMode.Force);
            if (Mathf.Abs(yawTorque) > 1e-5f)
            {
                rb.AddTorque(Vector3.up * yawTorque, ForceMode.Force);
            }
        }
        else
        {
            propelTargetTransform.position += horizonForwardDirection * (forceMagnitude * Time.fixedDeltaTime);
            if (Mathf.Abs(yawTorque) > 1e-5f)
            {
                propelTargetTransform.Rotate(0f, yawTorque * Time.fixedDeltaTime, 0f, Space.World);
            }
        }
    }

    // =========================================================
    // [3] 내부 물리 연산 (부력, 저항 등)
    // =========================================================
    void ApplyBuoyancyAssist(Rigidbody rb)
    {
        if (buoyancyPoints is { Length: > 0 })
        {
            foreach (var point in buoyancyPoints)
            {
                if (point)
                {
                    float depth = constantWaterLevel - point.position.y;
                    if (depth > 0f) rb.AddForceAtPosition(Vector3.up * (depth * buoyancyStrength * buoyancyScale), point.position, ForceMode.Acceleration);
                }
            }
        }
        else
        {
            float depth = constantWaterLevel - rb.worldCenterOfMass.y;
            if (depth > 0f) rb.AddForce(Vector3.up * (depth * buoyancyStrength * buoyancyScale), ForceMode.Acceleration);
        }
    }

    void ApplyWaterDragAssist(Rigidbody rb)
    {
        float speed = rb.velocity.magnitude;
        float angSpeed = rb.angularVelocity.magnitude;
        Vector3 localV = rb.transform.InverseTransformDirection(rb.velocity);

        rb.drag = baseDrag + (speed * dragSpeedFactor);
        rb.angularDrag = baseAngularDrag + (angSpeed * angularDragFactor);
        localV.x *= Mathf.Clamp01(lateralDampingMultiply);
        rb.velocity = rb.transform.TransformDirection(localV);
    }

    void ClampVelocities(Rigidbody rb)
    {
        if (rb.velocity.magnitude > maxVelocity) rb.velocity = rb.velocity.normalized * maxVelocity;
        if (rb.angularVelocity.magnitude > maxAngularVelocity) rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
    }

    void UprightStabilization(Rigidbody rb)
    {
        Vector3 desiredFwd = useWorldSpaceForward ? Vector3.forward : Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up);
        if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = Vector3.forward;

        Quaternion targetRot = Quaternion.LookRotation(desiredFwd.normalized, Vector3.up);
        if (Vector3.Angle(rb.transform.up, Vector3.up) < uprightStartAngleDeg) return;

        Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        Vector3 torque = axis.normalized * (angleDeg * Mathf.Deg2Rad * uprightStability) - rb.angularVelocity * uprightAngularDamping;
        rb.AddTorque(torque, ForceMode.Acceleration);
    }
}