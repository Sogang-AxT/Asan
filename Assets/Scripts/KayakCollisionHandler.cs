using UnityEngine;
using System.Collections;

public class KayakCollisionHandler : MonoBehaviour
{
    [Header("UI")]
    public GameObject warningUI;                 // Canvas 안의 경고 이미지(오브젝트) 할당

    [Header("Settings")]
    public string obstacleTag = "Obstacle";      // 일반 장애물 태그
    public string hugeObstacleTag = "HugeObstacle"; // 큰 장애물 태그
    public float requiredStaySeconds = 3f;       // 장애물에 닿아있는 시간(초)
    public float delayBeforePush = 2f;           // 경고를 띄운 뒤 이동하기까지 지연(초)
    public float pushDistance = 2f;              // 일반 밀어낼 거리
    public float warningShowSeconds = 1.5f;      // 이동 후 경고 추가 표시 시간
    public float resetXOnHuge = 20f;             // HugeObstacle 충돌 시 월드 X 목표값

    [Header("Detection")]
    public float nearCheckRadius = 0.6f;         // StillNearObstacle()에서 사용하는 반경
    public LayerMask raycastMask = ~0;           // 안전 방향 탐색용 Raycast 마스크(필요 시 조정)

    [Header("Audio")]                            // 경고음 재생
    public AudioSource audioSource;              // 경고음을 재생할 AudioSource (playOnAwake 꺼두기)
    public AudioClip warningClip;                // 경고음 클립
    public bool loopWarning = false;             // 경고 표시 동안 반복 재생할지
    [Range(0f, 1f)]
    public float warningVolume = 1f;             // 경고음 볼륨

    private Rigidbody rb;
    private float stayTimer;
    private bool touching;
    // ★ 추가: 이번 프레임에 HugeObstacle을 접촉했는지
    private bool touchingHuge;
    private bool fired;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            Debug.LogWarning("[KayakObstacleStayHandler] 부모 오브젝트(이 스크립트가 붙은 곳)에 Rigidbody가 필요합니다.");
        }
        else
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    void Start()
    {
        if (warningUI) warningUI.SetActive(false);

        // 안전 설정(선택)
        if (audioSource)
        {
            audioSource.playOnAwake = false;
            // 루프 여부는 실제 재생 시점에 설정함
        }
    }

    void Update()
    {
        if (touching && !fired)
        {
            stayTimer += Time.deltaTime;
            if (stayTimer >= requiredStaySeconds)
            {
                // ★ 이 시점에서 HugeObstacle 접촉 여부를 스냅샷으로 넘긴다
                bool wasHuge = touchingHuge;
                fired = true;
                StartCoroutine(ShowWarningAndNudge(wasHuge));
            }
        }
        else if (!touching)
        {
            stayTimer = 0f;
        }

        // 다음 프레임을 위해 초기화(충돌 콜백에서 다시 true로)
        touching = false;
        touchingHuge = false; // ★ 프레임 단위 초기화
    }

    // ---------- Collision 모드(자식 콜라이더 IsTrigger=Off) ----------
    void OnCollisionStay(Collision c)
    {
        if (HasTagInHierarchy(c.collider.transform, hugeObstacleTag))
        {
            touching = true;
            touchingHuge = true; // ★ HugeObstacle 접촉 기록
        }
        else if (HasTagInHierarchy(c.collider.transform, obstacleTag))
        {
            touching = true;
            // touchingHuge는 그대로(false)
        }
    }

    void OnCollisionExit(Collision c)
    {
        if (HasTagInHierarchy(c.collider.transform, obstacleTag) ||
            HasTagInHierarchy(c.collider.transform, hugeObstacleTag)) // 둘 다 처리
        {
            touching = false;
            touchingHuge = false;
            stayTimer = 0f;
            fired = false;
        }
    }

    // ---------- Trigger 모드(자식 콜라이더 IsTrigger=On) ----------
    void OnTriggerStay(Collider other)
    {
        if (HasTagInHierarchy(other.transform, hugeObstacleTag))
        {
            touching = true;
            touchingHuge = true; // ★ HugeObstacle 접촉 기록
        }
        else if (HasTagInHierarchy(other.transform, obstacleTag))
        {
            touching = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (HasTagInHierarchy(other.transform, obstacleTag) ||
            HasTagInHierarchy(other.transform, hugeObstacleTag)) //  둘 다 처리
        {
            touching = false;
            touchingHuge = false;
            stayTimer = 0f;
            fired = false;
        }
    }

    // isHuge = 이번 발동이 HugeObstacle 때문인가?
    private IEnumerator ShowWarningAndNudge(bool isHuge)
    {
        // UI ON
        if (warningUI) warningUI.SetActive(true);

        // 경고음 재생
        if (audioSource && warningClip)
        {
            if (loopWarning)
            {
                audioSource.Stop(); // 중복 방지
                audioSource.clip = warningClip;
                audioSource.volume = warningVolume;
                audioSource.loop = true;
                audioSource.Play();
            }
            else
            {
                audioSource.PlayOneShot(warningClip, warningVolume);
            }
        }

        // 이동 전 대기(유저가 스스로 빠져나오면 강제 이동 생략됨)
        if (delayBeforePush > 0f)
            yield return new WaitForSeconds(delayBeforePush);

        // 아직도 장애물(일반/큰 돌)에 붙어있거나 매우 가까운가?
        if (StillNearObstacle())
        {
            Vector3 target;

            if (isHuge)
            {
                // ★ HugeObstacle: 월드 X를 resetXOnHuge로 스냅
                Vector3 current = rb ? rb.position : transform.position;
                target = new Vector3(resetXOnHuge, current.y, current.z);
            }
            else
            {
                // 일반 Obstacle: 안전 방향으로 밀어내기
                Vector3 dir = FindSafeDirection();
                if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward; // 비상: 방향을 못 찾으면 뒤로
                Vector3 current = rb ? rb.position : transform.position;
                target = current + dir.normalized * pushDistance;
            }

            if (rb && !rb.isKinematic)
            {
                rb.MovePosition(target);
                // 자연스러운 밀림이 필요하면 AddForce로 교체 가능:
                // rb.AddForce((target - rb.position).normalized * (isHuge ? 4f : pushDistance), ForceMode.VelocityChange);
            }
            else
            {
                transform.position = target;
            }
        }

        // 경고를 조금 더 유지
        if (warningShowSeconds > 0f)
            yield return new WaitForSeconds(warningShowSeconds);

        // UI OFF
        if (warningUI) warningUI.SetActive(false);

        // 루프 중이었다면 정지
        if (audioSource && loopWarning && audioSource.isPlaying && audioSource.clip == warningClip)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }

        fired = false; // 다음에도 조건 충족 시 다시 발동되도록 초기화
    }

    // 자식/부모 어디에 태그가 있어도 잡히도록 위로 타고 올라가며 검사
    private bool HasTagInHierarchy(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.CompareTag(tag)) return true;
            t = t.parent;
        }
        return false;
    }

    // 현재 위치 주변에 Obstacle(일반/큰 돌)이 있는지 간단히 확인
    private bool StillNearObstacle()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, nearCheckRadius);
        foreach (var h in hits)
        {
            if (HasTagInHierarchy(h.transform, obstacleTag) ||
                HasTagInHierarchy(h.transform, hugeObstacleTag)) // ★ 둘 다 체크
                return true;
        }
        return false;
    }

    // 주변 360도 방향으로 짧은 Ray를 쏴서 막히지 않은 쪽(Obstacle이 아닌 쪽)을 선택
    private Vector3 FindSafeDirection()
    {
        const int rays = 12;
        float bestScore = -1f;
        Vector3 best = Vector3.zero;

        for (int i = 0; i < rays; i++)
        {
            float ang = (360f / rays) * i;
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;

            if (!Physics.Raycast(transform.position, dir, out RaycastHit hit, pushDistance + 1f, raycastMask))
            {
                // 완전히 막히지 않은 방향 발견 → 즉시 사용
                return dir;
            }

            // 맞은 것이 Obstacle이면 점수 낮게, 그 외면 히트 거리 기준
            bool isObstacle =
                HasTagInHierarchy(hit.transform, obstacleTag) ||
                HasTagInHierarchy(hit.transform, hugeObstacleTag); // ★ 둘 다 장애물 취급
            float score = isObstacle ? -1f : hit.distance;
            if (score > bestScore)
            {
                bestScore = score;
                best = dir;
            }
        }
        return best; // 완전한 빈 방향이 없으면 그나마 덜 막힌 방향
    }

#if UNITY_EDITOR
    // 에디터에서 근접 체크 반경 가시화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, nearCheckRadius);
    }
#endif
}
