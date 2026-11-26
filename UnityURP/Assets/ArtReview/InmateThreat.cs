using UnityEngine;

public sealed class InmateThreat
{
    public enum State { Calm, Warning, Pursuit, Recovering }
    public enum Temperament { Aggressive, Defensive, Feigning }
    private float timer;
    private float exposure;
    private float unseen;
    public const float TerritoryRadius = 10f;
    public const float PursuitDuration = 5f;
    public const float SightGrace = 1.5f;
    private readonly Temperament temperament;

    public InmateThreat(Temperament behavior) => temperament = behavior;
    public State Current { get; private set; }
    public bool KnifeVisible => Current is State.Warning or State.Pursuit;
    public float Range => temperament switch { Temperament.Aggressive => 4.3f, Temperament.Defensive => 2.1f, _ => 3.4f };

    public bool Step(float seconds, float distance, bool visible, bool canThreaten, bool insideTerritory = true)
    {
        timer -= seconds;
        switch (Current)
        {
            case State.Calm:
                exposure = canThreaten && insideTerritory && visible && distance <= Range ? exposure + seconds : Mathf.Max(0, exposure - seconds * 2f);
                float delay = temperament switch { Temperament.Aggressive => .65f, Temperament.Defensive => 1.1f, _ => 2.1f };
                if (exposure >= delay)
                    Change(State.Warning, temperament == Temperament.Aggressive ? 1.05f : 1.45f);
                break;
            case State.Warning:
                if (!canThreaten || !insideTerritory || !visible || distance >= Range + .7f)
                    Change(State.Recovering, 4f);
                else if (timer <= 0f)
                    Change(State.Pursuit, PursuitDuration);
                break;
            case State.Pursuit:
                unseen = visible ? 0f : unseen + seconds;
                bool contact = canThreaten && insideTerritory && visible && timer > 0f && distance <= 1.22f;
                if (contact || !canThreaten || !insideTerritory || unseen >= SightGrace || timer <= 0f)
                    Change(State.Recovering, 8f);
                return contact;
            default:
                if (timer <= 0f)
                    Change(State.Calm, 0f);
                break;
        }
        return false;
    }

    private void Change(State state, float duration)
    {
        Current = state;
        timer = duration;
        exposure = 0f;
        unseen = 0f;
    }
}
