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

    private readonly List<float> _leftAngles = new();
    private readonly List<float> _rightAngles = new();

    public EndTrainingUI endUI;
    public bool isFinish = false;
    private bool isEnd = false;

    public PlayerMovementController movement;

    [SerializeField] private PlayTimer timer;

    void Start()
    {
        movement = movement.GetComponent<PlayerMovementController>();
    }

    public void RecordStroke(bool leftside, float angle)
    {
        if (leftside) 
        {
            _leftAngles.Add(angle);
        }
        else
        {
            _rightAngles.Add(angle);
        }
        Debug.Log($"[Score] add {(leftside ? "L" : "R")} angle={angle:F1}¡Æ  |  Lcnt={_leftAngles.Count}, Rcnt={_rightAngles.Count}");
    }

    void Update()
    {
        if(isFinish == true && isEnd ==false)
        {
            float distanceM = movement.distanceMeters;
            int leftCnt = movement.LegStrokeCountLeft;
            int rightCnt = movement.LegStrokeCountRight;
            float avgL = (_leftAngles.Count > 0) ? _leftAngles.Average() : 0f;
            float avgR = (_rightAngles.Count > 0) ? _rightAngles.Average() : 0f;
            Debug.Log("avgL" + avgL + " / avgR" + avgR);
            endUI.Show(timer.ElapsedTime, distanceM, avgL, avgR, leftCnt, rightCnt);

            isEnd = true;
        }
    }
}
