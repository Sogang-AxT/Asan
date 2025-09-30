using System.Linq;
using UnityEngine;
using RageRunGames.KayakController;

public class FinishBoxTrigger : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] private string kayakTag = "Kayak";

    [Header("UI")]
    [SerializeField] private GameObject finishImageUI;
    private EndTrainingUI endUI;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip finishClip;

    [Header("Background Music")]
    [SerializeField] private AudioSource bgmAudioSource;

    [Header("Timer (optional)")]
    [SerializeField] private PlayTimer timer;

    // 명시 참조(인스펙터에 플레이어의 컴포넌트를 넣어두면 가장 확실)
    [Header("Stats Sources (optional, drop your player here)")]
    [SerializeField] private PaddlePoseDriver poseSource;
    [SerializeField] private KayakController kayakSource;

    private bool triggered = false;

    void Awake()
    {
        if (finishImageUI) endUI = finishImageUI.GetComponent<EndTrainingUI>();
        if (!timer) timer = FindObjectOfType<PlayTimer>(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(kayakTag)) return;
        triggered = true;

        if (!endUI)  { Debug.LogWarning("[Finish] EndTrainingUI missing"); return; }
        if (!timer)  { Debug.LogWarning("[Finish] PlayTimer missing");   return; }

        // ── 1) 통계 소스 결정 ────────────────────────────────────────────────
        var pose  = poseSource;
        var kayak = kayakSource;

        // A. 트리거로 들어온 쪽에서 먼저 찾기
        if (!pose)  pose  = other.GetComponentInParent<PaddlePoseDriver>(true);
        if (!kayak) kayak = other.GetComponentInParent<KayakController>(true);

        // B. 그래도 없으면 씬 전체에서 후보 모아서 가장 유력한 것 선택
        if (!pose)
        {
            var candidates = FindObjectsOfType<PaddlePoseDriver>(true)
                            .OrderByDescending(p => p.PaddleCount) // 카운트가 큰 = 실제 플레이어 가능성↑
                            .ThenByDescending(p => p.DistanceMeters)
                            .ToArray();
            pose = candidates.FirstOrDefault(p => p.isActiveAndEnabled) ?? candidates.FirstOrDefault();
        }
        if (!kayak)
        {
            var kcands = FindObjectsOfType<KayakController>(true)
                        .OrderByDescending(k => k.PaddleCount)
                        .ThenByDescending(k => k.DistanceMeters)
                        .ToArray();
            kayak = kcands.FirstOrDefault(k => k.isActiveAndEnabled) ?? kcands.FirstOrDefault();
        }

        // ── 2) 값 읽기 ─────────────────────────────────────────────────────
        float distanceM = 0f;
        int   paddleCnt = 0;
        float avgL = 0f, avgR = 0f;
        int   leftCnt = 0, rightCnt = 0;

        if (pose)
        {
            distanceM = pose.DistanceMeters;
            //paddleCnt = pose.PaddleCount;
            avgL = pose.AvgAngleLeftDeg;
            avgR = pose.AvgAngleRightDeg;
            leftCnt  = pose.LeftStrokeCount;   
            rightCnt = pose.RightStrokeCount;
            Debug.Log($"[Finish] Stats from PaddlePoseDriver ({pose.name}): dist={distanceM}, cnt={paddleCnt}, L={avgL:F1}, R={avgR:F1}");
        }
        else if (kayak)
        {
            // 백업: KayakController만 있을 때
            distanceM = kayak.DistanceMeters;
            paddleCnt = kayak.PaddleCount;
            Debug.Log($"[Finish] Stats from KayakController ({kayak.name}): dist={distanceM}, cnt={paddleCnt}");
        }
        else
        {
            Debug.LogWarning("[Finish] No stats provider found (both PaddlePoseDriver and KayakController missing).");
        }

        // ── 3) 타이머/사운드/표시 ───────────────────────────────────────────
        timer.Stop();

        if (bgmAudioSource) bgmAudioSource.Stop();

        // SFX는 UI 띄우기 전에 (AudioListener.pause의 영향을 피하려면 ignoreListenerPause=true 사용 가능)
        if (audioSource && finishClip)
        {
            // audioSource.ignoreListenerPause = true; // 전체 일시정지 해도 SFX 들리게 하려면
            audioSource.PlayOneShot(finishClip);
        }

        endUI.Show(timer.ElapsedTime, distanceM, avgL, avgR, leftCnt, rightCnt);

        GameStarter.GameStarted = false;
    }
}
