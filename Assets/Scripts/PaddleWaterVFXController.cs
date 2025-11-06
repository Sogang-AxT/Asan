using System;
using UnityEngine;

public class PaddleWaterVFXController : MonoBehaviour {
    [SerializeField] private ParticleSystem paddleWaterParticleSystem;
        
    private bool isInWater;


    private void Update() {
        if (this.isInWater) {
            this.paddleWaterParticleSystem.Play();
        }
    }


    private void OnTriggerEnter(Collider other) {
        if (!isInWater && other.CompareTag("Water")) {
            isInWater = true;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Water") && isInWater) {
            isInWater = false;
        }
    }
}