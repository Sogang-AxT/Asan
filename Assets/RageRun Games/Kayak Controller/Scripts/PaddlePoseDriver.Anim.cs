using UnityEngine;

public partial class PaddlePoseDriver
{
    // ───── Animation 공용 필드 ─────
    [Header("Paddle & IK Targets")]
    public Transform Paddle;
    public Transform LeftHandTarget;
    public Transform RightHandTarget;
    public Transform ChestTarget;
    public Transform SpineTarget;
    public Transform NeckTarget;

    [Header("Arm IK Hints (to keep elbows bent)")]
    public Transform LeftElbowHint;
    public Transform RightElbowHint;
    public Transform LeftShoulder;
    public Transform RightShoulder;

    [Header("Hand grips on paddle (LOCAL offsets)")]
    public Vector3 LeftGripLocal  = new Vector3( 0.20f, 0.00f, -0.30f);
    public Vector3 RightGripLocal = new Vector3(-0.20f, 0.00f, -0.30f);
    public bool AlignHandsToPaddle = true;

    [Header("Mapping & Limits")]
    public float FullAngleDeg   = 30f; 
    public float PaddlePitchMax = 35f;
    public float PaddleYawMax   = 20f;
    public float PaddleRollMax  = 25f;

    // 상체/척추/목
    public float ChestPitchMax  = 18f;
    public float ChestYawMax    = 20f;
    public float SpinePitchMax  = 10f;
    public float ChestShiftX    = 0.06f;
    public float ChestShiftZ    = 0.07f;

    [Header("Neck (Roll/Z only)")]
    public float NeckYawMaxDeg  = 30f;

    [Header("Lean Into Stroke")]
    public float BodyBowPitchMax = 12f;
    public float BodyLeanRollMax = 10f;
    public float HeadBowPitchMax = 6f;
    public float HeadLeanRollMax = 12f;
    public float LeanLerp        = 10f;
    [Range(0f,1f)] public float LeanReturnScale = 0.75f;

    [Header("Whole-body Bow")]
    public Transform BodyRoot; // 없으면 CharacterRoot
    public float GlobalBowPitchMax      = 6f;
    public float GlobalBowForwardOffset = 0.05f;
    public float GlobalBowLerp          = 10f;

    [Header("Smoothing")]
    public float DominantLerp    = 8f;
    public float PhaseSmoothUp   = 0.06f;
    public float PhaseSmoothDown = 0.18f;
    public float ReturnLerp      = 10f;
    public float PaddleRotLerp   = 10f;
    public float PaddlePosLerp   = 12f;
    public float Deadzone        = 0.02f;

    [Header("Arm IK (elbow hints)")]
    public float ElbowSideOffset = 0.10f;
    public float ElbowDownOffset = 0.05f;
    public float HintFollowLerp  = 12f;
    public float ArmMaxReach     = 0.55f;

    [Header("Leg Targets (baseline=0, apply ΔX)")]
    public Transform c_LEG_L;
    public Transform c_LEG_R;
    public float LegFollowLerp = 12f;
    Quaternion _legLBaseWorldRot, _legRBaseWorldRot;

    // ───── Animation 메서드 ─────
    void AnimatePaddleAndHands(bool leftDominant, sbyte domTrendNow, float sinT, float cosT, float strength)
    {
        if (!Paddle) return;

        Transform frame = ArcFrame ? ArcFrame : Paddle;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // 위치는 항상 베이스로 복귀
        Paddle.localPosition = Vector3.Lerp(Paddle.localPosition, _paddleBasePos, dt * PaddlePosLerp);

        float dir = leftDominant ? +1f : -1f;

        // ▼ 하강(중립 복귀) 구간
        if (domTrendNow == -1)
        {
            Paddle.localRotation = Quaternion.Slerp(Paddle.localRotation, _paddleBaseRot, dt * PaddleRotLerp);
        }
        else
        {
            // 상승/정체: 회전 적용
            float pitch = (PaddlePitchMax * sinT * strength);
            float yaw   = (PaddleYawMax   * cosT * dir * strength);
            float roll  = (PaddleRollMax  * sinT * dir * strength);

            Quaternion deltaWorld =
                  Quaternion.AngleAxis(pitch, frame.right)
                * Quaternion.AngleAxis(yaw,   frame.up)
                * Quaternion.AngleAxis(roll,  frame.forward);

            Quaternion parentRot   = Paddle.parent ? Paddle.parent.rotation : Quaternion.identity;
            Quaternion baseWorld   = parentRot * _paddleBaseRot;
            Quaternion targetWorld = deltaWorld * baseWorld;
            Quaternion targetLocal = Quaternion.Inverse(parentRot) * targetWorld;

            Paddle.localRotation = Quaternion.Slerp(Paddle.localRotation, targetLocal, dt * PaddleRotLerp);
        }

        if (LeftHandTarget && RightHandTarget)
        {
            Vector3 L = Paddle.TransformPoint(LeftGripLocal);
            Vector3 R = Paddle.TransformPoint(RightGripLocal);

            float leadLerp = ReturnLerp * 1.6f; 
            if (leftDominant)
            {
                LeftHandTarget.position  = Vector3.Lerp(LeftHandTarget.position,  L, dt * leadLerp);
                RightHandTarget.position = Vector3.Lerp(RightHandTarget.position, R, dt * ReturnLerp);
            }
            else
            {
                RightHandTarget.position = Vector3.Lerp(RightHandTarget.position, R, dt * leadLerp);
                LeftHandTarget.position  = Vector3.Lerp(LeftHandTarget.position,  L, dt * ReturnLerp);
            }

            if (AlignHandsToPaddle)
            {
                var pr = Paddle.rotation;
                LeftHandTarget.rotation  = Quaternion.Slerp(LeftHandTarget.rotation,  pr, dt * ReturnLerp);
                RightHandTarget.rotation = Quaternion.Slerp(RightHandTarget.rotation, pr, dt * ReturnLerp);
            }

            ClampHandDistance(LeftShoulder,  LeftHandTarget);
            ClampHandDistance(RightShoulder, RightHandTarget);
        }
    }

    void AnimateUpperBody(bool leftDominant)
    {
        var br = BodyRoot ? BodyRoot : CharacterRoot;
        if (br)
        {
            float power = Mathf.Sin(_phase * Mathf.PI);
            if (power < 0f) power = 0f;

            float rootPitch = GlobalBowPitchMax * power;
            Quaternion targetLocalRot = Quaternion.Euler(rootPitch, 0f, 0f) * _bodyRootBaseRot;

            Vector3 fwd = br.parent ? br.parent.TransformDirection(Vector3.forward) : Vector3.forward;
            Vector3 horizFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (horizFwd.sqrMagnitude < 1e-6f) horizFwd = Vector3.forward;

            Vector3 targetWorldPos = (br.parent ? br.parent.TransformPoint(_bodyRootBasePos) : _bodyRootBasePos)
                                   + horizFwd * (GlobalBowForwardOffset * power);
            Vector3 targetLocalPos = br.parent ? br.parent.InverseTransformPoint(targetWorldPos) : targetWorldPos;

            br.localRotation = Quaternion.Slerp(br.localRotation, targetLocalRot, Time.deltaTime * GlobalBowLerp);
            br.localPosition = Vector3.Lerp(br.localPosition, targetLocalPos, Time.deltaTime * GlobalBowLerp);
        }

        float side = leftDominant ? +1f : -1f;
        float power2 = Mathf.Sin(_phase * Mathf.PI);
        float leanScale = _phase * LeanReturnScale + (1f - LeanReturnScale) * _phase;

        if (ChestTarget)
        {
            float chestPitch = (ChestPitchMax * side * _phase) + (BodyBowPitchMax * power2 * _phase);
            float chestYaw   = (ChestYawMax   * side * _phase);
            float chestRoll  = (BodyLeanRollMax * side * leanScale);
            Quaternion chestRot = Quaternion.Euler(chestPitch, chestYaw, chestRoll) * _chestBaseRot;

            ChestTarget.localRotation = Quaternion.Slerp(ChestTarget.localRotation, chestRot, Mathf.Max(ReturnLerp, LeanLerp) * Time.deltaTime);
            Vector3 chestShift = new Vector3(ChestShiftX * side * _phase, 0f, ChestShiftZ * _phase);
            ChestTarget.localPosition = Vector3.Lerp(ChestTarget.localPosition, _chestBasePos + chestShift, Time.deltaTime * ReturnLerp);
        }

        if (SpineTarget)
        {
            float spinePitch = (SpinePitchMax * side * _phase) + (BodyBowPitchMax * 0.5f * power2 * _phase);
            float spineRoll  = (BodyLeanRollMax * 0.6f * side * leanScale);
            Quaternion spineRot = Quaternion.Euler(spinePitch, 0f, spineRoll) * _spineBaseRot;

            SpineTarget.localRotation = Quaternion.Slerp(SpineTarget.localRotation, spineRot, Mathf.Max(ReturnLerp, LeanLerp) * Time.deltaTime);
            SpineTarget.localPosition = Vector3.Lerp(SpineTarget.localPosition, _spineBasePos, Time.deltaTime * ReturnLerp);
        }

        if (NeckTarget)
        {
            float headPitch = HeadBowPitchMax * power2 * _phase;
            float headRoll  = HeadLeanRollMax * side  * leanScale;
            headRoll = Mathf.Clamp(headRoll, -NeckYawMaxDeg, NeckYawMaxDeg);
            Quaternion targetRot = Quaternion.Euler(headPitch, 0f, headRoll) * _neckBaseRot;
            NeckTarget.localRotation = Quaternion.Slerp(NeckTarget.localRotation, targetRot, Mathf.Max(ReturnLerp, LeanLerp) * Time.deltaTime);
        }
    }

    // ───── Arm & Leg helpers ─────
    void ClampHandDistance(Transform shoulder, Transform handTarget)
    {
        if (!shoulder || !handTarget) return;
        Vector3 v = handTarget.position - shoulder.position;
        float d = v.magnitude;
        if (d > ArmMaxReach && d > 1e-5f)
            handTarget.position = shoulder.position + v.normalized * ArmMaxReach * 0.98f;
    }

    void UpdateElbowHints(float dt)
    {
        if (LeftElbowHint && LeftShoulder && LeftHandTarget)
        {
            Vector3 shoulder = LeftShoulder.position;
            Vector3 hand     = LeftHandTarget.position;

            Vector3 dir  = (hand - shoulder).normalized;
            Vector3 up   = Vector3.up;
            Vector3 side = Vector3.Cross(up, dir).normalized;

            Vector3 target = shoulder
                           + dir * Vector3.Distance(shoulder, hand) * 0.5f
                           + side * ElbowSideOffset
                           - up   * ElbowDownOffset;

            LeftElbowHint.position = Vector3.Lerp(LeftElbowHint.position, target, dt * Mathf.Max(1f, HintFollowLerp));
        }

        if (RightElbowHint && RightShoulder && RightHandTarget)
        {
            Vector3 shoulder = RightShoulder.position;
            Vector3 hand     = RightHandTarget.position;

            Vector3 dir  = (hand - shoulder).normalized;
            Vector3 up   = Vector3.up;
            Vector3 side = Vector3.Cross(up, dir).normalized;

            Vector3 target = shoulder
                           + dir * Vector3.Distance(shoulder, hand) * 0.5f
                           - side * ElbowSideOffset
                           - up   * ElbowDownOffset;

            RightElbowHint.position = Vector3.Lerp(RightElbowHint.position, target, dt * Mathf.Max(1f, HintFollowLerp));
        }
    }

    // ΔWorldX만 적용
    void UpdateLegTargets(float dt)
    {
        if (!c_LEG_L && !c_LEG_R) return;

        float lxWorld = ReadWorldX(LeftCube,  InvertLeftX);
        float rxWorld = ReadWorldX(RightCube, InvertRightX);

        float dLX = lxWorld - _baseLX_WorldX;
        float dRX = rxWorld - _baseRX_WorldX;

        if (c_LEG_L)
        {
            Quaternion targetWorld = Quaternion.Euler(-dLX, 0f, 0f) * _legLBaseWorldRot;
            c_LEG_L.rotation = Quaternion.Slerp(c_LEG_L.rotation, targetWorld, dt * LegFollowLerp);
        }

        if (c_LEG_R)
        {
            Quaternion targetWorld = Quaternion.Euler(-dRX, 0f, 0f) * _legRBaseWorldRot;
            c_LEG_R.rotation = Quaternion.Slerp(c_LEG_R.rotation, targetWorld, dt * LegFollowLerp);
        }
    }
}
