using UnityEngine;

public sealed class PrisonIncidents : MonoBehaviour
{
    public const float SealDuration = 18f;
    public const float FaultDuration = 30f;
    [SerializeField] private MazeSession session;
    [SerializeField] private ReviewDoor[] gates;
    [SerializeField] private PrisonBreaker[] breakers;
    private CorridorReview controls;
    private HauntExperience audioCue;
    private ReviewDoor sealedGate;
    private PrisonBreaker activeBreaker;
    private float sealUntil;
    private float faultUntil;
    private bool sealHappened;
    private bool faultHappened;

    public void Configure(MazeSession game, ReviewDoor[] checkpoints, PrisonBreaker[] panels)
    {
        session = game;
        gates = checkpoints;
        breakers = panels;
    }

    private void Awake()
    {
        controls = GetComponent<CorridorReview>();
        audioCue = GetComponent<HauntExperience>();
    }

    private void Update()
    {
        if (!session.HasStarted || session.IsOver)
            return;
        float clock = Time.time;
        ResolveExpired(clock);

        if (!sealHappened && !session.PlayerDetained && !session.HasDetainee)
        {
            foreach (ReviewDoor gate in gates)
            {
                Vector3 end = gate.transform.position;
                float distance = Vector3.Distance(transform.position, end);
                if (distance < 2.5f || distance > 5.3f ||
                    Vector3.Distance(session.PartnerTransform.position, end) < 2.35f ||
                    (session.GuardTransform != null && Vector3.Distance(session.GuardTransform.position, end) < 2.35f) ||
                    !ClearTo(end - (end - transform.position).normalized * .4f))
                    continue;
                if (TryStartSeal(gate, clock))
                    break;
            }
        }
        if (!faultHappened && !session.PlayerDetained && controls.HasFacilityPower)
        {
            foreach (PrisonBreaker breaker in breakers)
            {
                Vector3 point = breaker.transform.position - breaker.transform.forward * .23f;
                if (Vector3.Distance(transform.position, point) > 6.3f || !ClearTo(point))
                    continue;
                TryStartFault(breaker, clock);
                break;
            }
        }
    }

    private bool ClearTo(Vector3 end) => !Physics.Linecast(transform.position + Vector3.up * 1.15f,
        new Vector3(end.x, transform.position.y + 1.15f, end.z), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

    public bool TryStartSeal(ReviewDoor gate, float clock)
    {
        if (sealHappened || !gate.TrySeal())
            return false;
        sealHappened = true;
        sealedGate = gate;
        sealUntil = clock + SealDuration;
        audioCue.RuleCue(true);
        return true;
    }

    public bool TryStartFault(PrisonBreaker breaker, float clock)
    {
        if (faultHappened)
            return false;
        faultHappened = true;
        activeBreaker = breaker;
        faultUntil = clock + FaultDuration;
        activeBreaker.SetFault(true);
        controls.SetLocalFault(breaker.transform.position);
        audioCue.RuleCue(true);
        return true;
    }

    public bool Repair(PrisonBreaker breaker)
    {
        if (activeBreaker != breaker || activeBreaker == null)
            return false;
        EndFault();
        return true;
    }

    public void ResolveExpired(float clock)
    {
        if (sealedGate != null && clock >= sealUntil)
        {
            sealedGate.LiftSeal();
            sealedGate = null;
        }
        if (activeBreaker != null && clock >= faultUntil)
            EndFault();
    }

    private void EndFault()
    {
        activeBreaker.SetFault(false);
        activeBreaker = null;
        controls.SetLocalFault(null);
        audioCue.RuleCue(false);
    }

    public string PresentationAlert => (sealedGate!=null?$"附近铁门临时封锁 · {Mathf.Max(0,Mathf.CeilToInt(sealUntil-Time.time))} 秒\n":"") +
        (activeBreaker!=null?$"局部跳闸 · {Mathf.Max(0,Mathf.CeilToInt(faultUntil-Time.time))} 秒后恢复，也可按 F 修复":"");
    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (session.IsOver)
            return;
        float x = Screen.width - 270f;
        if (sealedGate != null)
            GUI.Box(new Rect(x, 106, 258f, 25f), $"TEMPORARY SEAL   {Mathf.CeilToInt(sealUntil - Time.time)}s");
        if (activeBreaker != null)
            GUI.Box(new Rect(x, sealedGate == null ? 106 : 136, 258f, 25f),
                $"LOCAL POWER FAULT   {Mathf.CeilToInt(faultUntil - Time.time)}s   F at breaker");
    }
}
