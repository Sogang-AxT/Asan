using UnityEngine;
using TMPro;

// 기존에 만들었던 BoatPhysics를 재사용합니다!
[RequireComponent(typeof(BoatPhysics))]
[RequireComponent(typeof(JoyconAccelInputReader))]
public class PlayerAccelBoatController : MonoBehaviour
{
    [Header("Modules")]
    private BoatPhysics _physics;
    private JoyconAccelInputReader _input;

    [Header("Physics Movement")]
    [Tooltip("최대 속도로 인정할 입력값")]
    public float maxInputLimit = 1.5f;

    [Tooltip("전진 힘 (추천: 60~80)")]
    public float propulsionGain = 70f;

    [Tooltip("관성: 클수록 반응이 느리고 묵직함 (0.15~0.2)")]
    public float propulsionSmoothing = 0.2f;

    [Tooltip("손 놨을 때 멈추는 속도 (3.0)")]
    public float stopDecaySpeed = 3.0f;

    [Header("Steering")]
    public float yawTorqueFromDelta = 0.3f;
    public bool scaleYawByPropulsion = true;

    [Header("Counting Logic")]
    public float minCountInput = 0.6f;
    public float resetCountInput = 0.2f;
    public float strokeCooldown = 0.6f;

    [Header("UI & Score")]
    public TMP_Text distanceText;
    public TMP_Text paddleCountText;
    [SerializeField] private ScoreManager scoreManager;

    // --- 내부 변수 ---
    private float _propulsion;
    private float _currentCooldownL = 0f;
    private float _currentCooldownR = 0f;
    private bool _gateLockL, _gateLockR;

    // 통계 변수
    private int _distanceMeters = 0;
    private int _paddleCount = 0;

    // --- 외부 공개 프로퍼티 (애니메이션 연동용) ---
    public float Propulsion => _propulsion;
    public bool LeftDominant => _input.LeftDominant;

    private void Awake()
    {
        _physics = GetComponent<BoatPhysics>();
        _input = GetComponent<JoyconAccelInputReader>();
    }

    private void Update()
    {
        if (!GameStarter.GameStarted) return;

        // 1. 추진력 계산 (연속적 가속 로직)
        CalculatePropulsionContinuous();

        // 2. 스트로크 카운팅
        UpdateStrokeCountingLogic();
    }

    private void FixedUpdate()
    {
        // 3. 물리 엔진에 명령 하달 (BoatPhysics 재사용)
        ApplyMovementToPhysics();
    }

    private void CalculatePropulsionContinuous()
    {
        float targetDrive = 0f;
        float inputL = _input.InputL;
        float inputR = _input.InputR;
        float maxInput = Mathf.Max(inputL, inputR);

        // 현재 더 큰 입력값을 가진 쪽의 데드존 값을 가져옴 (Reader에서 설정된 값)
        // Reader가 이미 데드존 처리를 해서 0으로 보냈지만, 
        // InverseLerp의 min 값으로 쓰기 위해 원본 데드존 설정값이 필요할 수 있음.
        // 여기서는 Reader가 이미 컷팅했으므로 0보다 크면 유효 입력으로 간주.

        if (maxInput > 0)
        {
            // Reader의 raw값 기준이 아니라 처리된 input 기준이므로 
            // 0 ~ maxInputLimit 사이에서 보간
            targetDrive = Mathf.InverseLerp(0f, maxInputLimit, maxInput);
        }

        // 멈출 때는 더 빠르게 감속 (stopDecaySpeed)
        float currentSmoothing = (targetDrive > 0) ? propulsionSmoothing : (propulsionSmoothing / stopDecaySpeed);
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float t = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, currentSmoothing));

        _propulsion = Mathf.Lerp(_propulsion, targetDrive, t);

        if (_propulsion < 0.01f) _propulsion = 0f;
    }

    private void ApplyMovementToPhysics()
    {
        if (_propulsion <= 0.001f) return;

        float force = propulsionGain * _propulsion;

        // 조향 계산 (L/R 차이)
        float inputL = _input.InputL;
        float inputR = _input.InputR;
        float steerDelta = inputL - inputR;
        steerDelta = Mathf.Clamp(steerDelta, -maxInputLimit, maxInputLimit);

        float yaw = yawTorqueFromDelta * (steerDelta / maxInputLimit);

        if (scaleYawByPropulsion) yaw *= _propulsion;

        _physics.ApplyPropulsionForce(force, yaw);
    }

    private void UpdateStrokeCountingLogic()
    {
        if (_currentCooldownL > 0) _currentCooldownL -= Time.deltaTime;
        if (_currentCooldownR > 0) _currentCooldownR -= Time.deltaTime;

        CheckGateAndCount(ref _gateLockL, _input.InputL, true, ref _currentCooldownL);
        CheckGateAndCount(ref _gateLockR, _input.InputR, false, ref _currentCooldownR);
    }

    private void CheckGateAndCount(ref bool gate, float input, bool isLeft, ref float cooldownTimer)
    {
        if (cooldownTimer > 0)
        {
            gate = false;
            return;
        }

        if (!gate && input > minCountInput)
        {
            gate = true;
            ProcessStroke(isLeft, input);
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
        _distanceMeters += addDist;
        _paddleCount++;

        if (distanceText) distanceText.text = _distanceMeters + "m";
        if (paddleCountText) paddleCountText.text = "x " + _paddleCount;
        if (scoreManager) scoreManager.RecordStroke(leftSide, val);
    }
}