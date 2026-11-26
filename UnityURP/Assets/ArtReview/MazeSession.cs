using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MazeSession : MonoBehaviour
{
    [SerializeField] private Vector3 exit;
    [SerializeField] private ReviewDoor[] exitDoors;
    [SerializeField] private MazeCompanion partner;
    [SerializeField] private MazeGuard guard;
    private float elapsed;
    private bool moving;
    private bool finished;
    private PartnerProtocols protocols;
    private HauntRules rules;
    private CorridorReview controls;
    private HauntExperience haunt;
    private HauntRules.PowerPhase phase;
    private float noticeUntil;
    private float protocolNoticeUntil;
    private string protocolNotice;
    private Transform detainee;
    private bool atHolding;
    private float releaseAt;
    private float rescueProgress;
    private float graceUntil;
    private Vector3 rescueWaypoint;
    private float nextRescueRoute;
    private AudioSource partnerVoice;
    private LastRollCall escape;
    private int captures;
    private int rescues;
    [SerializeField] private AudioClip partnerReply;

    public void Configure(Vector3 destination) => exit = destination;
    public void ConfigureExitDoors(ReviewDoor[] doors) => exitDoors = doors;
    public bool HasRelayObjectives => exitDoors != null && exitDoors.Length != 0;
    public bool HasPartner => partner != null;
    public bool HasGuard => guard != null;
    public bool HasStarted => moving;
    public Vector3 ExitPosition => exit;
    public bool Escaped => protocols != null && protocols.Outcome == PartnerProtocols.Result.Escaped;
    public float ElapsedTime => protocols?.Elapsed ?? elapsed;
    public bool TorchLit => haunt != null && haunt.TorchOn;
    public bool RelayAOn => rules != null && rules.RelayOn(0);
    public float RelayBProgress => rules?.RelayBProgress ?? 0f;
    public Vector3 RouteWaypoint(Vector3 destination) => partner.NextWaypoint(transform.position, destination);
    public Vector3 RouteWaypoint(Vector3 from, Vector3 destination) => partner.NextWaypoint(from, destination);
    public bool ExitUnlocked => rules != null && rules.ExitUnlocked;
    public bool IsSecuritySweep => protocols?.Active == PartnerProtocols.Mode.Sweep;
    public bool CanGuardPursue => detainee == null && Time.time >= graceUntil &&
        protocols != null && protocols.Active != PartnerProtocols.Mode.Life;
    public bool PlayerDetained => detainee == transform;
    public bool HasDetainee => detainee != null;
    public void InmateMessage(string message, bool urgent)
    {
        NotifyProtocol(message);
        if (urgent)
            haunt.RuleCue(true);
    }

    public void InmateContact()
    {
        if (protocols == null || IsOver || HasDetainee)
            return;
        protocols.LoseTime(12f);
        haunt.Stagger();
        InmateMessage("STAGGERED - 12 SECONDS LOST. PUT DISTANCE BETWEEN YOU.", true);
    }
    public bool GuardChasing => guard != null && guard.Current == MazeGuard.State.Chase;
    public Transform GuardTransform => guard != null ? guard.transform : null;
    public void ConfigureGuard(MazeGuard officer, AudioClip reply)
    {
        guard = officer;
        partnerReply = reply;
    }
    public void ReportNoise(Vector3 position, float range, bool decoy = false)
    {
        if (guard == null || !CanGuardPursue)
            return;
        if (!decoy && Physics.Linecast(guard.transform.position + Vector3.up, position + Vector3.up,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            range *= .55f;
        guard.Hear(position, range, decoy);
    }
    public Transform PartnerTransform => HasPartner ? partner.transform : null;
    public bool IsOver => finished || (protocols != null && protocols.Outcome != PartnerProtocols.Result.Running);
    public bool CanAssist => !PlayerDetained && (detainee == null && protocols != null && protocols.NeedsHelp || detainee == PartnerTransform && detainee != null)
        && PartnerNear(transform.position, 2.7f);
    public void ConfigurePartner(MazeCompanion companion) => partner = companion;

    private void Awake()
    {
        escape = GetComponent<LastRollCall>();
        if (!HasRelayObjectives)
            return;
        rules = new HauntRules();
        controls = GetComponent<CorridorReview>();
        haunt = GetComponent<HauntExperience>();
        if (HasPartner)
        {
            protocols = new PartnerProtocols(escape != null ? 1080f : PartnerProtocols.Deadline);
            protocolNotice = "The last transfer never returned. Take the torch, read the roll-call board. TAB: help.";
            protocolNoticeUntil = Time.time + 14f;
            if (guard != null)
            {
                partnerVoice = partner.gameObject.AddComponent<AudioSource>();
                partnerVoice.spatialBlend = 1f;
                partnerVoice.dopplerLevel = 0f;
                partnerVoice.minDistance = 2.5f;
                partnerVoice.maxDistance = 18f;
                partnerVoice.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }

    public bool RelayOn(HauntRelay relay) => rules != null && rules.RelayOn(relay.Index);
    public bool PartnerReady(HauntRelay relay) => partner == null || relay.Index == 0 ||
        (!partner.NeedsHelp && detainee != PartnerTransform && PartnerNear(relay.transform.position, 3f));

    public void UpdateRelayHold(HauntRelay relay, bool held, float seconds)
    {
        if (rules == null || !HasPartner || IsOver)
            return;
        bool valid = relay != null && relay.Index == 1 && !RelayOn(relay) && RelayAOn &&
            !PlayerDetained && !CanAssist && held && PartnerReady(relay);
        if (rules.AdvanceRelayB(seconds, valid))
            Activate(relay);
    }

    public string RelayPrompt(HauntRelay relay)
    {
        if (RelayOn(relay))
            return relay.Label + " ONLINE";
        if (relay.Index != 1 || !HasPartner)
            return "F  Activate " + relay.Label;
        if (!RelayAOn)
            return "RESTORE RELAY A FIRST";
        return PartnerReady(relay) ? $"HOLD F  RELAY B  {RelayBProgress:P0}" : "WAIT FOR YOUR PARTNER";
    }

    public void Activate(HauntRelay relay)
    {
        if (IsOver || rules == null || !PartnerReady(relay) ||
            (HasPartner && relay.Index == 1 && (!RelayAOn || RelayBProgress < 1f)) || !rules.Activate(relay.Index))
            return;
        relay.MarkActive();
        haunt.RuleCue(false);
        noticeUntil = Time.time + 4f;
        if (rules.ExitUnlocked && (escape == null || escape.Ready) && GetComponent<TransferInterlock>() == null)
        {
            foreach (ReviewDoor door in exitDoors)
                door.Unlock();
        }
    }

    public void OpenPreparedExit()
    {
        if (!ExitUnlocked || (escape != null && !escape.Ready)) return;
        if (TryGetComponent<TransferInterlock>(out var interlock)) { interlock.Begin(); return; }
        foreach (ReviewDoor door in exitDoors) door.Unlock();
        InmateMessage("TRANSFER GATE RELEASED - LEAVE TOGETHER", false);
    }

    private void Update()
    {
        if (PrisonInput.Down(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        if (IsOver)
            return;
        if (partnerVoice != null && partner.TakeCommand())
            partnerVoice.PlayOneShot(partnerReply, .48f);
        moving |= GetComponent<CharacterController>().velocity.sqrMagnitude > .04f;
        Vector3 offset = exit - transform.position;
        offset.y = 0;
        bool atExit = offset.sqrMagnitude < .81f;
        if (moving && protocols != null)
            StepProtocols(atExit);
        if (IsOver)
            return;
        if (moving)
            elapsed = protocols?.Elapsed ?? elapsed + Time.deltaTime;
        if (rules != null)
        {
            var current = HauntRules.Phase(elapsed);
            if (current != phase)
            {
                phase = current;
                controls.SetPower(phase != HauntRules.PowerPhase.Blackout);
                haunt.SetBlackout(phase == HauntRules.PowerPhase.Blackout);
                haunt.RuleCue(phase != HauntRules.PowerPhase.Normal);
            }
            bool lightAllowed = phase != HauntRules.PowerPhase.Blackout &&
                (protocols == null || protocols.Active == PartnerProtocols.Mode.Free);
            if (controls.HasFacilityPower != lightAllowed)
            {
                controls.SetPower(lightAllowed);
                haunt.SetBlackout(!lightAllowed);
            }
            if (!rules.ExitUnlocked)
                return;
        }
        if (protocols == null && atExit && PartnerNear(exit, 2.1f))
            finished = true;
    }

    private void StepProtocols(bool atExit)
    {
        if (detainee != null)
            StepDetention();
        var before = protocols.Active;
        bool helping = detainee == null && CanAssist && PrisonInput.Held(KeyCode.F);
        float distance = Vector3.Distance(transform.position, partner.transform.position);
        protocols.Step(Time.deltaTime, haunt.TorchOn, haunt.Sprinting, haunt.LookingAtImitator,
            distance > 9f && detainee == null, helping, atExit && rules.ExitUnlocked && detainee == null && (escape == null || escape.Ready) &&
            (GetComponent<TransferInterlock>() == null || GetComponent<TransferInterlock>().Complete),
            atExit && PartnerNear(exit, 2.1f), guard == null, detainee != null);
        partner.SetDistressed(detainee == PartnerTransform || (detainee == null && protocols.NeedsHelp));
        if (before != protocols.Active || protocols.Alert != null)
        {
            protocolNotice = protocols.Alert ?? ProtocolRule(protocols.Active);
            protocolNoticeUntil = Time.time + 6f;
            haunt.RuleCue(protocols.Alert != null || protocols.Active != PartnerProtocols.Mode.Free);
        }
        if (IsOver)
        {
            bool escaped = protocols.Outcome == PartnerProtocols.Result.Escaped;
            if (!escaped)
                foreach (ReviewDoor door in exitDoors)
                    door.Lock();
            partner.SetRoundOver();
            guard?.StopGuard();
            haunt.EndRound(escaped);
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public bool TryDetain(Transform target)
    {
        if (!CanGuardPursue || (target != transform && target != PartnerTransform))
            return false;
        detainee = target;
        captures++;
        rescueProgress = 0f;
        atHolding = false;
        if (!PlayerDetained)
            partner.SetDistressed(true);
        NotifyProtocol(PlayerDetained ? "You were caught. Your partner is coming." : "Partner caught. Follow the route marker.");
        if (PlayerDetained)
            partner.SetRescueTarget(detainee);
        return true;
    }

    public bool MoveDetainee(Vector3 officerPosition, float seconds)
    {
        bool officerArrived = Vector3.Distance(officerPosition, guard.Holding.position) < 1.25f;
        Vector3 goal = guard.RouteToward(detainee.position, officerArrived ? guard.Holding.position : officerPosition);
        Vector3 difference = goal - detainee.position;
        difference.y = 0f;
        if (difference.sqrMagnitude > .78f * .78f)
            detainee.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(difference, 4.1f * seconds) + Vector3.down * (5f * seconds));
        return officerArrived && Vector3.Distance(detainee.position, guard.Holding.position) < 1.75f;
    }

    public void ArriveAtHolding()
    {
        if (detainee == null || Vector3.Distance(detainee.position, guard.Holding.position) >= 1.75f)
            return;
        atHolding = true;
        releaseAt = Time.time + 33f;
    }

    private void StepDetention()
    {
        if (!PlayerDetained && Time.time >= nextRescueRoute)
        {
            nextRescueRoute = Time.time + .25f;
            rescueWaypoint = guard.RouteToward(transform.position, detainee.position);
        }
        bool nearby = PartnerNear(transform.position, 2.7f);
        bool helping = nearby && (PlayerDetained || PrisonInput.Held(KeyCode.F));
        rescueProgress = helping ? rescueProgress + Time.deltaTime : 0f;
        if (rescueProgress < PartnerProtocols.AssistTime && (!atHolding || Time.time < releaseAt))
            return;
        bool rescued = rescueProgress >= PartnerProtocols.AssistTime;
        ReleaseDetention(rescued, rescued ? "Rescued. Move before the guard returns." : "Released. Lockdown advanced by 24s.");
    }

    public void ReleaseStalledEscort() => ReleaseDetention(false, "Escort interrupted. Released here. Lockdown advanced by 24s.");

    private void ReleaseDetention(bool rescued, string message)
    {
        if (rescued) rescues++;
        if (!rescued)
            protocols.LoseTime(24f);
        detainee = null;
        graceUntil = Time.time + 12f;
        partner.SetRescueTarget(null);
        partner.SetDistressed(false);
        NotifyProtocol(message);
        haunt.RuleCue(!rescued);
    }

    private void NotifyProtocol(string message)
    {
        protocolNotice = message;
        protocolNoticeUntil = Time.time + 6f;
    }

    private static string ProtocolName(PartnerProtocols.Mode mode) => mode switch
    {
        PartnerProtocols.Mode.Sweep => "02  SECURITY SWEEP",
        PartnerProtocols.Mode.Identity => "03  IDENTITY ANOMALY",
        PartnerProtocols.Mode.Life => "04  LIFE ANOMALY",
        PartnerProtocols.Mode.Reckoning => "05  BLOCK RECKONING",
        _ => "01  FREE MOVEMENT"
    };

    private string ProtocolRule(PartnerProtocols.Mode mode) => mode switch
    {
        PartnerProtocols.Mode.Sweep => HasGuard ? "Keep quiet. Break the guard's line of sight." : "Walk with your light off. Light and sprinting cost time.",
        PartnerProtocols.Mode.Identity => "A figure may imitate your partner. Break eye contact.",
        PartnerProtocols.Mode.Life => "Reach your partner and hold F for two seconds.",
        PartnerProtocols.Mode.Reckoning => "Stay within 9 m of your partner. Separation costs time.",
        _ => escape != null && escape.Plan != null ? escape.CurrentGoal : "Take the flashlight. Activate A, bring your partner to B, then leave together."
    };

    private bool PartnerNear(Vector3 point, float radius)
    {
        if (partner == null)
            return true;
        Vector3 delta = partner.transform.position - point;
        delta.y = 0;
        if (delta.sqrMagnitude > radius * radius)
            return false;
        return !Physics.Linecast(partner.transform.position + Vector3.up * .85f, point + Vector3.up * .85f,
                out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            hit.collider.gameObject == gameObject;
    }

    public PartnerProtocols PresentationProtocol => protocols;
    public string PresentationRule => protocols==null?"":ProtocolRule(protocols.Active);
    public string PresentationProtocolName => protocols==null?"":ProtocolName(protocols.Active);
    public string PresentationResult => $"用时 {ElapsedTime/60f:0.0} 分钟    被捕 {captures} 次    救援 {rescues} 次\n违规累计损失 {protocols?.Penalty:0} 秒";
    public string PresentationAlert
    {
        get
        {
            if(detainee!=null)return PlayerDetained?$"已被拘留，队友正在救你 · {rescueProgress/PartnerProtocols.AssistTime:P0}":
                CanAssist?$"按住 F 解救队友 · {rescueProgress/PartnerProtocols.AssistTime:P0}":"救援队友 · "+PrisonChinese.T(RescueDirection());
            if(CanAssist)return $"按住 F 救援队友 · {protocols.AssistProgress:P0}";
            if(GuardChasing)return "狱警正在追捕！关门或绕过转角，躲开他的视线";
            return Time.time<protocolNoticeUntil?protocolNotice:null;
        }
    }
    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (protocols != null)
        {
            DrawProtocolHud();
            return;
        }
        string time = $"{(int)elapsed / 60:00}:{(int)elapsed % 60:00}";
        GUI.Box(new Rect(12, 12, 180, 32), finished ? "EXIT REACHED   " + time : "FIND EXIT   " + time);
        if (rules != null && !finished)
        {
            string relayStatus = $"RELAY A {(rules.RelayOn(0) ? "ON" : "OFF")}   RELAY B {(rules.RelayOn(1) ? "ON" : "OFF")}";
            GUI.Box(new Rect(12, 50, 180, 26), rules.ExitUnlocked ? "EXIT UNLOCKED" : relayStatus);
            if (phase != HauntRules.PowerPhase.Normal)
            {
                string label = phase == HauntRules.PowerPhase.Warning ? "POWER SURGE" : "BLACKOUT";
                GUI.Box(new Rect(12, 82, 180, 26), $"{label}   {HauntRules.Countdown(elapsed, phase)}s");
            }
            if (Time.time < noticeUntil)
                GUI.Box(new Rect(Screen.width * .5f - 124, 12, 248, 28),
                    rules.ExitUnlocked ? "EXIT UNLOCKED - FOLLOW EXIT SIGNS" : "RELAY ONLINE - FIND THE OTHER");
            if (partner != null)
                GUI.Box(new Rect(12, phase == HauntRules.PowerPhase.Normal ? 82 : 114, 220, 26),
                    partner.Following ? "PARTNER FOLLOWING   T Wait" : "PARTNER WAITING   T Call");
            if (rules.ExitUnlocked && (exit - transform.position).sqrMagnitude < 2f && !PartnerNear(exit, 2.1f))
                GUI.Box(new Rect(Screen.width * .5f - 112, 43, 224, 25), "WAIT FOR YOUR PARTNER");
        }
        if (finished)
            GUI.Box(new Rect((Screen.width - 240) / 2f, (Screen.height - 54) / 2f, 240, 54),
                "EXIT REACHED   " + time + "\nPress R to restart");
    }

    private void DrawProtocolHud()
    {
        if (IsOver)
        {
            bool escaped = protocols.Outcome == PartnerProtocols.Result.Escaped;
            GUI.Box(new Rect((Screen.width - 420f) / 2f, (Screen.height - 112f) / 2f, 420f, 112f),
                (escaped ? "YOU ESCAPED TOGETHER" : "FINAL LOCKDOWN\nThe exit is sealed.") +
                $"\nTime {protocols.Elapsed / 60f:0.0} min / Captures {captures} / Rescues {rescues}\nPenalty {protocols.Penalty:0}s\nPress R to restart");
            return;
        }
        var mode = protocols.Active;
        int remaining = Mathf.CeilToInt(protocols.Remaining);
        GUI.Box(new Rect(12, 12, 232, 29), $"LOCKDOWN   {remaining / 60:00}:{remaining % 60:00}");
        GUI.Box(new Rect(12, 45, 232, 28), $"{ProtocolName(mode)}   {Mathf.CeilToInt(protocols.ProtocolRemaining)}s");
        string goal = rules.ExitUnlocked ? (escape != null && !escape.Ready ? "POWER ON - RELEASE TRANSFER GATE" : "EXIT UNLOCKED - GO TOGETHER")
            : $"RELAY A {(rules.RelayOn(0) ? "ON" : "OFF")}   RELAY B {(rules.RelayOn(1) ? "ON" : "OFF")}";
        GUI.Box(new Rect(12, 77, 232, 26), goal);
        float distance = Vector3.Distance(transform.position, partner.transform.position);
        string status = protocols.NeedsHelp ? $"PARTNER NEEDS HELP   {distance:0} m"
            : partner.Following ? $"PARTNER FOLLOWING   {distance:0} m   T Wait"
            : $"PARTNER WAITING   {distance:0} m   T Call";
        GUI.Box(new Rect(12, 107, 232, 26), status);
        if (phase != HauntRules.PowerPhase.Normal)
            GUI.Box(new Rect(12, 137, 232, 26), $"{(phase == HauntRules.PowerPhase.Warning ? "POWER SURGE" : "BLACKOUT")}   {HauntRules.Countdown(elapsed, phase)}s");
        if (protocols.ExposureProgress > 0f)
            GUI.Box(new Rect((Screen.width - 212f) / 2f, 83, 212, 25), $"DETECTION   {protocols.ExposureProgress:P0}");
        string instruction = Time.time < protocolNoticeUntil ? protocolNotice : ProtocolRule(mode);
        if (detainee != null)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(releaseAt - Time.time));
            string clock = atHolding ? $"   {seconds}s" : "";
            instruction = PlayerDetained ? $"DETAINED   AI RESCUE {rescueProgress / PartnerProtocols.AssistTime:P0}{clock}"
                : CanAssist ? $"HOLD F TO FREE PARTNER   {rescueProgress / PartnerProtocols.AssistTime:P0}{clock}"
                : $"RESCUE PARTNER   {RescueDirection()}   {distance:0} m{clock}";
        }
        else if (CanAssist)
            instruction = $"HOLD F TO HELP   {protocols.AssistProgress:P0}";
        else if (GuardChasing)
            instruction = "GUARD IN PURSUIT - BREAK LINE OF SIGHT";
        else if (rules.ExitUnlocked && Vector3.Distance(exit, transform.position) < 1.45f && !PartnerNear(exit, 2.1f))
            instruction = "WAIT FOR YOUR PARTNER";
        float width = Mathf.Min(Screen.width - 24f, 510f);
        GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height - 116f, width, 27f), instruction);
    }

    private string RescueDirection()
    {
        Vector3 heading = transform.InverseTransformDirection(rescueWaypoint - transform.position);
        if (Mathf.Abs(heading.x) > Mathf.Abs(heading.z))
            return heading.x < 0f ? "LEFT" : "RIGHT";
        return heading.z < 0f ? "BEHIND" : "AHEAD";
    }
}
