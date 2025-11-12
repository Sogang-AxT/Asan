using UnityEngine;

public class AvatarMovementController : MonoBehaviour {
    [SerializeField] private PlayerMovementController playerMovementController;
    [Space(10f)]
    [SerializeField] private Transform thighL;
    [SerializeField] private Transform thighR;
    
    [Header("Rotation Settings")]
    [SerializeField] private float maxRotationAngle = 30f; // 최대 회전 각도
    [SerializeField] private float rotationSpeed = 5f; // 회전 속도

    private float _initThighL;
    private float _initThighR;


    private void Init() {
        this._initThighL = this.thighL.localEulerAngles.z;
        this._initThighR = this.thighR.localEulerAngles.z;
    }

    private void Awake() {
        Init();
    }
    
    private void Update() {
        if (!this.playerMovementController) {
            return;
        }
        
        // Propulsion 값을 0~1 범위에서 0~30으로 변환
        var normalizedRotation = this.playerMovementController.Propulsion * this.maxRotationAngle;
        var isLeft = this.playerMovementController.LeftDominant;
        
        if (isLeft) {   // thighL: 1.52f ~ -28.48f (0~30을 1.52 ~ -28.48로 매핑)
            var targetZL 
                = Mathf.Lerp(this._initThighL, (this._initThighL - this.maxRotationAngle), 
                    normalizedRotation / this.maxRotationAngle);
            var currentRotationL = this.thighL.localEulerAngles;
            var newZL = Mathf.LerpAngle(currentRotationL.z, targetZL, Time.deltaTime * this.rotationSpeed);

            this.thighL.localEulerAngles = new Vector3(currentRotationL.x, currentRotationL.y, newZL);
        }
        else {  // thighR: -179.856f ~ -209.856f (0~30을 -179.856 ~ -209.856로 매핑)
            var targetZR 
                = Mathf.Lerp(this._initThighR, (this._initThighR - this.maxRotationAngle), 
                    normalizedRotation / this.maxRotationAngle);
            var currentRotationR = this.thighR.localEulerAngles;
            var newZR = Mathf.LerpAngle(currentRotationR.z, targetZR, Time.deltaTime * this.rotationSpeed);

            this.thighR.localEulerAngles = new Vector3(currentRotationR.x, currentRotationR.y, newZR);
        }
    }
}