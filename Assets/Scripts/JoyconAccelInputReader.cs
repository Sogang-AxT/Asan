using UnityEngine;
using System.Collections.Generic;

public class JoyconAccelInputReader : MonoBehaviour
{
    [Header("Input Settings")]
    public JoyconDemo joyconScriptLeft;
    public JoyconDemo joyconScriptRight;
    public bool isInvertedLeft;
    public bool isInvertedRight;

    [Header("Sensor Stabilization")]
    public float cutoffFrequency = 5.0f;
    public float filterSampleRate = 60f;
    public float gravityAdaptSpeedIdle = 2.0f;
    public float gravityAdaptSpeedActive = 0.05f;
    public float motionThreshold = 0.15f;

    [Header("Deadzone Settings")]
    [Range(0f, 0.5f)] public float deadZoneLeft = 0.1f;
    [Range(0f, 0.5f)] public float deadZoneRight = 0.1f;

    public bool autoCalibrateOnStart = true;
    public KeyCode manualCalibrateKeyCode = KeyCode.C;

    // --- 내부 상태 ---
    private AccelButterworthFilter _bwLX, _bwLY, _bwLZ;
    private AccelButterworthFilter _bwRX, _bwRY, _bwRZ;
    private Vector3 _adaptiveGravityL, _adaptiveGravityR;

    // 디버깅 및 출력용 변수
    private float _inputL, _inputR;
    private float _debugRawL, _debugRawR; // 데드존 적용 전 값
    private bool _leftDominant;

    // --- 외부 공개 프로퍼티 ---
    public float InputL => _inputL;
    public float InputR => _inputR;
    public bool LeftDominant => _leftDominant;

    private void Awake()
    {
        InitializeFilters();
    }

    private void Start()
    {
        if (autoCalibrateOnStart) Calibrate();
    }

    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(manualCalibrateKeyCode)) Calibrate();

        ReadSensorInput();
        ApplyDominanceSuppression();
        CheckDominantHand();
    }

    public void Calibrate()
    {
        InitializeFilters();
        if (joyconScriptLeft) _adaptiveGravityL = joyconScriptLeft.orientation * joyconScriptLeft.accel;
        if (joyconScriptRight) _adaptiveGravityR = joyconScriptRight.orientation * joyconScriptRight.accel;

        _inputL = 0;
        _inputR = 0;

        Debug.Log("Accel Calibration Completed");
    }

    private void InitializeFilters()
    {
        _bwLX = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwLY = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwLZ = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRX = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRY = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
        _bwRZ = new AccelButterworthFilter(cutoffFrequency, filterSampleRate);
    }

    private void ReadSensorInput()
    {
        if (!joyconScriptLeft || !joyconScriptRight) return;
        float dt = Time.deltaTime;

        // --- Left Processing ---
        Vector3 rawL = joyconScriptLeft.accel;
        Vector3 filtL = new Vector3(_bwLX.Update(rawL.x), _bwLY.Update(rawL.y), _bwLZ.Update(rawL.z));
        Vector3 worldL = joyconScriptLeft.orientation * filtL;
        if (isInvertedLeft) worldL = -worldL;

        float diffL = (worldL - _adaptiveGravityL).magnitude;
        float adaptL = (diffL > motionThreshold) ? gravityAdaptSpeedActive : gravityAdaptSpeedIdle;
        _adaptiveGravityL = Vector3.Lerp(_adaptiveGravityL, worldL, dt * adaptL);
        float valL = (worldL - _adaptiveGravityL).y;

        // --- Right Processing ---
        Vector3 rawR = joyconScriptRight.accel;
        Vector3 filtR = new Vector3(_bwRX.Update(rawR.x), _bwRY.Update(rawR.y), _bwRZ.Update(rawR.z));
        Vector3 worldR = joyconScriptRight.orientation * filtR;
        if (isInvertedRight) worldR = -worldR;

        float diffR = (worldR - _adaptiveGravityR).magnitude;
        float adaptR = (diffR > motionThreshold) ? gravityAdaptSpeedActive : gravityAdaptSpeedIdle;
        _adaptiveGravityR = Vector3.Lerp(_adaptiveGravityR, worldR, dt * adaptR);
        float valR = (worldR - _adaptiveGravityR).y;

        // 디버깅용 Raw 값 저장
        _debugRawL = Mathf.Abs(valL);
        _debugRawR = Mathf.Abs(valR);

        // 데드존 적용
        if (_debugRawL < deadZoneLeft) valL = 0f;
        if (_debugRawR < deadZoneRight) valR = 0f;

        _inputL = Mathf.Abs(valL);
        _inputR = Mathf.Abs(valR);
    }

    private void ApplyDominanceSuppression()
    {
        if (_inputL > _inputR + 0.2f) _inputR = 0f;
        else if (_inputR > _inputL + 0.2f) _inputL = 0f;
    }

    private void CheckDominantHand()
    {
        _leftDominant = (_inputL >= _inputR);
    }

    private void OnGUI()
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;

        GUI.Box(new Rect(10, 10, 400, 350), "Deadzone Tuner");

        // LEFT
        GUILayout.BeginArea(new Rect(20, 40, 380, 150));
        bool isActiveL = _debugRawL > deadZoneLeft;
        labelStyle.normal.textColor = isActiveL ? Color.green : Color.yellow;
        GUILayout.Label($"LEFT Raw Input: {_debugRawL:F4}", labelStyle);
        GUILayout.Label(isActiveL ? "Status: ACTIVE" : "Status: BLOCKED", labelStyle);

        labelStyle.normal.textColor = Color.white;
        GUILayout.Space(10);
        GUILayout.Label($"Deadzone L: {deadZoneLeft:F3}", labelStyle);
        deadZoneLeft = GUILayout.HorizontalSlider(deadZoneLeft, 0.0f, 0.5f);
        GUILayout.EndArea();

        // RIGHT
        GUILayout.BeginArea(new Rect(20, 180, 380, 150));
        bool isActiveR = _debugRawR > deadZoneRight;
        labelStyle.normal.textColor = isActiveR ? Color.green : Color.yellow;
        GUILayout.Label($"RIGHT Raw Input: {_debugRawR:F4}", labelStyle);
        GUILayout.Label(isActiveR ? "Status: ACTIVE" : "Status: BLOCKED", labelStyle);

        labelStyle.normal.textColor = Color.white;
        GUILayout.Space(10);
        GUILayout.Label($"Deadzone R: {deadZoneRight:F3}", labelStyle);
        deadZoneRight = GUILayout.HorizontalSlider(deadZoneRight, 0.0f, 0.5f);
        GUILayout.EndArea();
    }
}

// 필터 클래스는 InputReader 파일 하단에 같이 두거나 별도 파일로 관리하세요.
public class AccelButterworthFilter
{
    private class Biquad
    {
        private float a0, a1, a2, b1, b2;
        private float[] x = new float[3];
        private float[] y = new float[3];
        public Biquad(float cutoffFreq, float sampleRate)
        {
            float c = Mathf.Tan(Mathf.PI * cutoffFreq / sampleRate);
            float a = 1.0f + Mathf.Sqrt(2.0f) * c + c * c;
            a0 = c * c / a; a1 = 2 * a0; a2 = a0;
            b1 = 2.0f * (c * c - 1.0f) / a; b2 = (1.0f - Mathf.Sqrt(2.0f) * c + c * c) / a;
        }
        public float Update(float input)
        {
            x[0] = input;
            y[0] = a0 * x[0] + a1 * x[1] + a2 * x[2] - b1 * y[1] - b2 * y[2];
            x[2] = x[1]; x[1] = x[0]; y[2] = y[1]; y[1] = y[0];
            if (float.IsNaN(y[0]) || float.IsInfinity(y[0])) y[0] = 0;
            return y[0];
        }
    }
    private List<Biquad> sections = new List<Biquad>();
    public AccelButterworthFilter(float cutoffFreq, float sampleRate, int order = 2)
    {
        order = Mathf.Max(2, order);
        int numSections = Mathf.CeilToInt(order / 2.0f);
        for (int i = 0; i < numSections; i++) sections.Add(new Biquad(cutoffFreq, sampleRate));
    }
    public float Update(float input)
    {
        float output = input;
        foreach (var s in sections) output = s.Update(output);
        return output;
    }
}