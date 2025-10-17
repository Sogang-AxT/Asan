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
        if (!this.isJoyconBindingChecked) {
            BindingCheck();
        }

        this.stickLeft = this.joyconsTuple.Item1.GetStick();
        this.stickRight = this.joyconsTuple.Item2.GetStick();

        this.gyroLeft = this.joyconsTuple.Item1.GetGyro(); 
        this.gyroRight = this.joyconsTuple.Item2.GetGyro();
        
        this.accelLeft = this.joyconsTuple.Item1.GetAccel();  
        this.accelRight = this.joyconsTuple.Item2.GetAccel();  
        
        this.orientationLeft = this.joyconsTuple.Item1.GetVector();
        this.orientationRight = this.joyconsTuple.Item2.GetVector();
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