using UnityEngine;

public sealed class PrisonInmateRoster : MonoBehaviour
{
    [SerializeField] private PrisonInmate[] inmates;
    [SerializeField] private MazeSession session;
    [SerializeField] private Transform eyes;
    private PrisonInmate target;
    private int clueIndex;

    public void Configure(MazeSession match, Transform view, PrisonInmate[] cast)
    {
        session = match;
        eyes = view;
        inmates = cast;
    }

    public static int SelectArmed(int count, System.Random random) => random.Next(count);

    private void Awake()
    {
        var random = new System.Random();
        int armed = SelectArmed(inmates.Length, random);
        for (int i = 0; i < inmates.Length; i++)
            inmates[i].BeginRound(session, i == armed, (InmateThreat.Temperament)random.Next(3));
    }

    public string Prompt => target != null && target.CanTalk ? "F  Speak to inmate" : null;

    public bool TrySpeak()
    {
        if (target == null || !target.CanTalk || session.PlayerDetained || session.IsOver)
            return false;
        string[] clues =
        {
            "Inmate: Two relays open the exit. Bring your partner to the second.",
            "Inmate: The guard follows noise. The pipes can draw him off.",
            "Inmate: If someone reaches for their pocket, back away."
        };
        if (target.Talk(clues[clueIndex % clues.Length]))
            clueIndex++;
        return true;
    }

    private void Update()
    {
        target = null;
        float nearest = 2.8f * 2.8f;
        foreach (PrisonInmate actor in inmates)
        {
            Vector3 point = actor.transform.position + Vector3.up * 1.42f;
            Vector3 delta = point - eyes.position;
            if (delta.sqrMagnitude >= nearest || Vector3.Dot(eyes.forward, delta.normalized) < .86f ||
                Physics.Linecast(eyes.position, point, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;
            nearest = delta.sqrMagnitude;
            target = actor;
        }
    }
}
