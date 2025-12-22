using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using Unity.VisualScripting;

public class ScoreManager : MonoBehaviour
{
    public GameObject player;
    float distance = 0;

    private readonly List<float> _leftValues = new();
    private readonly List<float> _rightValues = new();

    public EndTrainingUI endUI;
    public bool isFinish = false;
    private bool isEnd = false;

    public PlayerMovementController movement;
    public PlayerAccelMovementController accelMovement;

    [SerializeField] private PlayTimer timer;

    void Start()
    {
        if (player != null)
        {
            movement = player.GetComponent<PlayerMovementController>();
            accelMovement = player.GetComponent<PlayerAccelMovementController>();
        }
    }

    public void RecordStroke(bool leftside, float value)
    {
        if (leftside) _leftValues.Add(value);
        else _rightValues.Add(value);

        // Debug.Log($"[Score] {(leftside ? "L" : "R")} Record: {value:F2}");
    }

    void Update()
    {
        if(isFinish == true && isEnd ==false)
        {
            float distanceM = 0f;

            if (accelMovement != null && accelMovement.enabled)
            {
                distanceM = accelMovement.distanceMeters;
            }
            // 우선순위 2: 기존 무브먼트 컨트롤러가 켜져 있는가?
            else if (movement != null && movement.enabled)
            {
                distanceM = movement.distanceMeters;
            }
            isEnd = true;
            int leftCnt = _leftValues.Count;
            int rightCnt = _rightValues.Count;

            float avgL = (leftCnt > 0) ? _leftValues.Average() : 0f;
            float avgR = (rightCnt > 0) ? _rightValues.Average() : 0f;

            Debug.Log($"[Result] Dist: {distanceM}m, AvgL: {avgL:F1}, AvgR: {avgR:F1}");

            if (endUI != null)
            {
                endUI.Show(timer.ElapsedTime, distanceM, avgL, avgR, leftCnt, rightCnt);
            }

            isEnd = true;
        }
    }
}
