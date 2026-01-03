using UnityEngine;

public class CharacterAnimationController : MonoBehaviour {
    private static readonly int Blend = Animator.StringToHash("Blend");
    private static readonly int IsLeft = Animator.StringToHash("isLeft");

    [SerializeField] private PlayerMovementController playerMovementController;
    [SerializeField] private PlayerAccelMovementController playerAccelMovementController;
    [SerializeField] private Animator animator;

    public float animStrengthMultiplier = 2.5f;


    private void Init() {
        this.animator.SetFloat(Blend, 0.5f);
    }

    private void Awake() {
        Init();
    }

    private void Update() {
        if (playerMovementController != null && playerMovementController.enabled)
        {
            this.animator.SetBool(IsLeft, this.playerMovementController.PeakDomSide == "Left");
            this.animator.SetFloat(Blend, this.playerMovementController.Propulsion);
        }
        if (playerAccelMovementController != null && playerAccelMovementController.enabled)
        {
            this.animator.SetBool(IsLeft, this.playerMovementController.PeakDomSide == "Left");
            this.animator.SetFloat(Blend, this.playerAccelMovementController.Propulsion);
        }
    }
}