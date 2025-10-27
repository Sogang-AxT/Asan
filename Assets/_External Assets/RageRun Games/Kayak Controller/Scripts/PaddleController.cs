using System;
using UnityEngine;

namespace RageRunGames.KayakController
{
    public class PaddleController : MonoBehaviour
    {
        public KayakController kayak;

        [Header("Settings")] public float velocitySmoothing = 0.1f;

        private Vector3 previousPosition;
        private Vector3 smoothedVelocity;
        private Vector3 currentVelocity;
        private bool isInWater;


        private void Awake()
        {
            previousPosition = transform.position;
        }

        private void Update()
        {
            CalculateVelocity();
        }

        private void CalculateVelocity()
        {
            Vector3 frameVelocity = (transform.position - previousPosition) / Time.deltaTime;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, frameVelocity, velocitySmoothing);
            currentVelocity = smoothedVelocity;
            previousPosition = transform.position;
        }

        private void ApplyStrikeForce()
        {
            kayak.ApplyPaddleForce(transform.position, currentVelocity);
        }


        private void OnTriggerEnter(Collider other)
        {
            if (!isInWater && other.CompareTag("Water"))
            {
                isInWater = true;
                kayak.IsPaddleInWater = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Water") && isInWater)
            {
                if (kayak.ForceOn != ForceOn.AnimationEvent)
                {
                    ApplyStrikeForce();
                }
                
                kayak.IsPaddleInWater = false;
                isInWater = false;
            }
        }

    }
}