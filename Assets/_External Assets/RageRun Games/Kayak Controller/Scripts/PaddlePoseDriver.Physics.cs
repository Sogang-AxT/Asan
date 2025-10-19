using UnityEngine;

public partial class PaddlePoseDriver
{
    // ───── Physics 공용 필드 ─────
    [Header("Physics Assist")]
    public bool EnablePhysicsAssist = true;
    public Rigidbody TargetRb;

    [Header("Water Level / Buoyancy")]
    public float ConstantWaterLevel = 0f;
    public Transform[] BuoyancyPoints;
    public float BuoyancyStrength = 9.81f;
    public float BuoyancyScale    = 1.0f;

    [Header("Drag / Damping")]
    public float BaseDrag = 1.5f;
    public float DragSpeedFactor = 0.05f;
    public float BaseAngularDrag = 3.0f;
    public float AngularDragFactor = 0.025f;
    public float LateralDampingMul = 0.8f;

    [Header("Clamp / Upright")]
    public float MaxVelocity = 6f;
    public float MaxAngularVelocity = 5f;
    public float UprightStartAngleDeg = 5f;
    public float UprightStability = 12f;
    public float UprightAngularDamping = 0.1f;

    [Header("(Optional) Center of Mass")]
    public Vector3 CenterOfMassOffset = new Vector3(0f, -0.1f, 0f);
    public bool ApplyCenterOfMass = true;

    [Header("Propel Target / Direction")]
    public Transform PropelTarget; // 전진 대상(연속 힘 적용)
    public Transform PropelForwardRef;
    public bool UseWorldSpaceForward = false;

    [Header("Angle-driven Propulsion")]
    [Tooltip("Δ각이 이 값(도) 미만이면 전진 힘 0")]
    public float PropulsionDeadbandDeg = 3f;
    [Tooltip("전진 힘 스케일(값↑ = 더 세게)")]
    public float PropulsionGain = 1.2f;
    [Tooltip("Δ각→전진 힘 저역통과(초)")]
    public float PropulsionSmoothing = 0.15f;
    [Tooltip("좌/우 Δ각 차이에 비례하는 약한 Yaw 토크")]
    public float YawTorqueFromDelta = 0.25f;
    [Tooltip("Yaw 토크를 추진량에 비례시킬지")]
    public bool ScaleYawByPropulsion = true;

    // ───── Physics internal ─────
    float _propulsion;

    void EnsureRigidbodyAndPropelTarget()
    {
        if (!TargetRb)
        {
            TargetRb = CharacterRoot ? CharacterRoot.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();
            if (!TargetRb) TargetRb = gameObject.AddComponent<Rigidbody>(); 
        }
        if (ApplyCenterOfMass && TargetRb)
            TargetRb.centerOfMass += CenterOfMassOffset;

        if (!PropelTarget) PropelTarget = TargetRb ? TargetRb.transform : CharacterRoot;
    }

    void FixedUpdate()
    {
        if (EnablePhysicsAssist && TargetRb)
        {
            ApplyBuoyancyAssist(TargetRb);
            ApplyWaterDragAssist(TargetRb);
            ClampVelocities(TargetRb);
            UprightStabilization(TargetRb);
        }

        // Δ각 기반 연속 전진 & Yaw 살짝.. 
        if (_propulsion > 1e-4f && PropelTarget)
        {
            Vector3 fwd =
                UseWorldSpaceForward ? Vector3.forward :
                (PropelForwardRef ? PropelForwardRef.forward :
                 (PropelTarget ? PropelTarget.forward : Vector3.forward));

            Vector3 horizFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (horizFwd.sqrMagnitude < 1e-6f) horizFwd = Vector3.forward;

            float forceMag = PropulsionGain * _propulsion;
           
            if (PropelTarget.TryGetComponent<Rigidbody>(out var prb) && prb.isKinematic == false)
            {
                prb.AddForce(horizFwd * forceMag, ForceMode.Force);

                float delta = Mathf.Clamp(_rx - _lx, -45f, 45f);
                float yaw = YawTorqueFromDelta * (delta / Mathf.Max(1f, FullAngleDeg));
                if (ScaleYawByPropulsion) yaw *= _propulsion;

                if (Mathf.Abs(yaw) > 1e-5f)
                    prb.AddTorque(Vector3.up * yaw, ForceMode.Force);
            }
            else
            {
                PropelTarget.position += horizFwd * (forceMag * Time.fixedDeltaTime);

                float delta = Mathf.Clamp(_rx - _lx, -45f, 45f);
                float yawPerSec = YawTorqueFromDelta * (delta / Mathf.Max(1f, FullAngleDeg));
                if (ScaleYawByPropulsion) yawPerSec *= _propulsion;
                if (Mathf.Abs(yawPerSec) > 1e-5f)
                    PropelTarget.Rotate(0f, yawPerSec * Time.fixedDeltaTime, 0f, Space.World);
            }
        }
    }

    // ───── Physics helpers ─────
    void ApplyBuoyancyAssist(Rigidbody rb)
    {
        if (BuoyancyPoints != null && BuoyancyPoints.Length > 0)
        {
            foreach (var p in BuoyancyPoints)
            {
                if (!p) continue;
                float depth = ConstantWaterLevel - p.position.y;
                if (depth > 0f)
                    rb.AddForceAtPosition(Vector3.up * depth * BuoyancyStrength * BuoyancyScale, p.position, ForceMode.Acceleration);
            }
        }
        else
        {
            float depth = ConstantWaterLevel - rb.worldCenterOfMass.y;
            if (depth > 0f)
                rb.AddForce(Vector3.up * depth * BuoyancyStrength * BuoyancyScale, ForceMode.Acceleration);
        }
    }

    void ApplyWaterDragAssist(Rigidbody rb)
    {
        float speed = rb.velocity.magnitude;
        rb.drag = BaseDrag + speed * DragSpeedFactor;

        float angSpeed = rb.angularVelocity.magnitude;
        rb.angularDrag = BaseAngularDrag + angSpeed * AngularDragFactor;

        // 로컬 X 횡미끄럼 억제
        Vector3 localV = rb.transform.InverseTransformDirection(rb.velocity);
        localV.x *= Mathf.Clamp01(LateralDampingMul);
        rb.velocity = rb.transform.TransformDirection(localV);
    }

    void ClampVelocities(Rigidbody rb)
    {
        if (rb.velocity.magnitude > MaxVelocity)
            rb.velocity = rb.velocity.normalized * MaxVelocity;

        if (rb.angularVelocity.magnitude > MaxAngularVelocity)
            rb.angularVelocity = rb.angularVelocity.normalized * MaxAngularVelocity;
    }

    void UprightStabilization(Rigidbody rb)
    {
        Vector3 desiredFwdOnPlane =
            UseWorldSpaceForward ? Vector3.ProjectOnPlane(Vector3.forward, Vector3.up)
                                 : Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up);
        if (desiredFwdOnPlane.sqrMagnitude < 1e-6f) desiredFwdOnPlane = Vector3.forward;

        Quaternion targetRot = Quaternion.LookRotation(desiredFwdOnPlane.normalized, Vector3.up);

        float tilt = Vector3.Angle(rb.transform.up, Vector3.up);
        if (tilt < UprightStartAngleDeg) return;

        Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;

        Vector3 torque = axis.normalized * (angleRad * UprightStability)
                       - rb.angularVelocity * UprightAngularDamping;

        rb.AddTorque(torque, ForceMode.Acceleration);
    }
}
