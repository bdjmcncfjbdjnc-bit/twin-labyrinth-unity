using UnityEngine;

// Pure round state, independent from input and rendering.
public sealed class EscapePlan
{
    public enum Route { Transfer, Maintenance }
    public Route Selected { get; }
    public bool Briefed { get; private set; }
    public bool Equipped { get; private set; }
    public bool Prepared { get; private set; }
    public bool Released { get; private set; }
    public bool OptionalCache { get; private set; }
    public float Progress { get; private set; }
    private int holding = -1;
    public EscapePlan(int seed) => Selected = (Route)new System.Random(seed).Next(2);
    public string Goal => !Briefed ? "Read the roll-call board in A"
        : !Equipped ? Selected == Route.Transfer ? "Duty office: collect the transfer pass" : "Duty office: collect the service tool"
        : !Prepared ? Selected == Route.Transfer ? "Workshop: validate the pass during a radio gap" : "Workshop: close the steam valve (hold F)"
        : !Released ? "Restore A, then B together; release the transfer gate" : "Exit together - do not leave your partner";
    public bool CanUse(int step, bool powered, bool partner) => step switch
    {
        0 => !Briefed,
        1 => Briefed && !Equipped,
        2 => Equipped && !Prepared,
        3 => Prepared && powered && partner && !Released,
        4 => !OptionalCache,
        _ => false
    };
    public bool Advance(int step, float dt, bool held, bool powered, bool partner)
    {
        if (!held || !CanUse(step, powered, partner)) { Progress = 0f; holding = -1; return false; }
        if (holding != step) { Progress = 0f; holding = step; }
        float duration = step == 2 ? 3f : step == 3 ? 2.5f : step == 4 ? 2f : .35f;
        Progress = Mathf.Min(1f, Progress + Mathf.Max(0f, dt) / duration);
        if (Progress < 1f) return false;
        switch (step)
        {
            case 0: Briefed = true; break;
            case 1: Equipped = true; break;
            case 2: Prepared = true; break;
            case 3: Released = true; break;
            case 4: OptionalCache = true; break;
        }
        Progress = 0f;
        holding = -1;
        return true;
    }
}
