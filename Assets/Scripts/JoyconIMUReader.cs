using UnityEngine;

public class JoyconIMUReader : MonoBehaviour
{
    [Header("Joy-Con 선택")]
    public bool useLeftJoycon = true;   // 왼쪽 조이콘 사용
    public bool useRightJoycon = true;  // 오른쪽 조이콘 사용

    [Header("데이터 표시 설정")]
    public bool showDebugLog = true;   // 콘솔에 데이터 출력
    public float updateInterval = 0.5f; // 디버그 로그 업데이트 주기

    [Header("왼쪽 Joy-Con 센서 데이터")]
    [SerializeField] private Vector3 leftGyroscope;      // 자이로스코프
    [SerializeField] private Vector3 leftAccelerometer;  // 가속도계
    [SerializeField] private Vector3 leftEulerAngles;    // 오일러 각도
    [SerializeField] private Quaternion leftOrientation; // 방향

    [Header("오른쪽 Joy-Con 센서 데이터")]
    [SerializeField] private Vector3 rightGyroscope;      // 자이로스코프
    [SerializeField] private Vector3 rightAccelerometer;  // 가속도계
    [SerializeField] private Vector3 rightEulerAngles;    // 오일러 각도
    [SerializeField] private Quaternion rightOrientation; // 방향

    private Joycon leftJoycon;
    private Joycon rightJoycon;
    private float debugTimer;

    void Start()
    {
        // JoyconManager가 준비될 때까지 대기
        if (JoyconManager.Instance == null)
        {
            Debug.LogError("JoyconManager가 씬에 없습니다!");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        // Joy-Con 참조 가져오기
        if (useLeftJoycon && leftJoycon == null)
        {
            leftJoycon = JoyconManager.Instance.leftJoycon;
            if (leftJoycon == null)
            {
                Debug.LogWarning("왼쪽 Joy-Con이 연결되지 않았습니다.");
            }
        }

        if (useRightJoycon && rightJoycon == null)
        {
            rightJoycon = JoyconManager.Instance.rightJoycon;
            if (rightJoycon == null)
            {
                Debug.LogWarning("오른쪽 Joy-Con이 연결되지 않았습니다.");
            }
        }

        // IMU 센서 데이터 읽기
        if (useLeftJoycon && leftJoycon != null)
        {
            ReadLeftIMUData();
        }

        if (useRightJoycon && rightJoycon != null)
        {
            ReadRightIMUData();
        }

        // 디버그 로그 출력
        if (showDebugLog)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer >= updateInterval)
            {
                PrintDebugInfo();
                debugTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 왼쪽 Joy-Con의 IMU 센서 데이터를 읽어옵니다
    /// </summary>
    private void ReadLeftIMUData()
    {
        leftGyroscope = leftJoycon.GetGyro();
        leftAccelerometer = leftJoycon.GetAccel();
        leftOrientation = leftJoycon.GetVector();
        leftEulerAngles = leftOrientation.eulerAngles;
    }

    /// <summary>
    /// 오른쪽 Joy-Con의 IMU 센서 데이터를 읽어옵니다
    /// </summary>
    private void ReadRightIMUData()
    {
        rightGyroscope = rightJoycon.GetGyro();
        rightAccelerometer = rightJoycon.GetAccel();
        rightOrientation = rightJoycon.GetVector();
        rightEulerAngles = rightOrientation.eulerAngles;
    }

    /// <summary>
    /// 센서 데이터를 콘솔에 출력합니다
    /// </summary>
    private void PrintDebugInfo()
    {
        Debug.Log("========== Joy-Con IMU 데이터 ==========");

        if (useLeftJoycon && leftJoycon != null)
        {
            Debug.Log("【왼쪽 Joy-Con】");
            Debug.Log($"  자이로: X={leftGyroscope.x:F2}°/s, Y={leftGyroscope.y:F2}°/s, Z={leftGyroscope.z:F2}°/s");
            Debug.Log($"  가속도: X={leftAccelerometer.x:F2}G, Y={leftAccelerometer.y:F2}G, Z={leftAccelerometer.z:F2}G");
            Debug.Log($"  각도: Pitch={leftEulerAngles.x:F1}°, Yaw={leftEulerAngles.y:F1}°, Roll={leftEulerAngles.z:F1}°");
        }

        if (useRightJoycon && rightJoycon != null)
        {
            Debug.Log("【오른쪽 Joy-Con】");
            Debug.Log($"  자이로: X={rightGyroscope.x:F2}°/s, Y={rightGyroscope.y:F2}°/s, Z={rightGyroscope.z:F2}°/s");
            Debug.Log($"  가속도: X={rightAccelerometer.x:F2}G, Y={rightAccelerometer.y:F2}G, Z={rightAccelerometer.z:F2}G");
            Debug.Log($"  각도: Pitch={rightEulerAngles.x:F1}°, Yaw={rightEulerAngles.y:F1}°, Roll={rightEulerAngles.z:F1}°");
        }

        Debug.Log("======================================");
    }

    // ========== 왼쪽 Joy-Con 접근 메서드 ==========

    public Vector3 GetLeftGyroscope() => leftGyroscope;
    public Vector3 GetLeftAccelerometer() => leftAccelerometer;
    public Quaternion GetLeftOrientation() => leftOrientation;
    public Vector3 GetLeftEulerAngles() => leftEulerAngles;
    public bool IsLeftConnected() => leftJoycon != null;

    public bool GetLeftButton(Joycon.Button button)
    {
        if (leftJoycon == null) return false;
        return leftJoycon.GetButton(button);
    }

    public bool GetLeftButtonDown(Joycon.Button button)
    {
        if (leftJoycon == null) return false;
        return leftJoycon.GetButtonDown(button);
    }

    public bool GetLeftButtonUp(Joycon.Button button)
    {
        if (leftJoycon == null) return false;
        return leftJoycon.GetButtonUp(button);
    }

    public Vector2 GetLeftStick()
    {
        if (leftJoycon == null) return Vector2.zero;
        float[] stick = leftJoycon.GetStick();
        return new Vector2(stick[0], stick[1]);
    }

    // ========== 오른쪽 Joy-Con 접근 메서드 ==========

    public Vector3 GetRightGyroscope() => rightGyroscope;
    public Vector3 GetRightAccelerometer() => rightAccelerometer;
    public Quaternion GetRightOrientation() => rightOrientation;
    public Vector3 GetRightEulerAngles() => rightEulerAngles;
    public bool IsRightConnected() => rightJoycon != null;

    public bool GetRightButton(Joycon.Button button)
    {
        if (rightJoycon == null) return false;
        return rightJoycon.GetButton(button);
    }

    public bool GetRightButtonDown(Joycon.Button button)
    {
        if (rightJoycon == null) return false;
        return rightJoycon.GetButtonDown(button);
    }

    public bool GetRightButtonUp(Joycon.Button button)
    {
        if (rightJoycon == null) return false;
        return rightJoycon.GetButtonUp(button);
    }

    public Vector2 GetRightStick()
    {
        if (rightJoycon == null) return Vector2.zero;
        float[] stick = rightJoycon.GetStick();
        return new Vector2(stick[0], stick[1]);
    }

    // ========== 화면 표시 (OnGUI) ==========

    void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        int yPos = 10;

        if (useLeftJoycon && leftJoycon != null)
        {
            GUI.Label(new Rect(10, yPos, 400, 30), "【왼쪽 Joy-Con】", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"자이로: ({leftGyroscope.x:F1}, {leftGyroscope.y:F1}, {leftGyroscope.z:F1})", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"가속도: ({leftAccelerometer.x:F2}, {leftAccelerometer.y:F2}, {leftAccelerometer.z:F2})", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"각도: ({leftEulerAngles.x:F0}°, {leftEulerAngles.y:F0}°, {leftEulerAngles.z:F0}°)", style);
            yPos += 35;
        }

        if (useRightJoycon && rightJoycon != null)
        {
            GUI.Label(new Rect(10, yPos, 400, 30), "【오른쪽 Joy-Con】", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"자이로: ({rightGyroscope.x:F1}, {rightGyroscope.y:F1}, {rightGyroscope.z:F1})", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"가속도: ({rightAccelerometer.x:F2}, {rightAccelerometer.y:F2}, {rightAccelerometer.z:F2})", style);
            yPos += 25;
            GUI.Label(new Rect(10, yPos, 400, 30),
                $"각도: ({rightEulerAngles.x:F0}°, {rightEulerAngles.y:F0}°, {rightEulerAngles.z:F0}°)", style);
        }
    }
}