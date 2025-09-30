using UnityEngine;

public class JoyconDemo : MonoBehaviour
{
    private Joycon j;                 // 실제로 사용할 단일 Joy-Con
    public bool useLeft = true;       // 이 오브젝트가 왼쪽인지/오른쪽인지 지정

    // Values made available via Unity
    public float[] stick;
    public Vector3 gyro;
    public Vector3 accel;
    public Quaternion orientation;

    void TryBindJoycon()
    {
        var mgr = JoyconManager.Instance;
        if (mgr == null) return;

        // 1순위: 매니저의 고정 슬롯에서 바로 가져오기
        j = useLeft ? mgr.leftJoycon : mgr.rightJoycon;

        // 2순위: 혹시 슬롯이 비었으면 리스트에서 스캔
        if (j == null && mgr.j != null)
        {
            foreach (var cand in mgr.j)
            {
                if (cand != null && cand.isLeft == useLeft)
                {
                    j = cand;
                    break;
                }
            }
        }

        if (j != null)
        {
            Debug.Log($"[{name}] {(useLeft ? "Left" : "Right")} Joy-Con bound.");
        }
    }

    void Start()
    {
        gyro = Vector3.zero;
        accel = Vector3.zero;
        TryBindJoycon();

        if (j == null)
        {
            Debug.LogWarning($"[{name}] {(useLeft ? "Left" : "Right")} Joy-Con not found yet.");
        }
    }

    void Update()
    {
        // 핫플러그/지연 연결 대비: 아직 못 잡았으면 재시도
        if (j == null)
        {
            TryBindJoycon();
            if (j == null) return;
        }

        // 버튼 샘플
        if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
        {
            Debug.Log("Shoulder button 2 pressed");
            Debug.Log(string.Format("Stick x: {0:N} Stick y: {1:N}", j.GetStick()[0], j.GetStick()[1]));
            j.Recenter();
        }
        if (j.GetButtonUp(Joycon.Button.SHOULDER_2))
            Debug.Log("Shoulder button 2 released");

        if (j.GetButton(Joycon.Button.SHOULDER_2))
            Debug.Log("Shoulder button 2 held");

        if (j.GetButtonDown(Joycon.Button.DPAD_DOWN))
        {
            Debug.Log("Rumble");
            j.SetRumble(160, 320, 0.6f, 200);
        }

        // 센서/포즈
        stick = j.GetStick();
        gyro = j.GetGyro();
        accel = j.GetAccel();
        orientation = j.GetVector();

        // 간단한 피드백
        if (j.GetButton(Joycon.Button.DPAD_UP))
            GetComponent<Renderer>().material.color = Color.red;
        else
            GetComponent<Renderer>().material.color = Color.blue;

        transform.rotation = orientation;
        transform.Rotate(90, 0, 0, Space.World);
    }
}
