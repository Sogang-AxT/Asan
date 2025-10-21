using UnityEngine;

public class CharacterAnimationEvent : MonoBehaviour {
    private static readonly int Blend = Animator.StringToHash("Blend");
    private static readonly int IsLeft = Animator.StringToHash("isLeft");

    [SerializeField] private PlayerMovementController playerMovementController;
    [SerializeField] private Animator animator;
    
    [SerializeField] private Transform rightPaddle;
    [SerializeField] private Transform leftPaddle;

    [SerializeField] private ParticleSystem rightParticleSystem;
    [SerializeField] private ParticleSystem leftParticleSystem;


    // private Vector3 _currentLeftPaddleVelocity;
    // private Vector3 _currentRightPaddleVelocity;
    //
    // private Vector3 _previousLeftPaddleVelocity;
    // private Vector3 _previousRightPaddleVelocity;
    //
    // private Vector3 _smoothLeftPaddleVelocity;
    // private Vector3 _smoothRightPaddleVelocity;

    private float _velocitySmoothing = 0.1f;
    private float _animationBlending;
    
    
    private void Init() {
        this._animationBlending = 0.5f;
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
        
        this._animationBlending = this.playerMovementController.Propulsion; // 0f ~ 1f
        this.animator.SetFloat(Blend, this._animationBlending);
        this.animator.SetBool(IsLeft, this.playerMovementController.LeftDominant);
    }

    public void ApplyRightPaddleVFX() {
        Collider[] colliders = Physics.OverlapSphere(rightPaddle.position, 5);

        foreach (var col in colliders) {
            if (col.transform.CompareTag("Water")) {
                leftParticleSystem.Play();
            }
        }
    }
    
    public void ApplyLeftPaddleVFX() {
        Collider[] colliders = Physics.OverlapSphere(leftPaddle.position, 5);

        foreach (var col in colliders) {
            if (col.transform.CompareTag("Water")) {
                rightParticleSystem.Play();
            }
        }
    }
}