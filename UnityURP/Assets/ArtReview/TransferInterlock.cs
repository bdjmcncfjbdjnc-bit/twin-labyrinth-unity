using UnityEngine;

public sealed class TransferInterlock : MonoBehaviour
{
    public enum Phase { Locked, SendPartner, ReturnControl, CrossInnerGate, Released }
    public ReviewDoor innerGate;
    public ReviewDoor[] outerGates;
    public Transform entryConsole;
    public Transform returnConsole;
    public Transform assemblyPoint;
    public TextMesh status;
    public Phase Current { get; private set; }
    public bool Complete => Current == Phase.Released;
    public float ReturnProgress { get; private set; }
    private MazeSession session;
    private MazeCompanion partner;
    private LastRollCall mission;
    private Transform pause;
    private bool holding;
    private void Awake()
    {
        session = GetComponent<MazeSession>();
        partner = session.PartnerTransform.GetComponent<MazeCompanion>();
        mission = GetComponent<LastRollCall>();
        pause = new GameObject("Partner interlock waiting position").transform;
    }
    public void Begin()
    {
        if (Current != Phase.Locked || !session.ExitUnlocked || !mission.Ready) return;
        innerGate.Unlock();
        innerGate.RequestPassage();
        Current = Phase.SendPartner;
        session.InmateMessage("HOLD F at this panel. Partner must reach the return control.", false);
    }
    public string Instruction => Current switch
    {
        Phase.SendPartner => "HOLD F AT ENTRY PANEL - SEND PARTNER THROUGH INNER GATE",
        Phase.ReturnControl => $"PARTNER OPERATING RETURN CONTROL {ReturnProgress:P0}",
        Phase.CrossInnerGate => "RETURN LATCH OPEN - JOIN YOUR PARTNER INSIDE",
        Phase.Released => "BOTH GATES RELEASED - LEAVE TOGETHER",
        _ => "COMPLETE ESCAPE PREPARATIONS"
    };
    private void Update()
    {
        if (session.IsOver || Current is Phase.Locked or Phase.Released) return;
        if (session.HasDetainee || partner.NeedsHelp) return;
        if (Current == Phase.SendPartner)
        {
            var eyes = GetComponentInChildren<Camera>().transform;
            bool atPanel = Vector3.Distance(transform.position, entryConsole.position) < 3f &&
                Vector3.Dot(eyes.forward, (entryConsole.position - eyes.position).normalized) > .85f;
            bool input = atPanel && PrisonInput.Held(KeyCode.F);
            if (input) GetComponentInChildren<PrisonHands>()?.Hold(entryConsole.position);
            if (input != holding)
            {
                holding = input;
                pause.position = partner.transform.position;
                partner.CooperateAt(input ? assemblyPoint : pause);
            }
            if (Vector3.Distance(partner.transform.position, assemblyPoint.position) < .35f)
            { Current = Phase.ReturnControl; ReturnProgress = 0f; partner.GetComponent<PartnerAction>()?.Operate(returnConsole); }
        }
        else if (Current == Phase.ReturnControl)
        {
            ReturnProgress = Mathf.Min(1f, ReturnProgress + Time.deltaTime / 1.8f);
            if (ReturnProgress >= 1f)
            {
                Current = Phase.CrossInnerGate;
                session.InmateMessage("Partner released the return latch. Cross the inner gate.", false);
                GetComponent<HauntExperience>().RuleCue(false);
            }
        }
        else if (Current == Phase.CrossInnerGate && Vector3.Distance(transform.position, assemblyPoint.position) < 2.1f &&
            Vector3.Distance(partner.transform.position, assemblyPoint.position) < 2f)
        {
            Current = Phase.Released;
            foreach (var gate in outerGates) { gate.Unlock(); gate.RequestPassage(); }
            partner.CooperateAt(null);
            session.InmateMessage("OUTER GATE OPEN. BOTH OF YOU MUST REACH THE EXIT.", false);
            GetComponent<HauntExperience>().RuleCue(false);
        }
        if (status != null) status.text = Current switch
        {
            Phase.SendPartner => "INNER GATE / ACTIVE",
            Phase.ReturnControl => "RETURN CONTROL / WORKING",
            Phase.CrossInnerGate => "RETURN LATCH / OPEN",
            Phase.Released => "OUTER GATE / OPEN",
            _ => "RETURN CONTROL"
        };
    }
    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (session != null && !session.IsOver && Current != Phase.Locked)
            GUI.Box(new Rect((Screen.width - 540) * .5f, Screen.height - 152, 540, 28), Instruction);
    }
}
