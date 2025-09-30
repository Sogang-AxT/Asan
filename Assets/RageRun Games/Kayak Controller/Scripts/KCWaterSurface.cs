using UnityEngine;

namespace RageRunGames.KayakController

{
    public class KCWaterSurface : MonoBehaviour
    {
        [SerializeField] float surfaceHeight = 0f;
        [SerializeField] float waveFrequency = 1f;
        [SerializeField] float waveAmplitude = 0.1f;

      
        public float SurfaceHeight
        {
            get { return surfaceHeight + WaveFrequency; }
        }
        
        public float WaveFrequency => Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;


    }
}