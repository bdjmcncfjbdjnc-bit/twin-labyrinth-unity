using System.Linq;
using UnityEngine;

public sealed class LastRollCall : MonoBehaviour
{
    public EscapeStation[] stations;
    private EscapePlan plan;
    private MazeSession session;
    private Transform eyes;
    private PrisonHands hands;
    private EscapeStation focused;
    private bool wasWorking;
    private GUIStyle taskStyle;
    private float roundTime;
    private bool CanValidate => plan.Selected == EscapePlan.Route.Maintenance || Mathf.Repeat(roundTime, 30f) < 18f;
    public EscapePlan Plan => plan;
    public bool Ready => plan != null && plan.Released;
    public string CurrentGoal => GetComponent<TransferInterlock>() is TransferInterlock gate &&
        gate.Current != TransferInterlock.Phase.Locked && !gate.Complete ? gate.Instruction : plan.Goal;
    public bool OwnsInteraction => focused != null;
    public Transform NextTarget => stations == null || plan == null ? null : stations.FirstOrDefault(s => s.index ==
        (!plan.Briefed ? 0 : !plan.Equipped ? 1 : !plan.Prepared ? 2 : 3))?.transform;
    private void Awake()
    {
        plan = new EscapePlan(RoundPlaytest.RequestedSeed ?? System.Environment.TickCount);
        session = GetComponent<MazeSession>();
        eyes = GetComponentInChildren<Camera>().transform;
        hands = GetComponentInChildren<PrisonHands>();
        foreach (var station in stations) station.Present(plan.Selected);
    }
    private void Update()
    {
        focused = null;
        if (session.HasStarted && !session.IsOver) roundTime += Time.deltaTime;
        if (session.IsOver || session.PlayerDetained || session.CanAssist) { plan.Advance(-1, 0f, false, false, false); return; }
        if (Physics.Raycast(eyes.position, eyes.forward, out var hit, 2.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            focused = hit.collider.GetComponentInParent<EscapeStation>();
        bool near = Vector3.Distance(session.PartnerTransform.position, transform.position) < 3f &&
            !Physics.Linecast(transform.position + Vector3.up, session.PartnerTransform.position + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool held = focused != null && PrisonInput.Held(KeyCode.F);
        if (focused != null && focused.index == 2 && !CanValidate) held = false;
        bool working = held && plan.CanUse(focused.index, session.ExitUnlocked, near);
        if (working && !wasWorking)
        {
            hands?.Interact(focused.transform.position);
            if (focused.index == 4 || (focused.index == 2 && plan.Selected == EscapePlan.Route.Maintenance))
                session.ReportNoise(focused.transform.position, 14f, true);
        }
        wasWorking = working;
        if (working) hands?.Hold(focused.transform.position);
        if (!plan.Advance(focused != null ? focused.index : -1, Time.deltaTime, held, session.ExitUnlocked, near)) return;
        GetComponent<HauntExperience>().RuleCue(false);
        if (focused.index == 4)
        {
            GetComponent<HauntExperience>().GrantCache();
            GetComponent<PrisonGuidance>().Reveal();
            session.InmateMessage("Cache opened. Battery, water and a route note recovered.", false);
        }
        else session.InmateMessage(plan.Goal, false);
        GetComponent<PrisonGuidance>().Reveal();
        if (focused.label != null) focused.label.color = new Color(.45f, .72f, .58f);
        if (plan.Released) session.OpenPreparedExit();
    }
    public string InteractionHint
    {
        get
        {
            if(focused==null)return null;
            int i=focused.index;
            if((i==0&&plan.Briefed)||(i==1&&plan.Equipped)||(i==2&&plan.Prepared)||(i==3&&plan.Released)||(i==4&&plan.OptionalCache))return "已完成";
            if(i==1&&!plan.Briefed)return "先读起点墙上的点名板";
            if(i==2&&!plan.Equipped)return "先去值班室领取物品";
            if(i==3&&!plan.Prepared)return "先完成维修间的准备任务";
            if(i==3&&!session.ExitUnlocked)return "先接通配电 A 和 B";
            if(i==3&&Vector3.Distance(session.PartnerTransform.position,transform.position)>=3f)return "按 T 呼叫队友，让他来到面板旁";
            if(i==2&&!CanValidate)return $"无线电检查中，{30f-Mathf.Repeat(roundTime,30f):0} 秒后可校验";
            return "按住 F  " + PrisonChinese.T(plan.Selected==EscapePlan.Route.Transfer?focused.transferLabel:focused.maintenanceLabel);
        }
    }
    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (plan == null || session.IsOver) return;
        taskStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, padding = new RectOffset(10, 10, 7, 7) };
        GUI.Box(new Rect(Screen.width - 340, 12, 328, 86), "LAST ROLL CALL / " + (plan.Selected == EscapePlan.Route.Transfer ? "TRANSFER" : "MAINTENANCE") + "\n" + CurrentGoal + "\nTab: rules and objectives", taskStyle);
        if (focused != null)
        {
            string text = focused.index == 3 && !session.ExitUnlocked ? "Restore both relays first" :
                "HOLD F   " + (plan.Selected == EscapePlan.Route.Transfer ? focused.transferLabel : focused.maintenanceLabel) + $"   {plan.Progress:P0}";
            if (focused.index == 2 && !CanValidate) text = $"Radio check busy. Validation opens in {30f - Mathf.Repeat(roundTime, 30f):0}s";
            if (focused.index == 3 && session.ExitUnlocked && Vector3.Distance(session.PartnerTransform.position, transform.position) >= 3f)
                text = "Bring your partner beside the release panel";
            if (focused.index == 1 && !plan.Briefed) text = "Read the roll-call board first";
            if (focused.index == 2 && !plan.Equipped) text = "Collect the required item in the duty office first";
            if (focused.index == 3 && !plan.Prepared) text = "Complete the preparation in the workshop first";
            if ((focused.index == 0 && plan.Briefed) || (focused.index == 1 && plan.Equipped) ||
                (focused.index == 2 && plan.Prepared) || (focused.index == 3 && plan.Released) || (focused.index == 4 && plan.OptionalCache))
                text = "COMPLETED";
            GUI.Box(new Rect(Screen.width * .5f - 220, Screen.height * .5f + 56, 440, 28), text);
        }
        if (PrisonInput.Held(KeyCode.Tab))
            GUI.Box(new Rect(Screen.width * .5f - 270, Screen.height * .5f - 150, 540, 260),
                "LAST ROLL CALL\nThe previous transfer never returned. Escape with your cellmate.\n\n" + plan.Goal +
                "\n\nWASD: walk / Shift: sprint / L: flashlight / F: interact\nT: partner follow or wait; listen for the two-note reply\n" +
                "Free: explore. Sweep: stay quiet, break sight.\nIdentity: verify your partner; look away from the imitation.\nLife: hold F beside your partner. Reckoning: stay together.\n\n" +
                "Knives are hidden. Back away from threatening movement.\nDoors and corners break pursuit. Pipes distract the guard.\nCaptured partner: approach and hold F. Progress is retained.");
    }
}
