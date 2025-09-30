using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using TMPro;

[RequireComponent(typeof(Animator))]
public partial class PaddlePoseDriver : MonoBehaviour
{
    // ───── Input / Baseline / Rig ─────
    [Header("Arc space (axes used for rotation)")]
    public Transform ArcFrame; // 비우면 Paddle 축 기준

    [Header("Input (Joy-Con mapped cubes)")]
    public Transform LeftCube;
    public Transform RightCube;
    public bool InvertLeftX  = false;
    public bool InvertRightX = false;

    [Header("Baseline (zero at game start)")]
    [Tooltip("GameStarted 되는 첫 프레임에 현재 포즈를 0도로 캘리브레이션")]
    public bool AutoCalibrateOnGameStart = true;
    [Tooltip("수동 재보정 키")]
    public KeyCode RecalibrateKey = KeyCode.C;
    [Tooltip("베이스라인 주변의 미세 떨림 무시(도)")]
    public float ZeroDeadbandDeg = 2f;

    [Header("Rig / Animator")]
    public RigBuilder RigBuilder;
    public Rig        RigLayer;
    public Animator   Animator;
    public Transform  CharacterRoot;

    // ───── Stats / UI ─────
    [Header("Stroke Stats & UI")]
    public int DistanceMeters = 0;
    public int PaddleCount = 0;
    public TMP_Text distanceText;
    public TMP_Text paddleCountText;

    [Header("Angle Stats & UI")]
    public TMP_Text angleText;

    [Header("Counting / Thresholds")]
    [Tooltip("이 각도(절댓값) 미만이면 카운트/디스턴스 무시")]
    public float MinCountAngleDeg = 10f;

    [Header("Gate reset")]
    [Tooltip("베이스라인 ±이 각도 이하로 돌아와야 다시 트리거")]
    public float ResetAngleDeg = 5f;

    // ───── Internal state (공용) ─────
    bool _calibrated = false;
    float _baseLX = 0f, _baseRX = 0f;         // 로컬X 기준
    float _baseLX_WorldX = 0f, _baseRX_WorldX = 0f; // 다리용 월드X 기준

    float _lx, _rx;         // Δ각
    float _domXPrev;
    sbyte _domTrend;        // +1 up, -1 down, 0 hold
    float _peakDomAngle;
    string _peakDomSide = "-";

    float _phase, _phaseVel;
    float _domBlend = 0.5f;

    float _sumLeftAngleAbs  = 0f;
    float _sumRightAngleAbs = 0f;
    int   _countLeft = 0;
    int   _countRight = 0;

    public float AvgAngleLeftDeg  => (_countLeft  > 0) ? (_sumLeftAngleAbs  / _countLeft)  : 0f;
    public float AvgAngleRightDeg => (_countRight > 0) ? (_sumRightAngleAbs / _countRight) : 0f;
    public int LeftStrokeCount  => _countLeft;
    public int RightStrokeCount => _countRight;

    struct SideGate { public bool locked; }
    SideGate _gateL, _gateR;

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

    void Awake()
    {
        if (!Animator) Animator = GetComponent<Animator>();
        if (!CharacterRoot) CharacterRoot = transform;

        if (Animator)
        {
            Animator.enabled = true;
            Animator.speed = 1f;
            Animator.updateMode = AnimatorUpdateMode.Normal;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (!RigBuilder) RigBuilder = GetComponent<RigBuilder>();
        if (!RigBuilder) RigBuilder = gameObject.AddComponent<RigBuilder>();
        if (!RigLayer)   RigLayer   = GetComponentsInChildren<Rig>(true).FirstOrDefault();
        if (RigLayer)
        {
            bool has = RigBuilder.layers.Any(l => l.rig == RigLayer);
            if (!has) RigBuilder.layers.Add(new RigLayer(RigLayer));
        }
        RigBuilder.Build();

        var skins = (CharacterRoot ? CharacterRoot : transform).GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var s in skins) s.updateWhenOffscreen = true;

        if (Paddle) { _paddleBasePos = Paddle.localPosition; _paddleBaseRot = Paddle.localRotation; }
        if (ChestTarget) { _chestBasePos = ChestTarget.localPosition; _chestBaseRot = ChestTarget.localRotation; }
        if (SpineTarget) { _spineBasePos = SpineTarget.localPosition; _spineBaseRot = SpineTarget.localRotation; }
        if (NeckTarget)  { _neckBaseRot = NeckTarget.localRotation; }

        // BodyRoot 기준 포즈
        var br = BodyRoot ? BodyRoot : CharacterRoot;
        if (br)
        {
            _bodyRootBaseRot = br.localRotation;
            _bodyRootBasePos = br.localPosition;
        }

        // Legs base world rot
        if (c_LEG_L) _legLBaseWorldRot = c_LEG_L.rotation;
        if (c_LEG_R) _legRBaseWorldRot = c_LEG_R.rotation;

        // Rigidbody 준비(Physics partial에서 사용)
        EnsureRigidbodyAndPropelTarget();

        _gateL.locked = _gateR.locked = false;
        _calibrated = false;
        RefreshStatsUI();
    }

    void CalibrateFromCurrent()
    {
        _baseLX = ReadLocalX(LeftCube,  InvertLeftX);
        _baseRX = ReadLocalX(RightCube, InvertRightX);
        _baseLX_WorldX = ReadWorldX(LeftCube,  InvertLeftX);
        _baseRX_WorldX = ReadWorldX(RightCube, InvertRightX);

        if (c_LEG_L) _legLBaseWorldRot = c_LEG_L.rotation;
        if (c_LEG_R) _legRBaseWorldRot = c_LEG_R.rotation;

        _gateL.locked = _gateR.locked = false;
        _domXPrev = 0f; _domTrend = 0;
        _propulsion = 0f;

        _calibrated = true;
    }

    void Update()
    {
        if (!GameStarter.GameStarted) return;

        if (!_calibrated && AutoCalibrateOnGameStart) CalibrateFromCurrent();
        if (Input.GetKeyDown(RecalibrateKey)) CalibrateFromCurrent();
        if (RigLayer) RigLayer.weight = 1f;

        // 입력 → Δ각
        float rawLX = ReadLocalX(LeftCube,  InvertLeftX);
        float rawRX = ReadLocalX(RightCube, InvertRightX);
        _lx = Mathf.Abs(rawLX - _baseLX) < ZeroDeadbandDeg ? 0f : (rawLX - _baseLX);
        _rx = Mathf.Abs(rawRX - _baseRX) < ZeroDeadbandDeg ? 0f : (rawRX - _baseRX);

        bool  leftDominant = Mathf.Abs(_lx) >= Mathf.Abs(_rx);
        float domX   = leftDominant ? _lx : _rx;

        // 피크 트렌드
        float magPrev = Mathf.Abs(_domXPrev);
        float magCurr = Mathf.Abs(domX);
        sbyte domTrendNow =
            (magCurr > magPrev + 0.5f) ? (sbyte)+1 :
            (magCurr < magPrev - 0.5f) ? (sbyte)-1 :
            _domTrend;

        if (_domTrend == +1 && domTrendNow == -1)
        {
            _peakDomAngle = _domXPrev;
            _peakDomSide  = leftDominant ? "Left" : "Right";
            TryTrigger(_peakDomSide == "Left", Mathf.Abs(_peakDomAngle));
        }
        _domTrend = domTrendNow;
        _domXPrev = domX;

        // 위상값(0~1)
        float target = Mathf.Clamp01(Mathf.Abs(domX) / Mathf.Max(1f, FullAngleDeg));
        float smoothTime = (domTrendNow == -1) ? PhaseSmoothDown : PhaseSmoothUp;
        _phase = Mathf.SmoothDamp(_phase, target, ref _phaseVel, Mathf.Max(1e-3f, smoothTime));

        float strength = _phase;
        if (strength < Deadzone) strength = 0f;
        float theta = strength * Mathf.PI;
        float sinT  = Mathf.Sin(theta);
        float cosT  = Mathf.Cos(theta);

        // 애니메이션 계층 호출
        AnimatePaddleAndHands(leftDominant, domTrendNow, sinT, cosT, strength);
        AnimateUpperBody(leftDominant);
        UpdateElbowHints(Time.deltaTime);
        UpdateLegTargets(Time.deltaTime);

        // 추진량 계산
        {
            float absDom = Mathf.Abs(leftDominant ? _lx : _rx);
            float drive = 0f;
            if (absDom > PropulsionDeadbandDeg)
                drive = Mathf.InverseLerp(PropulsionDeadbandDeg, Mathf.Max(PropulsionDeadbandDeg + 1f, FullAngleDeg), absDom);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float a  = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, PropulsionSmoothing));
            _propulsion = Mathf.Lerp(_propulsion, drive, a);
        }

        // 게이트 해제
        UpdatePeakGates_AngleOnly(Mathf.Abs(_lx), Mathf.Abs(_rx));
    }

    // ───── 카운팅 / UI ─────
    void TryTrigger(bool leftSide, float angleAbsDeg)
    {
        if (angleAbsDeg < MinCountAngleDeg) return;
        if (leftSide)
        {
            if (_gateL.locked) return;
            _gateL.locked = true;
        }
        else
        {
            if (_gateR.locked) return;
            _gateR.locked = true;
        }
        RegisterStroke(leftSide, angleAbsDeg);
    }

    void RegisterStroke(bool leftSide, float angleAbsDeg)
    {
        int addDist = Mathf.RoundToInt(angleAbsDeg / 10f);
        if (addDist < 1) addDist = 1;

        DistanceMeters += addDist;
        PaddleCount    += 1;

        if (leftSide) { _sumLeftAngleAbs  += angleAbsDeg; _countLeft++;  }
        else          { _sumRightAngleAbs += angleAbsDeg; _countRight++; }

        Debug.Log($"[Stroke++] side={(leftSide ? "Left" : "Right")}, angleAbs={angleAbsDeg:0.0}°, +{addDist}m, count={PaddleCount}");
        RefreshStatsUI();
    }

    void UpdatePeakGates_AngleOnly(float absLeftDeg, float absRightDeg)
    {
        if (_gateL.locked && absLeftDeg  <= ResetAngleDeg) _gateL.locked = false;
        if (_gateR.locked && absRightDeg <= ResetAngleDeg) _gateR.locked = false;
    }

    void RefreshStatsUI()
    {
        if (distanceText)    distanceText.text = $"{DistanceMeters} m";
        if (paddleCountText) paddleCountText.text = $"x {PaddleCount}";
        if (angleText)       angleText.text     = $"좌 - {AvgAngleLeftDeg:0.#}° / 우 - {AvgAngleRightDeg:0.#}°";
    }

    public void ResetStrokeStats()
    {
        DistanceMeters = 0;
        PaddleCount    = 0;
        _sumLeftAngleAbs = _sumRightAngleAbs = 0f;
        _countLeft = _countRight = 0;
        _gateL.locked = false;
        _gateR.locked = false;
        RefreshStatsUI();
    }

    // ───── Utility ─────
    float ReadLocalX(Transform t, bool invert)
    {
        if (!t) return 0f;
        float x = t.localEulerAngles.x;
        if (x > 180f) x -= 360f;
        return invert ? -x : x;
    }

    float ReadWorldX(Transform t, bool invert)
    {
        if (!t) return 0f;
        float x = t.eulerAngles.x;
        if (x > 180f) x -= 360f;
        return invert ? -x : x;
    }
}
