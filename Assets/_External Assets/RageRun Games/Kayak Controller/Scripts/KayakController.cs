using System.Collections;
using UnityEngine;
using TMPro;

namespace RageRunGames.KayakController
{
    public enum ForceOn
    {
        WaterTrigger,
        AnimationEvent
    }
    
    
    //-- 해당 클래스는 현재 쓰이고 있지 않음. (체크 해제 상태) --//
    [RequireComponent(typeof(Rigidbody))]
    public class KayakController : MonoBehaviour
    {
        // === 애니메이션 타이머 ===
        [SerializeField] private float smallStrokeAnimDuration = 0.5f;
        private float smallLeftAnimTimer = 0f;
        private float smallRightAnimTimer = 0f;
        private float fullLeftAnimTimer = 0f;
        private float fullRightAnimTimer = 0f;
        [SerializeField] private float fullStrokeAnimDuration = 0.5f;

        [Header("Force Settings")]
        [SerializeField] private ForceOn forceOn;
        [SerializeField] private bool useExternalPluginBuoyancyForces;
        [SerializeField] private bool useExternalPluginDragForces;

        [Header("Water Force Settings")]
        [SerializeField] private bool enableWaterForceOnKayak;
        [SerializeField] private float waterForceMultiplier;
        [SerializeField] private Vector3 waterForceDirection;

        [Header("Paddle Settings")]
        [SerializeField] private Transform paddleParent;

        [Header("Physics Settings")]
        [SerializeField] private float forwardStrokeForce = 12f;
        [SerializeField] private float maxVelocity = 6f;
        [SerializeField] private float maxAngularVelocity = 5f;
        [SerializeField] private float dragInWater = 1.5f;
        [SerializeField] private float angularDragInWater = 3f;
        [SerializeField] private float stability = 10f;
        [SerializeField] private float turningTorque = 6f;
        [SerializeField] private float drawStrokeForce = 15f;

        [Header("Steering Settings")]
        [SerializeField] private float steerTorqueMultiplier = 2f;

        [Header("Buoyancy Settings")]
        [SerializeField] private Transform[] buoyancyPoints;

        [SerializeField] private KCWaterSurface waterSurface;

        [Header("Visual Leaning")]
        [SerializeField] private Transform visualModel;
        [SerializeField] private float leanAmount = 10f;
        [SerializeField] private float leanSpeed = 5f;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] audioClips;

        // ====== 키보드(풀) ======
        [SerializeField] private float keyStrokeForwardForce = 7f;   // 홀드 지속 전진력
        [SerializeField] private float keyStrokeTurnTorque   = 0.5f;
        [SerializeField] private float keyStrokeImpulse      = 9f;   // 탭 임펄스(풀)

        private bool keyLeftForward;   // M 홀드(왼쪽)
        private bool keyRightForward;  // N 홀드(오른쪽)
        private bool pendingLeftImpulse;
        private bool pendingRightImpulse;

        // ====== 키보드(스몰) ======
        [SerializeField] private float smallKeyStrokeForwardForce = 3.5f;
        [SerializeField] private float smallKeyStrokeTurnTorque   = 0.6f;
        [SerializeField] private float smallKeyStrokeImpulse      = 4f;
        [SerializeField] private float smallLegRotateAngle        = 20f;

        private bool keyLeftSmallForward;    // B 홀드(왼쪽)
        private bool keyRightSmallForward;   // V 홀드(오른쪽)
        private bool pendingLeftSmallImpulse;
        private bool pendingRightSmallImpulse;

        // ====== 조이콘 전용: 홀드 상태 & 드리프트 ======
        [SerializeField] private float holdSideDriftForce       = 1.0f; // 풀 홀드 사이드 드리프트
        [SerializeField] private float smallHoldSideDriftForce  = 0.5f; // 스몰 홀드 사이드 드리프트
        [SerializeField] private float tapSideDriftImpulse      = 1.0f; // 풀 탭 사이드 임펄스
        [SerializeField] private float smallTapSideDriftImpulse = 1.0f; // 스몰 탭 사이드 임펄스

        private bool joyHoldLeftFull, joyHoldRightFull, joyHoldLeftSmall, joyHoldRightSmall;

        // ====== UI ======
        [SerializeField] private TextMeshProUGUI distanceText;
        private float distanceMeters = 0f;

        [SerializeField] private TextMeshProUGUI paddleCountText;
        private int paddleCount = 0;
        private bool leftStrokeTriggered = false;
        private bool rightStrokeTriggered = false;

        // --- Leg rigs ---
        [SerializeField] private Transform leftLegRig;
        [SerializeField] private Transform rightLegRig;
        [SerializeField] private Vector3 legRotateAxis = Vector3.forward;
        [SerializeField] private float legRotateAngle = 40f;
        [SerializeField] private float legRotateHalfDuration = 2.2f;
        private bool leftLegBusy = false;
        private bool rightLegBusy = false;

        private Animator animator;
        private Rigidbody rb;

        private bool isLeftDrawStroking;
        private bool isRightDrawStroking;
        private bool isLeftRudderStroking;
        private bool isRightRudderStroking;

        private float drawStrokeAmount;
        private Vector3 glideVelocity;
        private float glideDecay = 0.95f;

        private float vertical;
        private float horizontal;

        public bool IsPaddleInWater { get; set; } = false;
        public ForceOn ForceOn => forceOn;
        
// ====== 조이콘 탭 Yaw 임펄스 ======
[SerializeField] private float tapYawImpulse = 2.0f;        // 풀 탭 회전 임펄스
[SerializeField] private float smallTapYawImpulse = 1.0f;   // 스몰 탭 회전 임펄스


        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>();
            if (!waterSurface)
                waterSurface = FindObjectOfType<KCWaterSurface>();
        }

        // === 조이콘 홀드용 공개 API ===
        public void JoyHoldLeft(bool on, bool small)
        {
            if (small) joyHoldLeftSmall = on;
            else       joyHoldLeftFull  = on;
        }
        public void JoyHoldRight(bool on, bool small)
        {
            if (small) joyHoldRightSmall = on;
            else       joyHoldRightFull  = on;
        }

        private void Update()
        {
            if (!GameStarter.GameStarted) return;

            vertical = Input.GetAxisRaw("Vertical");
            horizontal = Input.GetAxisRaw("Horizontal");

            isLeftDrawStroking  = Input.GetKey(KeyCode.Q);
            isRightDrawStroking = Input.GetKey(KeyCode.E);

            // 키보드 홀드 입력
            keyLeftForward        = Input.GetKey(KeyCode.M); // LEFT full
            keyRightForward       = Input.GetKey(KeyCode.N); // RIGHT full
            keyLeftSmallForward   = Input.GetKey(KeyCode.B); // LEFT small
            keyRightSmallForward  = Input.GetKey(KeyCode.V); // RIGHT small

            bool isForward  = vertical > 0.1f;
            bool isReverse  = vertical < -0.1f;
            bool isRight    = horizontal > 0.1f;
            bool isLeft     = horizontal < -0.1f;

            // 애니메이터 초기화
            animator.SetBool("LeftForwardStroking",  false);
            animator.SetBool("RightForwardStroking", false);
            animator.SetBool("LeftReverseStroking",  false);
            animator.SetBool("RightReverseStroking", false);
            animator.SetBool("ForwardStroking", false);
            animator.SetBool("ReverseStroking", false);
            animator.SetBool("LeftSweepStroking",  false);
            animator.SetBool("RightSweepStroking", false);
            animator.SetBool("LeftDrawStroking",  false);
            animator.SetBool("RightDrawStroking", false);
            animator.SetBool("LeftSmallForwardStroke",  false);
            animator.SetBool("RightSmallForwardStroke", false);

            if (smallLeftAnimTimer > 0f)  smallLeftAnimTimer  -= Time.deltaTime;
            if (smallRightAnimTimer > 0f) smallRightAnimTimer -= Time.deltaTime;
            if (fullLeftAnimTimer > 0f)   fullLeftAnimTimer   -= Time.deltaTime;
            if (fullRightAnimTimer > 0f)  fullRightAnimTimer  -= Time.deltaTime;

            // 우선순위: 키보드 풀 → 키보드 스몰 → (조이콘/탭 타이머 유지)
            if (keyLeftForward)
                animator.SetBool("LeftForwardStroking", true);
            else if (keyRightForward)
                animator.SetBool("RightForwardStroking", true);
            else if (keyLeftSmallForward)
                animator.SetBool("LeftSmallForwardStroke", true);
            else if (keyRightSmallForward)
                animator.SetBool("RightSmallForwardStroke", true);
            else if (smallLeftAnimTimer > 0f)
                animator.SetBool("LeftSmallForwardStroke", true);
            else if (smallRightAnimTimer > 0f)
                animator.SetBool("RightSmallForwardStroke", true);

            if (fullLeftAnimTimer > 0f)  animator.SetBool("LeftForwardStroking", true);
            if (fullRightAnimTimer > 0f) animator.SetBool("RightForwardStroking", true);

            // 추가 키 조합(선택)
            else if (isForward && isRight)
            {
                animator.SetBool("LeftForwardStroking", true);
            }
            else if (isForward && isLeft)
            {
                animator.SetBool("RightForwardStroking", true);
            }
            else if (isReverse && isRight)
            {
                animator.SetBool("LeftReverseStroking", true);
            }
            else if (isReverse && isLeft)
            {
                animator.SetBool("RightReverseStroking", true);
            }
            else
            {
                animator.SetBool("ForwardStroking",  isForward  && !isLeftDrawStroking && !isRightDrawStroking);
                animator.SetBool("ReverseStroking",  isReverse  && !isLeftDrawStroking && !isRightDrawStroking);
                animator.SetBool("LeftSweepStroking",  isRight  && !isLeftDrawStroking && !isRightDrawStroking);
                animator.SetBool("RightSweepStroking", isLeft   && !isLeftDrawStroking && !isRightDrawStroking);

                animator.SetBool("LeftDrawStroking",  isLeftDrawStroking);
                animator.SetBool("RightDrawStroking", isRightDrawStroking);
            }

            if (!isLeftDrawStroking && !isRightDrawStroking)
                drawStrokeAmount = 0f;

            // 카운트 (Q/E)
            if (Input.GetKeyDown(KeyCode.Q) && !leftStrokeTriggered)
            {
                IncreasePaddleCount();
                leftStrokeTriggered = true;
            }
            if (Input.GetKeyUp(KeyCode.Q)) leftStrokeTriggered = false;

            if (Input.GetKeyDown(KeyCode.E) && !rightStrokeTriggered)
            {
                IncreasePaddleCount();
                rightStrokeTriggered = true;
            }
            if (Input.GetKeyUp(KeyCode.E)) rightStrokeTriggered = false;

            // 키보드 풀 탭(N/M)
            if (Input.GetKeyDown(KeyCode.M) && !leftStrokeTriggered)
            {
                IncreasePaddleCount();
                leftStrokeTriggered  = true;
                distanceMeters += 3f;
                if (distanceText) distanceText.text = $"{distanceMeters:0} m";
                pendingLeftImpulse  = true;
                // 왼쪽으로 사이드 임펄스
                rb.AddForce(-transform.right * tapSideDriftImpulse, ForceMode.Impulse);
            }
            if (Input.GetKeyUp(KeyCode.M)) leftStrokeTriggered = false;

            if (Input.GetKeyDown(KeyCode.N) && !rightStrokeTriggered)
            {
                IncreasePaddleCount();
                rightStrokeTriggered = true;
                distanceMeters += 3f;
                if (distanceText) distanceText.text = $"{distanceMeters:0} m";
                pendingRightImpulse = true;
                // 오른쪽으로 사이드 임펄스
                rb.AddForce(transform.right * tapSideDriftImpulse, ForceMode.Impulse);
            }
            if (Input.GetKeyUp(KeyCode.N)) rightStrokeTriggered = false;

            // 키보드 스몰 탭(V/B)
            if (Input.GetKeyDown(KeyCode.B) && !leftStrokeTriggered)
            {
                IncreasePaddleCount();
                leftStrokeTriggered = true;
                distanceMeters += 2f;
                if (distanceText) distanceText.text = $"{distanceMeters:0} m";
                pendingLeftSmallImpulse = true;
                rb.AddForce(-transform.right * smallTapSideDriftImpulse, ForceMode.Impulse);
            }
            if (Input.GetKeyUp(KeyCode.B)) leftStrokeTriggered = false;

            if (Input.GetKeyDown(KeyCode.V) && !rightStrokeTriggered)
            {
                IncreasePaddleCount();
                rightStrokeTriggered = true;
                distanceMeters += 2f;
                if (distanceText) distanceText.text = $"{distanceMeters:0} m";
                pendingRightSmallImpulse = true;
                rb.AddForce(transform.right * smallTapSideDriftImpulse, ForceMode.Impulse);
            }
            if (Input.GetKeyUp(KeyCode.V)) rightStrokeTriggered = false;

            // 다리 리그
            if (Input.GetKeyDown(KeyCode.M) && leftLegRig && !leftLegBusy)
                StartCoroutine(RotateLegPingPong(leftLegRig, true,  legRotateAngle));
            if (Input.GetKeyDown(KeyCode.N) && rightLegRig && !rightLegBusy)
                StartCoroutine(RotateLegPingPong(rightLegRig, false, legRotateAngle));
            if (Input.GetKeyDown(KeyCode.V) && rightLegRig && !rightLegBusy)
                StartCoroutine(RotateLegPingPong(rightLegRig, false, smallLegRotateAngle));
            if (Input.GetKeyDown(KeyCode.B) && leftLegRig && !leftLegBusy)
                StartCoroutine(RotateLegPingPong(leftLegRig, true,  smallLegRotateAngle));
        }

        private void FixedUpdate()
        {
            if (!useExternalPluginBuoyancyForces) ApplyBuoyancy();
            ApplyWaterDrag();
            StabilizeKayak();
            if (enableWaterForceOnKayak) AddWaterForce();

            float targetLeanAngle = -horizontal * leanAmount;
            if (isLeftDrawStroking)       targetLeanAngle =  leanAmount * 1.125f;
            else if (isRightDrawStroking) targetLeanAngle = -leanAmount * 1.125f;

            // 키보드 홀드 기울기
            if (keyLeftForward)       targetLeanAngle =  leanAmount * 0.6f;
            else if (keyRightForward) targetLeanAngle = -leanAmount * 0.6f;
            if (keyLeftSmallForward)       targetLeanAngle =  leanAmount * 0.35f;
            else if (keyRightSmallForward) targetLeanAngle = -leanAmount * 0.35f;

            // 파도에 의한 미세 흔들림
            if (Mathf.Abs(vertical) > 0f && Mathf.Approximately(horizontal, 0f) && waterSurface)
            {
                float waveFrequency = waterSurface.WaveFrequency;
                targetLeanAngle = waveFrequency * -leanAmount * 0.2f;
            }

            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetLeanAngle);
            visualModel.localRotation =
                Quaternion.Slerp(visualModel.localRotation, targetRotation, Time.deltaTime * leanSpeed);

            if (glideVelocity.magnitude > 0.01f)
            {
                rb.AddForce(glideVelocity, ForceMode.Force);
                glideVelocity *= glideDecay;
            }

            if (rb.velocity.magnitude > maxVelocity)
                rb.velocity = rb.velocity.normalized * maxVelocity;

            if (rb.angularVelocity.magnitude > maxAngularVelocity)
                rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;

            if (IsPaddleInWater) drawStrokeAmount = drawStrokeForce;

            if (isLeftDrawStroking)
            {
                drawStrokeAmount -= Time.fixedDeltaTime * 15f;
                rb.AddForce(-transform.right * drawStrokeAmount);
            }
            if (isRightDrawStroking)
            {
                drawStrokeAmount -= Time.fixedDeltaTime * 15f;
                rb.AddForce(transform.right * drawStrokeAmount);
            }

            // === 키보드 홀드: 전진 + 회전 ===
            if (keyLeftForward)
            {
                rb.AddForce(transform.forward * keyStrokeForwardForce, ForceMode.Force);
                rb.AddTorque(Vector3.up * keyStrokeTurnTorque,        ForceMode.Force); // LEFT → 오른쪽 회전(-Y)
            }
            if (keyRightForward)
            {
                rb.AddForce(transform.forward * keyStrokeForwardForce, ForceMode.Force);
                rb.AddTorque(Vector3.up *  -keyStrokeTurnTorque,        ForceMode.Force); // RIGHT → 왼쪽 회전(+Y)
            }
            if (keyLeftSmallForward)
            {
                rb.AddForce(transform.forward * smallKeyStrokeForwardForce, ForceMode.Force);
                rb.AddTorque(Vector3.up * smallKeyStrokeTurnTorque,        ForceMode.Force);
            }
            if (keyRightSmallForward)
            {
                rb.AddForce(transform.forward * smallKeyStrokeForwardForce, ForceMode.Force);
                rb.AddTorque(Vector3.up *  -smallKeyStrokeTurnTorque,        ForceMode.Force);
            }

            // === 조이콘 홀드: 전진 + 회전 + 사이드 드리프트 ===
            if (joyHoldLeftFull)
            {
                rb.AddForce(transform.forward * keyStrokeForwardForce,  ForceMode.Force);
                rb.AddTorque(Vector3.up * -keyStrokeTurnTorque,         ForceMode.Force);
                rb.AddForce(-transform.right * holdSideDriftForce,      ForceMode.Force);
            }
            if (joyHoldRightFull)
            {
                rb.AddForce(transform.forward * keyStrokeForwardForce,  ForceMode.Force);
                rb.AddTorque(Vector3.up *  keyStrokeTurnTorque,         ForceMode.Force);
                rb.AddForce( transform.right * holdSideDriftForce,      ForceMode.Force);
            }
            if (joyHoldLeftSmall)
            {
                rb.AddForce(transform.forward * smallKeyStrokeForwardForce,  ForceMode.Force);
                rb.AddTorque(Vector3.up * -smallKeyStrokeTurnTorque,         ForceMode.Force);
                rb.AddForce(-transform.right * smallHoldSideDriftForce,      ForceMode.Force);
            }
            if (joyHoldRightSmall)
            {
                rb.AddForce(transform.forward * smallKeyStrokeForwardForce,  ForceMode.Force);
                rb.AddTorque(Vector3.up *  smallKeyStrokeTurnTorque,         ForceMode.Force);
                rb.AddForce( transform.right * smallHoldSideDriftForce,      ForceMode.Force);
            }

            // 탭 임펄스(풀/스몰)는 Update에서 pending* 로 세팅됨 → 여기서는 전진 임펄스만 처리
            if (pendingLeftImpulse)
            {
                rb.AddForce(transform.forward * keyStrokeImpulse, ForceMode.Impulse);
                pendingLeftImpulse = false;
            }
            if (pendingRightImpulse)
            {
                rb.AddForce(transform.forward * keyStrokeImpulse, ForceMode.Impulse);
                pendingRightImpulse = false;
            }
            if (pendingLeftSmallImpulse)
            {
                rb.AddForce(transform.forward * smallKeyStrokeImpulse, ForceMode.Impulse);
                pendingLeftSmallImpulse = false;
            }
            if (pendingRightSmallImpulse)
            {
                rb.AddForce(transform.forward * smallKeyStrokeImpulse, ForceMode.Impulse);
                pendingRightSmallImpulse = false;
            }
        }

        private IEnumerator RotateLegPingPong(Transform target, bool isLeft, float angleDeg)
        {
            if (target == null) yield break;

            if (isLeft) leftLegBusy = true; else rightLegBusy = true;

            Quaternion start = target.localRotation;
            Quaternion toMinus = start * Quaternion.AngleAxis(-angleDeg, legRotateAxis.normalized);

            yield return LerpLocalRotation(target, start, toMinus, legRotateHalfDuration);
            yield return LerpLocalRotation(target, toMinus, start, legRotateHalfDuration);

            if (isLeft) leftLegBusy = false; else rightLegBusy = false;
        }

        public void JoySmallRightTap()
        {
            IncreasePaddleCount();
            distanceMeters += 2f;
            if (distanceText) distanceText.text = $"{distanceMeters:0} m";
            pendingRightSmallImpulse = true;

            // 오른쪽으로 사이드 임펄스
            rb.AddTorque(Vector3.up * -tapYawImpulse, ForceMode.Impulse);

            if (rightLegRig && !rightLegBusy)
                StartCoroutine(RotateLegPingPong(rightLegRig, false, smallLegRotateAngle));

            smallRightAnimTimer = smallStrokeAnimDuration;
            if (audioSource) PlayOneShot(+0.2f);
        }

        public void JoySmallLeftTap()
        {
            IncreasePaddleCount();
            distanceMeters += 2f;
            if (distanceText) distanceText.text = $"{distanceMeters:0} m";
            pendingLeftSmallImpulse = true;

             // === 회전 임펄스 추가(왼쪽 젓기) ===
             rb.AddTorque(Vector3.up * tapYawImpulse, ForceMode.Impulse);

            if (leftLegRig && !leftLegBusy)
                StartCoroutine(RotateLegPingPong(leftLegRig, true, smallLegRotateAngle));

            smallLeftAnimTimer = smallStrokeAnimDuration;
            if (audioSource) PlayOneShot(-0.2f);
        }

        public void JoyFullLeftTap()
        {
            IncreasePaddleCount();
            distanceMeters += 3f;
            if (distanceText) distanceText.text = $"{distanceMeters:0} m";
            pendingLeftImpulse = true;

             // === 회전 임펄스 추가(왼쪽 젓기) ===
             rb.AddTorque(Vector3.up * tapYawImpulse, ForceMode.Impulse);

            if (leftLegRig && !leftLegBusy)
                StartCoroutine(RotateLegPingPong(leftLegRig, true, legRotateAngle));

            fullLeftAnimTimer = fullStrokeAnimDuration;
            if (audioSource) PlayOneShot(-0.2f);
        }

        public void JoyFullRightTap()
        {
            IncreasePaddleCount();
            distanceMeters += 3f;
            if (distanceText) distanceText.text = $"{distanceMeters:0} m";
            pendingRightImpulse = true;

            // 오른쪽으로 사이드 임펄스
            rb.AddTorque(Vector3.up * -tapYawImpulse, ForceMode.Impulse);

            if (rightLegRig && !rightLegBusy)
                StartCoroutine(RotateLegPingPong(rightLegRig, false, legRotateAngle));

            fullRightAnimTimer = fullStrokeAnimDuration;
            if (audioSource) PlayOneShot(+0.2f);
        }

        private IEnumerator LerpLocalRotation(Transform t, Quaternion a, Quaternion b, float duration)
        {
            float tElapsed = 0f;
            while (tElapsed < duration)
            {
                tElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(tElapsed / duration);
                t.localRotation = Quaternion.Slerp(a, b, u);
                yield return null;
            }
            t.localRotation = b;
        }

        private void IncreasePaddleCount()
        {
            paddleCount++;
            if (paddleCountText) paddleCountText.text = $"x {paddleCount}";
        }

        public void ApplyPaddleForce(Vector3 hitPoint, Vector3 paddleVelocity)
        {
            if (isRightDrawStroking || isLeftDrawStroking) return;

            if (Mathf.Abs(vertical) > 0.1f && horizontal == 0f)
            {
                Vector3 localPoint = transform.InverseTransformPoint(hitPoint);
                float sideInfluence = Mathf.Clamp(localPoint.x, -1f, 1f);
                rb.AddTorque(Vector3.up * sideInfluence * turningTorque, ForceMode.Force);
            }

            Vector3 projectedForce = transform.forward * Mathf.Clamp(paddleVelocity.magnitude, 0, forwardStrokeForce) * vertical;
            glideVelocity = projectedForce * 0.5f;

            if (Mathf.Abs(horizontal) < 0.1f)
            {
                rb.AddForce(transform.forward * forwardStrokeForce * 0.1f * vertical);
            }
            else
            {
                rb.AddForce(transform.forward * forwardStrokeForce * 0.05f * vertical);
                rb.AddTorque(Vector3.up * steerTorqueMultiplier * turningTorque * -horizontal, ForceMode.Force);
            }
        }

        private void ApplyBuoyancy()
        {
            if (buoyancyPoints == null || buoyancyPoints.Length == 0)
            {
                ApplySimpleBuoyancy();
                return;
            }

            foreach (Transform point in buoyancyPoints)
            {
                float depth = waterSurface.SurfaceHeight - point.position.y;
                if (depth > 0f)
                    rb.AddForceAtPosition(Vector3.up * depth * 9.81f, point.position, ForceMode.Acceleration);
            }
        }

        private void ApplySimpleBuoyancy()
        {
            float submergedAmount = Mathf.Max(0f, waterSurface.SurfaceHeight - transform.position.y);
            rb.AddForce(Vector3.up * submergedAmount * 9.81f, ForceMode.Acceleration);
        }

        public void AddWaterForce()
        {
            rb.AddForce(new Vector3(waterForceDirection.x, 0f, waterForceDirection.z) * waterForceMultiplier, ForceMode.Acceleration);
        }

        private void ApplyWaterDrag()
        {
            if (useExternalPluginDragForces) return;

            float speedFactor = rb.velocity.magnitude;
            rb.drag = dragInWater + speedFactor * 0.05f;
            rb.angularDrag = angularDragInWater + rb.angularVelocity.magnitude * 0.025f;

            Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
            localVelocity.x *= 0.8f; // 옆으로 미끄러짐 약간 억제
            rb.velocity = transform.TransformDirection(localVelocity);
        }

        private void StabilizeKayak()
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt > 5f)
            {
                Vector3 correctionTorque = Vector3.Cross(transform.up, Vector3.up) * stability;
                rb.AddTorque(correctionTorque - rb.angularVelocity * 0.1f, ForceMode.Acceleration);
            }
        }

        public void PlayOneShot(float stereoPan = 0f)
        {
            if (!audioSource || audioClips == null || audioClips.Length == 0) return;
            audioSource.panStereo = stereoPan;
            audioSource.pitch = Random.Range(0.85f, 1.15f);
            audioSource.volume = Random.Range(0.8f, 1f);
            audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
        }
        //END에서 읽을 데이터 open
        public int PaddleCount => paddleCount;
        public float DistanceMeters => distanceMeters;

        public void ResetStats()
        {
            distanceMeters = 0f;
            paddleCount = 0;
            if (distanceText) distanceText.text = "0 m";
            if (paddleCountText) paddleCountText.text = "x 0";
        }

    }
    
}


