using UnityEngine;

public sealed class PrisonGuidance : MonoBehaviour
{
    public const float HintDuration = 18f;
    [SerializeField] private MazeSession session;
    [SerializeField] private HauntRelay[] relays;
    [SerializeField] private Transform exit;
    private float revealUntil;
    private float nextRefresh;
    private string hint;

    public void Configure(MazeSession match, HauntRelay[] objectives, Transform destination)
    {
        session = match;
        relays = objectives;
        exit = destination;
    }

    public void Reveal()
    {
        revealUntil = Time.time + HintDuration;
        Refresh();
    }

    private void Update()
    {
        if (!session.IsOver && Time.time < revealUntil && Time.time >= nextRefresh)
            Refresh();
    }

    private void Refresh()
    {
        nextRefresh = Time.time + .2f;
        HauntRelay next = System.Array.Find(relays, relay => !session.RelayOn(relay));
        Transform destination = next != null ? next.transform : exit;
        var escape = GetComponent<LastRollCall>();
        if (escape != null && escape.Plan != null && !escape.Ready && (!escape.Plan.Prepared || session.ExitUnlocked))
            destination = escape.NextTarget ?? destination;
        var airlock = GetComponent<TransferInterlock>();
        if (escape != null && escape.Ready && airlock != null && !airlock.Complete)
            destination = airlock.Current == TransferInterlock.Phase.SendPartner ? airlock.entryConsole : airlock.assemblyPoint;
        if(GetComponent<HauntExperience>() is HauntExperience equipment && !equipment.HasTorch)
        {
            foreach(var pickup in FindObjectsByType<HauntPickup>(FindObjectsSortMode.None))
                if(pickup.Type==HauntPickup.Kind.Torch){destination=pickup.transform;break;}
        }
        Vector3 waypoint = session.RouteWaypoint(destination.position);
        Vector3 heading = transform.InverseTransformDirection(waypoint - transform.position);
        string direction;
        if (heading.sqrMagnitude < .06f)
            direction = Vector3.Distance(destination.position, transform.position) < 3.2f ? "NEARBY" : "PATH TEMPORARILY BLOCKED";
        else if (Mathf.Abs(heading.x) > Mathf.Abs(heading.z))
            direction = heading.x < 0f ? "LEFT" : "RIGHT";
        else
            direction = heading.z < 0f ? "BEHIND" : "AHEAD";
        hint = $"NEXT TASK   {direction}";
    }

    public string PresentationHint => Time.time < revealUntil ? hint : null;
    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (session.IsOver || Time.time >= revealUntil || session.HasDetainee)
            return;
        GUI.Box(new Rect((Screen.width - 360f) * .5f, Screen.height - 184f, 360f, 27f),
            $"{hint}   {Mathf.CeilToInt(revealUntil - Time.time)}s");
    }
}
