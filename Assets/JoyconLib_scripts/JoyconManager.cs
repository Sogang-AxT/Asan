using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System;

public class JoyconManager : MonoBehaviour
{
    // Settings accessible via Unity
    public bool EnableIMU = true;
    public bool EnableLocalize = true;

    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;
    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;

    public List<Joycon> j; // Array of all connected Joy-Cons
    public Joycon leftJoycon;   // ⬅ 왼쪽 고정 슬롯
    public Joycon rightJoycon;  // ⬅ 오른쪽 고정 슬롯

    static JoyconManager instance;
    public static JoyconManager Instance => instance;

    void Awake()
    {
        if (instance != null) Destroy(gameObject);
        instance = this;

        j = new List<Joycon>();
        bool isLeft = false;

        HIDapi.hid_init();

        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
        IntPtr top_ptr = ptr;

        if (ptr == IntPtr.Zero)
        {
            ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                Debug.Log("No Joy-Cons found!");
            }
        }

        while (ptr != IntPtr.Zero)
        {
            var enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

            if (enumerate.product_id == product_l || enumerate.product_id == product_r)
            {
                if (enumerate.product_id == product_l)
                {
                    isLeft = true;
                    Debug.Log("Left Joy-Con connected.");
                }
                else if (enumerate.product_id == product_r)
                {
                    isLeft = false;
                    Debug.Log("Right Joy-Con connected.");
                }
                else
                {
                    Debug.Log("Non Joy-Con input device skipped.");
                }

                IntPtr handle = HIDapi.hid_open_path(enumerate.path);
                HIDapi.hid_set_nonblocking(handle, 1);

                var jc = new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft);
                j.Add(jc);

                // ⬇ 좌/우 확정 매핑 (연결 순서와 무관)
                if (isLeft) leftJoycon = jc; else rightJoycon = jc;
            }

            ptr = enumerate.next;
        }
        HIDapi.hid_free_enumeration(top_ptr);
    }

    void Start()
    {
        for (int i = 0; i < j.Count; ++i)
        {
            Joycon jc = j[i];

            // LED 패턴: 왼쪽(LED1), 오른쪽(LED4) — 시각 확인용
            byte LEDs = (byte)(jc.isLeft ? 0b0001 : 0b1000);

            jc.Attach(leds_: LEDs);
            jc.Begin();
        }
    }

    void Update()
    {
        for (int i = 0; i < j.Count; ++i)
        {
            j[i].Update();
        }
    }

    void OnApplicationQuit()
    {
        for (int i = 0; i < j.Count; ++i)
        {
            j[i].Detach();
        }
    }
}
