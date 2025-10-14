using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxRotationAngleSource : MonoBehaviour
{
    [Header("Read from this transform (your rotating box)")]
    public Transform target;          // ← 좌/우 박스 Transform
    public bool useLocal = true;      // 박스가 로컬로 도는 구조면 체크
    public enum Axis { X, Y, Z }
    public Axis axis = Axis.X;

    [Header("Calibration")]
    public bool autoCalibrateOnStart = true;
    public KeyCode recalibrateKey = KeyCode.C;
    public bool invert = false;       // 박스가 반대로 도는 경우 체크
    public float baselineDeg;         // 기준 자세(초기각)

    public float CurrentXDegSigned { get; private set; }  // 읽기 전용 (–180~180)
    public float DeltaFromBaseline  { get; private set; }  // 기준 대비 변화량(deg)

    void Start()
    {
        if (autoCalibrateOnStart) Calibrate();
    }

    public void Calibrate()
    {
        baselineDeg = ReadSignedDeg();
    }

    void Update()
    {
        if (Input.GetKeyDown(recalibrateKey)) Calibrate();

        CurrentXDegSigned = ReadSignedDeg();
        float delta = CurrentXDegSigned - baselineDeg;
        if (invert) delta = -delta;
        DeltaFromBaseline = delta;
    }

    float ReadSignedDeg()
    {
        Vector3 e = useLocal ? target.localEulerAngles : target.eulerAngles;
        float v = axis == Axis.X ? e.x : axis == Axis.Y ? e.y : e.z;
        v %= 360f; if (v > 180f) v -= 360f;   // 0~360 → -180~180
        return v;
    }
}

