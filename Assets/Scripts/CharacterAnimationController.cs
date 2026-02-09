using UnityEngine;

public class CharacterAnimationController : MonoBehaviour {
    private static readonly int Blend = Animator.StringToHash("Blend");
    private static readonly int IsLeft = Animator.StringToHash("isLeft");

    [SerializeField] private PlayerMovementController playerBoatController;
    [SerializeField] private PlayerAccelBoatController playerAccelBoatController;

    [SerializeField] private Animator animator;

    public float animStrengthMultiplier = 2.5f;


    private void Init() {
        this.animator.SetFloat(Blend, 0.5f);
    }

    private void Awake() {
        Init();
    }

    private void Update() {
        if (playerBoatController != null && playerBoatController.enabled)
        {
            this.animator.SetBool(IsLeft, playerBoatController.LeftDominant);
            this.animator.SetFloat(Blend, playerBoatController.Propulsion);
        }
        else if (playerAccelBoatController != null && playerAccelBoatController.enabled)
        {
            this.animator.SetBool(IsLeft, playerAccelBoatController.LeftDominant);
            this.animator.SetFloat(Blend, playerAccelBoatController.Propulsion);
        }
    }
}