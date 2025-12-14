using UnityEngine;

public class CharacterAnimationController : MonoBehaviour {
    private static readonly int Blend = Animator.StringToHash("Blend");
    private static readonly int IsLeft = Animator.StringToHash("isLeft");

    [SerializeField] private PlayerMovementController_IMU playerMovementController;
    [SerializeField] private Animator animator;


    // private Vector3 _currentLeftPaddleVelocity;
    // private Vector3 _currentRightPaddleVelocity;
    //
    // private Vector3 _previousLeftPaddleVelocity;
    // private Vector3 _previousRightPaddleVelocity;
    //
    // private Vector3 _smoothLeftPaddleVelocity;
    // private Vector3 _smoothRightPaddleVelocity;

    // private float _velocitySmoothing;
    // private float _animationBlending;
    
    
    private void Init() {
        // this._velocitySmoothing = 0.1f;
        // this._animationBlending = 0.5f;
        this.animator.SetFloat(Blend, 0.5f);
    }

    private void Awake() {
        Init();
    }

    private void Update() {
        // Vector3 frameLeftVelocity = (leftPaddle.position - _previousLeftPaddleVelocity) / Time.deltaTime;
        // _smoothLeftPaddleVelocity = Vector3.Lerp(_smoothLeftPaddleVelocity, frameLeftVelocity, _velocitySmoothing);
        // _currentLeftPaddleVelocity = _smoothLeftPaddleVelocity;
        // _previousLeftPaddleVelocity = leftPaddle.position;
        //
        // Vector3 frameRightVelocity = (rightPaddle.position - _previousRightPaddleVelocity) / Time.deltaTime;
        // _smoothRightPaddleVelocity = Vector3.Lerp(_smoothRightPaddleVelocity, frameRightVelocity, _velocitySmoothing);
        // _currentLeftPaddleVelocity = _smoothRightPaddleVelocity;
        // _previousRightPaddleVelocity = rightPaddle.position;
        
        this.animator.SetBool(IsLeft, this.playerMovementController.LeftDominant);
        this.animator.SetFloat(Blend, this.playerMovementController.Propulsion);
    }
}