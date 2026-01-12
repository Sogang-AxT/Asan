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

        // 왼쪽 다리 아바타 애니메이션
        var targetZL = Mathf.Lerp(this._initThighL, this._initThighL - this.maxRotationAngle,
            this.playerMovementController.LeftPhase);
        var currentRotationL = this.thighL.localEulerAngles;
        this.thighL.localEulerAngles = new Vector3(currentRotationL.x, currentRotationL.y, targetZL);
        
        // 오른쪽 다리 아바타 애니메이션
        var targetZR = Mathf.Lerp(this._initThighR, this._initThighR - this.maxRotationAngle,
            this.playerMovementController.RightPhase);
        var currentRotationR = this.thighR.localEulerAngles;
        this.thighR.localEulerAngles = new Vector3(currentRotationR.x, currentRotationR.y, targetZR);

        
        // var isLeft = this.playerMovementController.LeftDominant;
        //
        // if (isLeft) {   // thighL: 1.52f ~ -28.48f (0 ~ 30을 1.52 ~ -28.48로 매핑)
        //     var targetZL = Mathf.Lerp(this._initThighL, (this._initThighL - this.maxRotationAngle),
        //             this.playerMovementController.Propulsion);
        //     
        //     this._currentZL 
        //         = Mathf.SmoothDampAngle(this._currentZL, targetZL, ref this._velocityZL, this.rotationSmoothTime);
        //     
        //     var currentRotationL = this.thighL.localEulerAngles;
        //     // var newZL = Mathf.LerpAngle(currentRotationL.z, targetZL, Time.deltaTime * this.rotationSpeed);
        //     
        //     this.thighL.localEulerAngles = new Vector3(currentRotationL.x, currentRotationL.y, this._currentZL);
        // }
        // else {  // thighR: -179.856f ~ -209.856f (0 ~ 30을 -179.856 ~ -209.856로 매핑)
        //     var targetZR = Mathf.Lerp(this._initThighR, (this._initThighR - this.maxRotationAngle), 
        //             this.playerMovementController.Propulsion);
        //     
        //     this._currentZR
        //         = Mathf.SmoothDampAngle(this._currentZR, targetZR, ref this._velocityZR, this.rotationSmoothTime);
        //     
        //     var currentRotationR = this.thighR.localEulerAngles;
        //     // var newZR = Mathf.LerpAngle(currentRotationR.z, targetZR, Time.deltaTime * this.rotationSpeed);
        //
        //     this.thighR.localEulerAngles = new Vector3(currentRotationR.x, currentRotationR.y, this._currentZR);
        // }
    }
}