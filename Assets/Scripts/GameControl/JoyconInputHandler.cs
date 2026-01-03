using UnityEngine;


public class JoyconInputHandler : MonoBehaviour {
    public (Joycon, Joycon) joyconsTuple;
    private JoyconManager joyconManager;
    
    public bool isJoyconBindingChecked;

    public float[] stickLeft;
    public float[] stickRight;
    public Vector3 gyroLeft;
    public Vector3 gyroRight;
    public Vector3 accelLeft;
    public Vector3 accelRight;
    public Quaternion orientationLeft;
    public Quaternion orientationRight;
    
    private void Init() {
        this.isJoyconBindingChecked = false;
        
        this.joyconManager = JoyconManager.Instance;
        this.joyconsTuple.Item1 = this.joyconManager.leftJoycon;
        this.joyconsTuple.Item2 = this.joyconManager.rightJoycon;

        this.gyroLeft = Vector3.zero; this.gyroRight = Vector3.zero;
        this.accelLeft = Vector3.zero; this.accelRight = Vector3.zero;
    }

    private void Start() {
        Init();
    }

    private void Update() {
        if (this.joyconsTuple.Item1 == null || this.joyconsTuple.Item2 == null) {
            return;
        }
        
        if (!this.isJoyconBindingChecked) {
            BindingCheck();
        }

        GetJoyconValue();
    }

    private void GetJoyconValue() {
        // RAW
        this.stickLeft = this.joyconsTuple.Item1.GetStick();
        this.stickRight = this.joyconsTuple.Item2.GetStick();

        this.gyroLeft = this.joyconsTuple.Item1.GetGyro(); 
        this.gyroRight = this.joyconsTuple.Item2.GetGyro();
        
        this.accelLeft = this.joyconsTuple.Item1.GetAccel();  
        this.accelRight = this.joyconsTuple.Item2.GetAccel();  
        
        this.orientationLeft = this.joyconsTuple.Item1.GetVector();
        this.orientationRight = this.joyconsTuple.Item2.GetVector();
    }
    
    // JoyconDemo가 어떤 조이콘을 참조해야 하는지 알려주는 메서드
    public Joycon GetJoycon(bool left) {
        return left ? joyconsTuple.Item1 : joyconsTuple.Item2;
    }
    
    private void BindingCheck() {
        if (this.joyconsTuple.Item1.GetButtonDown(Joycon.Button.PLUS)) {
            (this.joyconsTuple.Item1, this.joyconsTuple.Item2) = (this.joyconsTuple.Item2, this.joyconsTuple.Item1);
            this.isJoyconBindingChecked = true;
            Debug.Log("Swapped");
        }
        else if (this.joyconsTuple.Item2.GetButtonDown(Joycon.Button.MINUS)) {
            (this.joyconsTuple.Item2, this.joyconsTuple.Item1) = (this.joyconsTuple.Item1, this.joyconsTuple.Item2);
            this.isJoyconBindingChecked = true;
            Debug.Log("Swapped");
        }
        else if (this.joyconsTuple.Item1.GetButtonDown(Joycon.Button.MINUS) || 
                 this.joyconsTuple.Item2.GetButtonDown(Joycon.Button.PLUS)) {
            this.isJoyconBindingChecked = true;
            Debug.Log("Checked");
        }
    }
}