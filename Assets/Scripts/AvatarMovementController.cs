using UnityEngine;

public class AvatarMovementController : MonoBehaviour {
    [SerializeField] private PlayerMovementController playerMovementController;
    [Space(10f)]
    [SerializeField] private Transform thighL;
    [SerializeField] private Transform thighR;
    
    [Header("Rotation Settings")]
    [SerializeField] private float maxRotationAngle = 30f; // 최대 회전 각도
    [SerializeField] private float rotationSpeed = 5f; // 회전 속도
    
    [SerializeField] private float rotationSmoothTime = 0.1f; // Lerp 대신 SmoothTime 사용
    [SerializeField] private float legEffortDeadband = 0.02f; // 다리별 소음 무시 구간 (0~1)

    private float _initThighL;
    private float _initThighR;
    
    private float _currentZL;  // 현재 Z 각도 추적
    private float _currentZR;
    private float _velocityZL; // SmoothDamp용 속도
    private float _velocityZR;

    
    private void Init() {
        this._initThighL = this.thighL.localEulerAngles.z;
        this._initThighR = this.thighR.localEulerAngles.z;
        
        this._currentZL = this._initThighL;
        this._currentZR = this._initThighR;
    }

    private void Awake() {
        Init();
    }
    
    private void Update() {
        if (!this.playerMovementController) {
            return;
        }

        // 좌/우 이진 전환 대신, 각 다리의 순간 Δ각(절대값)을 0~1 effort로 매핑하여 각각 독립적으로 구동
        float lAbs = this.playerMovementController.DeltaAngleLeftAbs;
        float rAbs = this.playerMovementController.DeltaAngleRightAbs;

        float effortL = this.playerMovementController.MapDeltaToEffort(lAbs);
        float effortR = this.playerMovementController.MapDeltaToEffort(rAbs);

        if (effortL < this.legEffortDeadband) effortL = 0f;
        if (effortR < this.legEffortDeadband) effortR = 0f;

        // thighL: 1.52f ~ -28.48f (0~30을 1.52 ~ -28.48로 매핑)
        var targetZL = Mathf.Lerp(this._initThighL, (this._initThighL - this.maxRotationAngle), effortL);
        this._currentZL = Mathf.SmoothDampAngle(this._currentZL, targetZL, ref this._velocityZL, this.rotationSmoothTime);
        var currentRotationL = this.thighL.localEulerAngles;
        this.thighL.localEulerAngles = new Vector3(currentRotationL.x, currentRotationL.y, this._currentZL);

        // thighR: -179.856f ~ -209.856f (0~30을 -179.856 ~ -209.856로 매핑)
        var targetZR = Mathf.Lerp(this._initThighR, (this._initThighR - this.maxRotationAngle), effortR);
        this._currentZR = Mathf.SmoothDampAngle(this._currentZR, targetZR, ref this._velocityZR, this.rotationSmoothTime);
        var currentRotationR = this.thighR.localEulerAngles;
        this.thighR.localEulerAngles = new Vector3(currentRotationR.x, currentRotationR.y, this._currentZR);
    }
}