using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RageRunGames.KayakController
{
    public class CharacterAnimationEvent : MonoBehaviour
    {
        [SerializeField] private KayakController kayakController;

        [SerializeField] private Transform rightPaddle;
        [SerializeField] private Transform leftPaddle;

        [SerializeField] private ParticleSystem rightParticleSystem;
        [SerializeField] private ParticleSystem leftParticleSystem;


        private Vector3 currentLeftPaddleVelocity;
        private Vector3 currentRightPaddleVelocity;

        private Vector3 previousLeftPaddleVelocity;
        private Vector3 previousRightPaddleVelocity;

        private Vector3 smoothLeftPaddleVelocity;
        private Vector3 smoothRightPaddleVelocity;

        private float velocitySmoothing = 0.1f;

        private void Awake()
        {
            kayakController = GetComponentInParent<KayakController>();
        }

        private void Update()
        {
            Vector3 frameLeftVelocity = (leftPaddle.position - previousLeftPaddleVelocity) / Time.deltaTime;
            smoothLeftPaddleVelocity = Vector3.Lerp(smoothLeftPaddleVelocity, frameLeftVelocity, velocitySmoothing);
            currentLeftPaddleVelocity = smoothLeftPaddleVelocity;
            previousLeftPaddleVelocity = leftPaddle.position;

            
            Vector3 frameRightVelocity = (rightPaddle.position - previousRightPaddleVelocity) / Time.deltaTime;
            smoothRightPaddleVelocity = Vector3.Lerp(smoothRightPaddleVelocity, frameRightVelocity, velocitySmoothing);
            currentLeftPaddleVelocity = smoothRightPaddleVelocity;
            previousRightPaddleVelocity = rightPaddle.position;

        }

        //-- 아래 메서드는 현재 쓰이고 있지 않음. --//
        
        public void ApplyRightPaddleStrikingForce()
        {

            kayakController.PlayOneShot(0.85f);
            
            Collider[] colliders = Physics.OverlapSphere(rightPaddle.position, 5);

            foreach (var col in colliders)
            {
                if (col.transform.CompareTag("Water"))
                {
                   kayakController.ApplyPaddleForce(rightPaddle.position, smoothRightPaddleVelocity);
                }
            }
        }
        
        public void ApplyLeftPaddleStrikingForce()
        {

            kayakController.PlayOneShot(-0.85f);

            Collider[] colliders = Physics.OverlapSphere(leftPaddle.position, 5);

            foreach (var col in colliders)
            {
                if (col.transform.CompareTag("Water"))
                {
                    kayakController.ApplyPaddleForce(leftPaddle.position, currentLeftPaddleVelocity);
                }
            }
        }

        public void EnableLeftPaddleDeformer()
        {
            leftParticleSystem.Play();
        }

        public void DisableLeftPaddleDeformer()
        {
         
        }
        
        public void EnableRightPaddleDeformer()
        {
         
            rightParticleSystem.Play();
        }

        public void DisableRightPaddleDeformer()
        {
          
        }
    }
}