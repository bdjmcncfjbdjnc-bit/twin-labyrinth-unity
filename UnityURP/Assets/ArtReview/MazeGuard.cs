using UnityEngine;

[RequireComponent(typeof(MazeCompanion), typeof(AudioSource))]
public sealed class MazeGuard : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase, Search, Escort }
    [SerializeField] private MazeSession session;
    [SerializeField] private Transform eyes;
    [SerializeField] private Transform objective;
    [SerializeField] private Transform holding;
    [SerializeField] private Transform[] checkpoints;
    [SerializeField] private Vector3[] intakeRoute;
    [SerializeField] private Vector3[] relayRoute;
    [SerializeField] private Vector3[] exitRoute;
    [SerializeField] private AudioClip stepClip;
    [SerializeField] private AudioClip radioClip;
    private MazeCompanion navigator;
    private AudioSource audioSource;
    private AudioSource radio;
    private Transform suspect;
    private float contact;
    private float timer;
    private float nextThink;
    private float gait;
    private int routeIndex;
    private int assignment = -1;
    private Vector3 lastPosition;
    private Vector3 lastSighting;
    private Vector3 previousNoise;
    private float noiseTime;
    private int searchStep;

    public State Current { get; private set; }
    private Transform selectedHolding;
    public Transform Holding => selectedHolding != null ? selectedHolding : holding;
    public void ConfigureCheckpoints(Transform[] choices) => checkpoints = choices;
    public Vector3 RouteToward(Vector3 from, Vector3 to) => navigator.NextWaypoint(from, to);

    public void Configure(MazeSession match, Transform view, Transform destination, Transform detention,
        Vector3[] early, Vector3[] relays, Vector3[] exit, AudioClip step, AudioClip cue)
    {
        session = match;
        eyes = view;
        objective = destination;
        holding = detention;
        intakeRoute = early;
        relayRoute = relays;
        exitRoute = exit;
        stepClip = step;
        radioClip = cue;
    }

    private void Awake()
    {
        navigator = GetComponent<MazeCompanion>();
        IgnorePartnerCollision();
        eyes.GetComponentInChildren<Light>().shadows = LightShadows.Hard;
        audioSource = GetComponent<AudioSource>();
        ConfigureSource(audioSource);
        radio = gameObject.AddComponent<AudioSource>();
        ConfigureSource(radio);
        lastPosition = transform.position;
        objective.position = intakeRoute[0];
    }

    public void IgnorePartnerCollision()
    {
        Physics.IgnoreCollision(GetComponent<CharacterController>(), session.PartnerTransform.GetComponent<CharacterController>());
    }

    private static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 2f;
        source.maxDistance = 17f;
        source.rolloffMode = AudioRolloffMode.Linear;
    }

    public bool CanSee(Transform target, float range, float angle)
    {
        if (target == null)
            return false;
        Vector3 direction = target.position + Vector3.up * 1.45f - eyes.position;
        if (direction.sqrMagnitude > range * range || Vector3.Dot(eyes.forward, direction.normalized) < angle)
            return false;
        return !Physics.Linecast(eyes.position, eyes.position + direction, out RaycastHit hit,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(target);
    }

    public bool Hear(Vector3 point, float range, bool decoy)
    {
        if (session.IsOver || !session.HasStarted || Current is State.Chase or State.Escort ||
            (point - transform.position).sqrMagnitude > range * range)
            return false;
        bool repeated = decoy && Time.time < noiseTime && Vector3.Distance(point, previousNoise) < 1.5f;
        if (decoy)
        {
            noiseTime = Time.time + 25f;
            previousNoise = point;
        }
        lastSighting = point;
        searchStep = 0;
        SetState(repeated ? State.Search : State.Investigate, 8f);
        objective.position = repeated ? Vector3.Lerp(transform.position, point, .42f) : point;
        return true;
    }

    public void StopGuard() => navigator.SetRoundOver();

    private void Update()
    {
        Vector3 displacement = transform.position - lastPosition;
        gait += new Vector2(displacement.x, displacement.z).magnitude;
        lastPosition = transform.position;
        if (session.IsOver || !session.HasStarted)
            return;
        if (gait >= (Current == State.Chase ? 1.13f : .82f))
        {
            gait = 0f;
            audioSource.PlayOneShot(stepClip, Current == State.Chase ? .48f : .32f);
        }
        if (Time.time < nextThink)
            return;
        nextThink = Time.time + .12f;
        Think(.12f);
    }

    private void Think(float seconds)
    {
        if (Current == State.Escort)
        {
            objective.position = Holding.position;
            bool arrived = session.HasDetainee && session.MoveDetainee(transform.position, seconds);
            if (arrived || !session.HasDetainee || Time.time >= timer)
            {
                if (arrived)
                    session.ArriveAtHolding();
                else if (session.HasDetainee)
                    session.ReleaseStalledEscort();
                SetState(State.Patrol);
                assignment = -1;
            }
            return;
        }
        bool pursuitAllowed = session.CanGuardPursue;
        bool sweep = session.IsSecuritySweep;
        float range = sweep ? 11f : 7.1f;
        Transform visible = pursuitAllowed && CanSee(session.transform, range + (session.TorchLit ? 3f : 0f), .67f)
            ? session.transform : pursuitAllowed && CanSee(session.PartnerTransform, range - 1f, .74f)
                ? session.PartnerTransform : null;
        if (visible != null)
        {
            contact = suspect == visible ? contact + seconds : seconds;
            suspect = visible;
            lastSighting = visible.position;
            if (Current == State.Chase || contact >= (sweep ? .65f : 1.05f))
            {
                SetState(State.Chase, 3.4f);
                objective.position = lastSighting;
                if (Near(lastSighting, 1.25f) && session.TryDetain(visible))
                {
                    selectedHolding = holding;
                    foreach (Transform option in checkpoints)
                    {
                        if (navigator.RouteLength(transform.position, option.position) <
                            navigator.RouteLength(transform.position, selectedHolding.position))
                            selectedHolding = option;
                    }
                    SetState(State.Escort, 50f);
                }
                return;
            }
        }
        else
            contact = 0f;

        if (Current == State.Chase)
        {
            if (pursuitAllowed && Time.time < timer && !Near(lastSighting, 1.05f))
                return;
            searchStep = 0;
            SetState(State.Search, 6f);
        }
        if (Current is State.Investigate or State.Search)
        {
            if (Time.time < timer)
            {
                if (Near(objective.position, 1.2f) && searchStep < 2)
                {
                    bool approachingNoise = !Near(lastSighting, 1.5f) && searchStep == 0;
                    objective.position = approachingNoise ? lastSighting : navigator.SearchNeighbor(lastSighting, eyes.forward);
                    searchStep++;
                }
                if (Near(objective.position, 1.2f))
                    transform.rotation *= Quaternion.Euler(0f, seconds * 120f, 0f);
                return;
            }
            SetState(State.Patrol);
            assignment = -1;
        }
        Patrol();
    }

    private void Patrol()
    {
        int nextAssignment = session.ExitUnlocked ? 2 : session.RelayAOn ? 1 : 0;
        Vector3[] route = nextAssignment == 2 ? exitRoute : nextAssignment == 1 ? relayRoute : intakeRoute;
        if (assignment != nextAssignment)
        {
            assignment = nextAssignment;
            routeIndex = 0;
            objective.position = route[0];
            radio.PlayOneShot(radioClip, .25f);
        }
        else if (Near(objective.position, 1.25f))
        {
            routeIndex = (routeIndex + 1) % route.Length;
            objective.position = route[routeIndex];
        }
    }

    private bool Near(Vector3 position, float radius) =>
        (position - transform.position).sqrMagnitude <= radius * radius;

    private void SetState(State next, float duration = 0f)
    {
        if (Current != next && next != State.Patrol)
            radio.PlayOneShot(radioClip, next == State.Chase ? .46f : .29f);
        Current = next;
        timer = Time.time + duration;
        navigator.Speed = next == State.Chase ? 3.15f : next == State.Escort ? 3.5f : 1.9f;
    }
}
