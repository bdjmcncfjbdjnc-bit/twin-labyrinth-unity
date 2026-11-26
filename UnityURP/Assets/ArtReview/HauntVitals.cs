using UnityEngine;

public sealed class HauntVitals
{
    public float Stamina { get; private set; } = 1f;
    public float Charge { get; private set; }
    public bool HasTorch { get; private set; }
    public bool TorchOn { get; private set; }
    public bool Sprinting { get; private set; }
    public bool OutOfBreath { get; private set; }
    public bool CanSprint => !exhausted && Stamina > 0f;
    private bool exhausted;
    private float recoveryDelay;

    public void Stagger()
    {
        Stamina = 0f;
        exhausted = true;
        OutOfBreath = true;
        Sprinting = false;
        recoveryDelay = 1.45f;
    }

    public void Step(float seconds, bool moving, bool sprintHeld)
    {
        if (!sprintHeld && Stamina >= .24f)
            exhausted = false;
        Sprinting = moving && sprintHeld && !exhausted && Stamina > 0f;
        if (Sprinting)
        {
            Stamina = Mathf.Max(0f, Stamina - .155f * seconds);
            recoveryDelay = .8f;
            if (Stamina == 0f)
            {
                exhausted = true;
                OutOfBreath = true;
                Sprinting = false;
            }
        }
        else
        {
            recoveryDelay = Mathf.Max(0f, recoveryDelay - seconds);
            if (recoveryDelay == 0f)
                Stamina = Mathf.Min(1f, Stamina + (moving ? .10f : .19f) * seconds);
        }
        if (Stamina >= .32f)
            OutOfBreath = false;

        if (TorchOn)
        {
            Charge = Mathf.Max(0f, Charge - seconds / 425f);
            TorchOn = Charge > 0f;
        }
    }

    public bool ToggleTorch()
    {
        if (!HasTorch || Charge == 0f)
            return false;
        TorchOn = !TorchOn;
        return true;
    }

    public bool Collect(HauntPickup.Kind kind)
    {
        switch (kind)
        {
            case HauntPickup.Kind.Torch:
                if (HasTorch)
                    return false;
                HasTorch = TorchOn = true;
                Charge = 1f;
                return true;
            case HauntPickup.Kind.Batteries:
                if (!HasTorch || Charge > .97f)
                    return false;
                Charge = Mathf.Min(1f, Charge + .58f);
                return true;
            case HauntPickup.Kind.Water:
                if (Stamina > .97f)
                    return false;
                Stamina = Mathf.Min(1f, Stamina + .65f);
                exhausted = false;
                OutOfBreath = false;
                recoveryDelay = 0f;
                return true;
            default:
                return false;
        }
    }
}
