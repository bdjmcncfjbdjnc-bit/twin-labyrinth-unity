using UnityEngine;

public sealed class PartnerProtocols
{
    public enum Mode { Free, Sweep, Identity, Life, Reckoning }
    public enum Result { Running, Escaped, LockedDown }
    public const float Deadline = 600f;
    public const float AssistTime = 2f;
    private const float RescueStart = 205f;
    private const float RescueEnd = 255f;
    private const float ReckoningStart = 480f;
    private float exposure;
    private float assist;
    private float penalty;
    private bool rescueDone;
    private readonly float deadline;
    private float rescueDelay;
    public float Penalty => penalty;
    public PartnerProtocols(float duration = Deadline) => deadline = duration;

    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, deadline - Elapsed - penalty);
    public float AssistProgress => assist / AssistTime;
    public bool NeedsHelp { get; private set; }
    public float ExposureProgress { get; private set; }
    public Result Outcome { get; private set; }
    public string Alert { get; private set; }
    public void LoseTime(float seconds) => penalty += seconds;

    public Mode Active => Remaining <= Deadline - ReckoningStart ? Mode.Reckoning :
        Elapsed >= RescueStart + rescueDelay && Elapsed < RescueEnd + rescueDelay && !rescueDone ? Mode.Life :
        Elapsed is >= 125f and < RescueStart ? Mode.Identity :
        Elapsed is >= 75f and < 125f ? Mode.Sweep : Mode.Free;

    public float ProtocolRemaining => Mathf.Max(0f, Active switch
    {
        Mode.Sweep => 125f - Elapsed,
        Mode.Identity => RescueStart - Elapsed,
        Mode.Life => RescueEnd + rescueDelay - Elapsed,
        Mode.Reckoning => Remaining,
        _ => Elapsed < 75f ? 75f - Elapsed : Remaining - (Deadline - ReckoningStart)
    });

    public void Step(float seconds, bool torch, bool sprint, bool lookingAtImitator, bool separated,
        bool assisting, bool atExit, bool partnerAtExit, bool securitySensor = true, bool detention = false)
    {
        if (Outcome != Result.Running)
            return;
        Mode previous = Active;
        if (detention && !rescueDone && Elapsed + seconds >= RescueStart + rescueDelay)
            rescueDelay += seconds;
        Elapsed += seconds;
        Alert = null;
        NeedsHelp = Active == Mode.Life;
        if (NeedsHelp)
        {
            assist = assisting ? Mathf.Min(AssistTime, assist + seconds) : 0f;
            if (assist >= AssistTime)
            {
                NeedsHelp = false;
                rescueDone = true;
                Alert = "Partner stabilized. Stay together.";
            }
        }
        else if (previous == Mode.Life && !rescueDone)
        {
            rescueDone = true;
            penalty += 25f;
            Alert = "Partner recovered. Lockdown advanced by 25s.";
        }

        if (previous != Active)
            exposure = 0f;
        float limit = Active switch { Mode.Sweep => 3f, Mode.Identity => 2f, Mode.Reckoning => 8f, _ => 0f };
        bool exposed = Active switch
        {
            Mode.Sweep => securitySensor && (torch || sprint),
            Mode.Identity => lookingAtImitator,
            Mode.Reckoning => separated,
            _ => false
        };
        exposure = exposed ? exposure + seconds : Mathf.Max(0f, exposure - seconds * 2f);
        ExposureProgress = limit > 0f ? exposure / limit : 0f;
        if (limit > 0f && exposure >= limit)
        {
            exposure = ExposureProgress = 0f;
            penalty += 8f;
            Alert = "Detected. Lockdown advanced by 8s.";
        }
        if (Remaining <= 0f)
            Outcome = Result.LockedDown;
        else if (atExit && partnerAtExit && !NeedsHelp)
            Outcome = Result.Escaped;
    }
}
