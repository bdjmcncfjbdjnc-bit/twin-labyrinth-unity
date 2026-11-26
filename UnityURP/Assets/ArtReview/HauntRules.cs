using UnityEngine;

public sealed class HauntRules
{
    public enum PowerPhase { Normal, Warning, Blackout }
    public const float WarningAt = 56f;
    public const float BlackoutAt = 62f;
    public const float Cycle = 80f;
    private int relays;
    private float hold;
    public const float RelayBHoldTime = 2.2f;
    public float RelayBProgress => hold / RelayBHoldTime;

    public bool AdvanceRelayB(float seconds, bool valid)
    {
        hold = valid ? Mathf.Min(RelayBHoldTime, hold + seconds) : 0f;
        return hold >= RelayBHoldTime;
    }

    public bool ExitUnlocked => relays == 3;
    public bool RelayOn(int index) => (relays & (1 << index)) != 0;

    public bool Activate(int index)
    {
        if (index < 0 || index > 1 || RelayOn(index))
            return false;
        relays |= 1 << index;
        return true;
    }

    public static PowerPhase Phase(float elapsed)
    {
        float time = Mathf.Repeat(elapsed, Cycle);
        return time < WarningAt ? PowerPhase.Normal : time < BlackoutAt ? PowerPhase.Warning : PowerPhase.Blackout;
    }

    public static int Countdown(float elapsed, PowerPhase phase)
    {
        float boundary = phase switch
        {
            PowerPhase.Warning => BlackoutAt,
            PowerPhase.Blackout => Cycle,
            _ => WarningAt
        };
        return Mathf.CeilToInt(boundary - Mathf.Repeat(elapsed, Cycle));
    }
}
