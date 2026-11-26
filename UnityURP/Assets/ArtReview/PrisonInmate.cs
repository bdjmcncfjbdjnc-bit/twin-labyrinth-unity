using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PrisonInmate : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject knife;
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
    private MazeSession session;
    private CharacterController body;
    private InmateThreat threat;
    private Vector3 home;
    private Quaternion rest;
    private bool walking;
    private float talkReady;
    private Vector3 lastSeen;

    public bool CanTalk => Time.time >= talkReady && (threat == null || threat.Current == InmateThreat.State.Calm);
    public void Configure(Animator mover, GameObject prop)
    {
        animator = mover;
        knife = prop;
    }

    public void BeginRound(MazeSession match, bool armed, InmateThreat.Temperament behavior)
    {
        session = match;
        body = GetComponent<CharacterController>();
        home = transform.position;
        rest = transform.rotation;
        threat = armed ? new InmateThreat(behavior) : null;
        knife.SetActive(false);
        Physics.IgnoreCollision(body, session.GetComponent<CharacterController>());
        Physics.IgnoreCollision(body, session.PartnerTransform.GetComponent<CharacterController>());
        if (session.GuardTransform != null)
            Physics.IgnoreCollision(body, session.GuardTransform.GetComponent<CharacterController>());
    }

    public bool Talk(string clue)
    {
        talkReady = Time.time + 12f;
        // A refusal is not a reliable weapon detector; both roles use the same dialogue pool.
        bool helpful = Random.value > .35f;
        GetComponent<PrisonActorMotion>()?.Gesture(helpful ? "Talk" : "Shrug", 2.6f);
        session.InmateMessage(helpful ? clue : "Inmate: Keep moving. I don't know anything.", false);
        return helpful;
    }

    private void Update()
    {
        if (session == null || !session.HasStarted || session.IsOver)
            return;
        float dt = Time.deltaTime;
        Vector3 player = session.transform.position;
        Vector3 offset = player - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        bool visible = distance < 9f && (!Physics.Linecast(transform.position + Vector3.up * 1.25f,
            player + Vector3.up * 1.25f, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            hit.transform == session.transform);
        Vector3 motion = Vector3.zero;
        if (threat != null)
        {
            var previous = threat.Current;
            if (visible)
                lastSeen = player;
            bool inside = InsideTerritory(player) && InsideTerritory(transform.position);
            bool contact = threat.Step(dt, distance, visible, !session.HasDetainee, inside);
            if (threat.Current != previous)
            {
                knife.SetActive(threat.KnifeVisible);
                if (threat.Current == InmateThreat.State.Warning)
                {
                    GetComponent<PrisonActorMotion>()?.Gesture("Argue", 1.3f);
                    session.InmateMessage("KNIFE VISIBLE - BACK AWAY OR BREAK LINE OF SIGHT", true);
                }
                if (threat.Current == InmateThreat.State.Pursuit)
                    session.InmateMessage("INMATE PURSUIT - LEAVE THIS AREA", true);
                if (previous == InmateThreat.State.Pursuit && !contact)
                    session.InmateMessage("YOU SHOOK OFF THE INMATE", false);
                if (contact)
                {
                    GetComponent<PrisonActorMotion>()?.Strike(session.transform.position + Vector3.up * 1.15f);
                    session.InmateContact();
                }
            }
            if (threat.Current == InmateThreat.State.Pursuit)
            {
                Vector3 next = session.RouteWaypoint(transform.position, lastSeen);
                if (InsideTerritory(next))
                    motion = MoveToward(next, 2.95f * dt);
            }
        }
        if (motion.sqrMagnitude == 0f && (threat == null || !threat.KnifeVisible))
        {
            if ((home - transform.position).sqrMagnitude > .034f)
                motion = MoveToward(session.RouteWaypoint(transform.position, home), 1.65f * dt);
        }
        bool face = visible && distance > .1f && motion.sqrMagnitude <= .000001f &&
            (threat == null || threat.Current != InmateThreat.State.Recovering);
        Quaternion desired = face ? Quaternion.LookRotation(offset) :
            motion.sqrMagnitude > .000001f ? Quaternion.LookRotation(motion) : rest;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, dt * 240f);
        Vector3 before = transform.position;
        body.Move(motion + Vector3.down * (5f * dt));
        Vector3 traveled = transform.position - before;
        bool moved = new Vector2(traveled.x, traveled.z).sqrMagnitude > .000001f;
        if (moved != walking)
        {
            animator.CrossFade(moved ? Walk : Idle, .16f);
            walking = moved;
        }
    }

    private bool InsideTerritory(Vector3 point)
    {
        Vector3 offset = point - home;
        offset.y = 0f;
        return offset.sqrMagnitude <= InmateThreat.TerritoryRadius * InmateThreat.TerritoryRadius;
    }

    private Vector3 MoveToward(Vector3 point, float maxDistance)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return Vector3.ClampMagnitude(offset, maxDistance);
    }
}
