using UnityEngine;

public sealed class PrisonRouteBoard : MonoBehaviour
{
    public bool Used { get; private set; }

    public void Read(PrisonGuidance guidance)
    {
        if (Used || guidance == null)
            return;
        Used = true;
        guidance.Reveal();
    }
}
