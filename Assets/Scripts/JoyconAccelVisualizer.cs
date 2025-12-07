using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class JoyconAccelVisualizer : MonoBehaviour
{
    private List<Joycon> joycons;
    private Joycon joyconL;
    private Joycon joyconR;

    public LineRenderer leftXLine, leftYLine, leftZLine;
    public LineRenderer rightXLine, rightYLine, rightZLine;

    private int maxPoints = 200;
    private Queue<Vector3> leftAccelQueue = new Queue<Vector3>();
    private Queue<Vector3> rightAccelQueue = new Queue<Vector3>();

    [Header("Live IMU Values (Inspector)")]
    [SerializeField] private Vector3 leftAccelDisplay;
    [SerializeField] private Vector3 leftGyroDisplay;
    [SerializeField] private Vector3 rightAccelDisplay;
    [SerializeField] private Vector3 rightGyroDisplay;

    // ================== 필터 옵션 ==================
    [Header("Filtering Options")]
    public bool useButterworth = false;
    public bool useMovingAverage = false;
    public bool useEMA = false;
    public bool useLerp = false;

    [Range(1f, 10f)] public float butterworthCutoff = 3f;
    [Range(10f, 200f)] public float sampleRate = 100f; // 경우에 따라 Sampling rate 조절하는 칸은 아예 빼도 상관없을듯
    [Range(2, 10)] public int butterworthOrder = 2;
    [Range(1, 50)] public int movingAverageWindow = 10;
    [Range(0.01f, 0.5f)] public float emaAlpha = 0.1f;
    [Range(0.01f, 0.5f)] public float lerpFactor = 0.1f;

    // ---- 가속도용 필터 ----
    private ButterworthFilter filterLX, filterLY, filterLZ;
    private ButterworthFilter filterRX, filterRY, filterRZ;
    private MovingAverageFilter maLX, maLY, maLZ;
    private MovingAverageFilter maRX, maRY, maRZ;
    private Vector3 emaPrevL, emaPrevR;
    private Vector3 lerpPrevL, lerpPrevR;

    // ---- 각속도(Gyro)용 필터 ----
    private ButterworthFilter gFilterLX, gFilterLY, gFilterLZ;
    private ButterworthFilter gFilterRX, gFilterRY, gFilterRZ;
    private MovingAverageFilter gMaLX, gMaLY, gMaLZ;
    private MovingAverageFilter gMaRX, gMaRY, gMaRZ;
    private Vector3 gEmaPrevL, gEmaPrevR;
    private Vector3 gLerpPrevL, gLerpPrevR;

    // ---- 최근 필터 적용 결과 (CSV/Inspector용) ----
    private Vector3 lastFilteredAccelL = Vector3.zero;
    private Vector3 lastFilteredAccelR = Vector3.zero;
    private Vector3 lastFilteredGyroL = Vector3.zero;
    private Vector3 lastFilteredGyroR = Vector3.zero;
    
    public Vector3 LastFilteredAccelL => lastFilteredAccelL;
    public Vector3 LastFilteredAccelR => lastFilteredAccelR;

    public Vector3 LastFilteredGyroL => lastFilteredGyroL;
    public Vector3 LastFilteredGyroR => lastFilteredGyroR;

    // ================== 녹화 데이터 ==================
    private List<string> dataLog = new List<string>();
    private int frameIndex = 0;
    private bool isRecording = false;

    // -------------------------------------------------
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        joycons = JoyconManager.Instance?.j;
        if (joycons == null || joycons.Count < 1)
        {
            Debug.LogError("❌ Joy-Cons not found! Make sure JoyconManager is in the Scene and Joy-Cons are connected.");
            yield break;
        }

        foreach (var j in joycons)
        {
            if (j.isLeft) joyconL = j;
            else joyconR = j;
        }

        InitializeLineRenderer(leftXLine, Color.red);
        InitializeLineRenderer(leftYLine, Color.green);
        InitializeLineRenderer(leftZLine, Color.blue);
        InitializeLineRenderer(rightXLine, Color.red);
        InitializeLineRenderer(rightYLine, Color.green);
        InitializeLineRenderer(rightZLine, Color.blue);

        ResetFilters();
        Debug.Log("✅ JoyconAccelVisualizer initialized. Press 'R' to start or stop recording.");
    }

    void InitializeLineRenderer(LineRenderer lr, Color color)
    {
        if (lr == null) return;
        lr.positionCount = maxPoints;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
    }

    // -------------------------------------------------
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRecording)
                StartRecording();
            else
                StopRecording();
        }

        if (joyconL != null)
            UpdateJoyconData(joyconL, leftAccelQueue, true);
        if (joyconR != null)
            UpdateJoyconData(joyconR, rightAccelQueue, false);

        bool anyFilter = useButterworth || useMovingAverage || useEMA || useLerp;

        // --- Inspector용 실시간 Accel / Gyro 값 계산 ---
        if (joyconL != null)
        {
            Vector3 rawAccL = joyconL.GetAccel();
            Vector3 rawGyroL = joyconL.GetGyro();

            if (anyFilter)
            {
                // 가속도는 UpdateJoyconData에서 이미 필터 적용됨
                leftAccelDisplay = lastFilteredAccelL;

                // Gyro는 여기서 필터 적용
                Vector3 filteredGyroL = ApplyFiltersToGyro(rawGyroL, true);
                leftGyroDisplay = filteredGyroL;
                lastFilteredGyroL = filteredGyroL;
            }
            else
            {
                // 필터 OFF → RAW 표현
                leftAccelDisplay = rawAccL;
                leftGyroDisplay = rawGyroL;

                lastFilteredAccelL = rawAccL;
                lastFilteredGyroL = rawGyroL;
            }
        }

        if (joyconR != null)
        {
            Vector3 rawAccR = joyconR.GetAccel();
            Vector3 rawGyroR = joyconR.GetGyro();

            if (anyFilter)
            {
                rightAccelDisplay = lastFilteredAccelR;

                Vector3 filteredGyroR = ApplyFiltersToGyro(rawGyroR, false);
                rightGyroDisplay = filteredGyroR;
                lastFilteredGyroR = filteredGyroR;
            }
            else
            {
                rightAccelDisplay = rawAccR;
                rightGyroDisplay = rawGyroR;

                lastFilteredAccelR = rawAccR;
                lastFilteredGyroR = rawGyroR;
            }
        }

        // --- 그래프 플롯: 가속도만 ---
        DrawGraph(leftAccelQueue, leftXLine, leftYLine, leftZLine, new Vector3(-3, 0, 0));
        DrawGraph(rightAccelQueue, rightXLine, rightYLine, rightZLine, new Vector3(3, 0, 0));

        // --- CSV 기록 ---
        if (isRecording && joyconL != null && joyconR != null)
        {
            Vector3 l_acc, r_acc;
            Vector3 l_gyro, r_gyro;

            if (anyFilter)
            {
                // 필터 ON → 필터 결과를 저장
                l_acc = lastFilteredAccelL;
                r_acc = lastFilteredAccelR;
                l_gyro = lastFilteredGyroL;
                r_gyro = lastFilteredGyroR;
            }
            else
            {
                // 필터 OFF → RAW 값 저장
                l_acc = joyconL.GetAccel();
                r_acc = joyconR.GetAccel();
                l_gyro = joyconL.GetGyro();
                r_gyro = joyconR.GetGyro();
            }

            string line = string.Format(
                "{0}," +
                "{1:F4},{2:F4},{3:F4}," +     // L_AccX,Y,Z
                "{4:F4},{5:F4},{6:F4}," +     // L_GyroX,Y,Z
                "{7:F4},{8:F4},{9:F4}," +     // R_AccX,Y,Z
                "{10:F4},{11:F4},{12:F4}",    // R_GyroX,Y,Z
                frameIndex,
                l_acc.x, l_acc.y, l_acc.z,
                l_gyro.x, l_gyro.y, l_gyro.z,
                r_acc.x, r_acc.y, r_acc.z,
                r_gyro.x, r_gyro.y, r_gyro.z
            );
            dataLog.Add(line);
        }

        frameIndex++;
    }

    // -------------------------------------------------
    void StartRecording()
    {
        dataLog.Clear();
        dataLog.Add("Frame,L_AccX,L_AccY,L_AccZ,L_GyroX,L_GyroY,L_GyroZ,R_AccX,R_AccY,R_AccZ,R_GyroX,R_GyroY,R_GyroZ");
        frameIndex = 0;
        isRecording = true;
        Debug.Log("🔴 Recording started...");
    }

    // -------------------------------------------------
    void StopRecording()
    {
        isRecording = false;
        SaveDataToCSV();
        Debug.Log("🟢 Recording stopped and saved.");
    }

    // -------------------------------------------------
    void UpdateJoyconData(Joycon jc, Queue<Vector3> queue, bool isLeft)
    {
        Vector3 accel = jc.GetAccel();

        if (float.IsNaN(accel.x) || float.IsNaN(accel.y) || float.IsNaN(accel.z))
            return;

        // Butterworth 필터 (가속도)
        if (useButterworth)
        {
            try
            {
                if (isLeft)
                {
                    accel.x = filterLX.Update(accel.x);
                    accel.y = filterLY.Update(accel.y);
                    accel.z = filterLZ.Update(accel.z);
                }
                else
                {
                    accel.x = filterRX.Update(accel.x);
                    accel.y = filterRY.Update(accel.y);
                    accel.z = filterRZ.Update(accel.z);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Butterworth filter error (Accel): " + ex.Message);
            }
        }

        // 이동 평균 필터 (가속도)
        if (useMovingAverage)
        {
            if (isLeft)
            {
                accel.x = maLX.Update(accel.x);
                accel.y = maLY.Update(accel.y);
                accel.z = maLZ.Update(accel.z);
            }
            else
            {
                accel.x = maRX.Update(accel.x);
                accel.y = maRY.Update(accel.y);
                accel.z = maRZ.Update(accel.z);
            }
        }

        // EMA 필터 (가속도)
        if (useEMA)
        {
            Vector3 prev = isLeft ? emaPrevL : emaPrevR;
            Vector3 updated = prev * (1 - emaAlpha) + accel * emaAlpha;

            if (isLeft) emaPrevL = updated;
            else emaPrevR = updated;

            accel = updated;
        }

        // Lerp 필터 (가속도)
        if (useLerp)
        {
            Vector3 prev = isLeft ? lerpPrevL : lerpPrevR;
            Vector3 updated = Vector3.Lerp(prev, accel, lerpFactor);

            if (isLeft) lerpPrevL = updated;
            else lerpPrevR = updated;

            accel = updated;
        }

        // 최신 필터 결과 저장 (Inspector/CSV용)
        if (isLeft) lastFilteredAccelL = accel;
        else lastFilteredAccelR = accel;

        queue.Enqueue(accel);
        if (queue.Count > maxPoints) queue.Dequeue();
    }

    // -------------------------------------------------
    Vector3 ApplyFiltersToGyro(Vector3 gyro, bool isLeft)
    {
        Vector3 g = gyro;

        // Butterworth (Gyro)
        if (useButterworth)
        {
            try
            {
                if (isLeft)
                {
                    g.x = gFilterLX.Update(g.x);
                    g.y = gFilterLY.Update(g.y);
                    g.z = gFilterLZ.Update(g.z);
                }
                else
                {
                    g.x = gFilterRX.Update(g.x);
                    g.y = gFilterRY.Update(g.y);
                    g.z = gFilterRZ.Update(g.z);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Butterworth filter error (Gyro): " + ex.Message);
            }
        }

        // 이동 평균 (Gyro)
        if (useMovingAverage)
        {
            if (isLeft)
            {
                g.x = gMaLX.Update(g.x);
                g.y = gMaLY.Update(g.y);
                g.z = gMaLZ.Update(g.z);
            }
            else
            {
                g.x = gMaRX.Update(g.x);
                g.y = gMaRY.Update(g.y);
                g.z = gMaRZ.Update(g.z);
            }
        }

        // EMA (Gyro)
        if (useEMA)
        {
            Vector3 prev = isLeft ? gEmaPrevL : gEmaPrevR;
            Vector3 updated = prev * (1 - emaAlpha) + g * emaAlpha;

            if (isLeft) gEmaPrevL = updated;
            else gEmaPrevR = updated;

            g = updated;
        }

        // Lerp (Gyro)
        if (useLerp)
        {
            Vector3 prev = isLeft ? gLerpPrevL : gLerpPrevR;
            Vector3 updated = Vector3.Lerp(prev, g, lerpFactor);

            if (isLeft) gLerpPrevL = updated;
            else gLerpPrevR = updated;

            g = updated;
        }

        return g;
    }

    // -------------------------------------------------
    void DrawGraph(Queue<Vector3> data, LineRenderer xLine, LineRenderer yLine, LineRenderer zLine, Vector3 offset)
    {
        Vector3[] arr = data.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            float xPos = (float)i / maxPoints * 4f;
            Vector3 basePos = offset + new Vector3(xPos, 0, 0);

            if (xLine != null) xLine.SetPosition(i, basePos + new Vector3(0, arr[i].x, 0));
            if (yLine != null) yLine.SetPosition(i, basePos + new Vector3(0, arr[i].y, 0));
            if (zLine != null) zLine.SetPosition(i, basePos + new Vector3(0, arr[i].z, 0));
        }
    }

    // -------------------------------------------------
    void SaveDataToCSV()
    {
        if (dataLog.Count <= 1) return;

        string folderPath = @"C:\Users\shion\Joycon_movement_test_csv\"; // 실행되는 컴퓨터에 맞게 적절하게 경로 수정
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string fileName = "JoyconFullLog_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        string fullPath = Path.Combine(folderPath, fileName);

        File.WriteAllLines(fullPath, dataLog);
        Debug.Log("✅ Joy-Con full data saved to: " + fullPath);
    }

    // -------------------------------------------------
    void ResetFilters()
    {
        // ---- accel용 ----
        filterLX = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        filterLY = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        filterLZ = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        filterRX = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        filterRY = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        filterRZ = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);

        maLX = new MovingAverageFilter(movingAverageWindow);
        maLY = new MovingAverageFilter(movingAverageWindow);
        maLZ = new MovingAverageFilter(movingAverageWindow);
        maRX = new MovingAverageFilter(movingAverageWindow);
        maRY = new MovingAverageFilter(movingAverageWindow);
        maRZ = new MovingAverageFilter(movingAverageWindow);

        emaPrevL = emaPrevR = Vector3.zero;
        lerpPrevL = lerpPrevR = Vector3.zero;

        // ---- gyro용 ----
        gFilterLX = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        gFilterLY = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        gFilterLZ = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        gFilterRX = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        gFilterRY = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);
        gFilterRZ = new ButterworthFilter(butterworthCutoff, sampleRate, butterworthOrder);

        gMaLX = new MovingAverageFilter(movingAverageWindow);
        gMaLY = new MovingAverageFilter(movingAverageWindow);
        gMaLZ = new MovingAverageFilter(movingAverageWindow);
        gMaRX = new MovingAverageFilter(movingAverageWindow);
        gMaRY = new MovingAverageFilter(movingAverageWindow);
        gMaRZ = new MovingAverageFilter(movingAverageWindow);

        gEmaPrevL = gEmaPrevR = Vector3.zero;
        gLerpPrevL = gLerpPrevR = Vector3.zero;

        lastFilteredAccelL = lastFilteredAccelR = Vector3.zero;
        lastFilteredGyroL = lastFilteredGyroR = Vector3.zero;
    }
}

// ================== 필터 클래스 ==================
public class ButterworthFilter
{
    private List<Biquad> sections = new List<Biquad>();

    public ButterworthFilter(float cutoffFreq, float sampleRate, int order = 2)
    {
        order = Mathf.Max(2, order);
        int numSections = Mathf.CeilToInt(order / 2.0f);
        for (int i = 0; i < numSections; i++)
        {
            sections.Add(new Biquad(cutoffFreq, sampleRate));
        }
    }

    public float Update(float input)
    {
        float output = input;
        foreach (var s in sections)
            output = s.Update(output);
        return output;
    }

    private class Biquad
    {
        private float a0, a1, a2, b1, b2;
        private float[] x = new float[3];
        private float[] y = new float[3];

        public Biquad(float cutoffFreq, float sampleRate)
        {
            float c = Mathf.Tan(Mathf.PI * cutoffFreq / sampleRate);
            float a = 1.0f + Mathf.Sqrt(2.0f) * c + c * c;
            a0 = c * c / a;
            a1 = 2 * a0;
            a2 = a0;
            b1 = 2.0f * (c * c - 1.0f) / a;
            b2 = (1.0f - Mathf.Sqrt(2.0f) * c + c * c) / a;
        }

        public float Update(float input)
        {
            x[0] = input;
            y[0] = a0 * x[0] + a1 * x[1] + a2 * x[2] - b1 * y[1] - b2 * y[2];
            x[2] = x[1]; x[1] = x[0];
            y[2] = y[1]; y[1] = y[0];
            if (float.IsNaN(y[0]) || float.IsInfinity(y[0])) y[0] = 0;
            return y[0];
        }
    }
}

public class MovingAverageFilter
{
    private Queue<float> window = new Queue<float>();
    private int windowSize;
    private float sum = 0f;

    public MovingAverageFilter(int size)
    {
        windowSize = size;
    }

    public float Update(float newValue)
    {
        window.Enqueue(newValue);
        sum += newValue;

        if (window.Count > windowSize)
            sum -= window.Dequeue();

        float avg = sum / window.Count;
        if (float.IsNaN(avg) || float.IsInfinity(avg)) avg = 0;
        return avg;
    }
}
