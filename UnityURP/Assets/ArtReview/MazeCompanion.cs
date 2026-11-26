using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class MazeCompanion : MonoBehaviour
{
    private static readonly int Walk = Animator.StringToHash("Base Layer.Prisoner_walk");
    private static readonly int Idle = Animator.StringToHash("Base Layer.Prisoner_idle");
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private int size;
    [SerializeField] private float pitch;
    [SerializeField] private int[] passages;
    [SerializeField] private ReviewDoor[] gates;
    private CharacterController mover;
    private bool walking;
    private float nextPlan;
    private int current;
    private int destination = -1;
    private ReviewDoor gate;
    private int[] parents;
    private readonly Queue<int> queue = new();
    private readonly Dictionary<int, ReviewDoor> doors = new();

    public bool Following { get; private set; } = true;
    public bool NeedsHelp { get; private set; }
    private bool roundOver;
    private bool autonomous;
    private bool commandPending;
    private Transform rescueTarget;
    private CharacterController ignoredActor;
    private int idleState = Idle;
    private int walkState = Walk;
    private Transform cooperationTarget;
    public void CooperateAt(Transform target) { cooperationTarget = target; destination = -1; }
    public bool Cooperating => cooperationTarget != null;
    public void SetPassages(int[] value) => passages = value;
    public float Speed { get; set; } = 2.5f;

    public void SetDistressed(bool distressed)
    {
        if (NeedsHelp && !distressed)
            Following = true;
        NeedsHelp = distressed;
    }

    public void SetRoundOver() => roundOver = true;
    public void SetRescueTarget(Transform target) => rescueTarget = target;

    public Vector3 NextWaypoint(Vector3 from, Vector3 target)
    {
        int start = Cell(from);
        int goal = Cell(target);
        if (start == goal)
            return target;
        int next = FindNext(start, goal);
        return next < 0 ? from : Center(next);
    }

    public int RouteLength(Vector3 from, Vector3 goal)
    {
        int start = Cell(from);
        int end = Cell(goal);
        if (start == end)
            return 0;
        if (FindNext(start, end) < 0)
            return int.MaxValue;
        int length = 0;
        for (int cell = end; cell != start; cell = parents[cell])
            length++;
        return length;
    }

    public Vector3 SearchNeighbor(Vector3 point, Vector3 preferredDirection)
    {
        int cell = Cell(point);
        int best = cell;
        float priority = float.NegativeInfinity;
        for (int direction = 0; direction < Directions.Length; direction++)
        {
            if ((passages[cell] & (1 << direction)) == 0)
                continue;
            Vector2Int offset = Directions[direction];
            int neighbor = cell + offset.x + size * offset.y;
            if (doors.TryGetValue(Edge(cell, neighbor), out ReviewDoor door) && door.IsLocked)
                continue;
            float score = Vector3.Dot(new Vector3(offset.x, 0f, offset.y), preferredDirection);
            if (score > priority)
            {
                priority = score;
                best = neighbor;
            }
        }
        return Center(best);
    }
    public bool TakeCommand()
    {
        bool pending = commandPending;
        commandPending = false;
        return pending;
    }

    public void ConfigureAutonomous(CharacterController other)
    {
        autonomous = true;
        ignoredActor = other;
        idleState = Animator.StringToHash("Base Layer.Idle");
        walkState = Animator.StringToHash("Base Layer.Walk");
    }

    [SerializeField] private bool patrolNavigator;
    [SerializeField] private CharacterController patrolCollision;

    public void MarkPatrol(CharacterController collision)
    {
        patrolNavigator = true;
        patrolCollision = collision;
    }

    public void Configure(Transform target, Animator animated, int width, float spacing, int[] edges, ReviewDoor[] barriers)
    {
        player = target;
        animator = animated;
        size = width;
        pitch = spacing;
        passages = edges;
        gates = barriers;
    }

    private void Awake() => Initialize();

    public void Initialize()
    {
        mover = GetComponent<CharacterController>();
        if (patrolNavigator)
            ConfigureAutonomous(patrolCollision);
        var collidable = autonomous ? ignoredActor : player.GetComponent<CharacterController>();
        if (collidable != null)
            Physics.IgnoreCollision(mover, collidable);
        doors.Clear();
        parents = new int[passages.Length];
        current = Cell(transform.position);
        foreach (ReviewDoor door in gates)
        {
            Vector3 side = door.transform.forward * .6f;
            doors[Edge(Cell(door.transform.position - side), Cell(door.transform.position + side))] = door;
        }
    }

    private void Update()
    {
        if (!NeedsHelp && !roundOver && PrisonInput.Down(KeyCode.T))
        {
            if (!autonomous && rescueTarget == null && cooperationTarget == null)
            {
                Following = !Following;
                commandPending = true;
            }
        }
        Step(Time.deltaTime, Time.time);
    }

    public void Step(float seconds, float clock)
    {
        Vector3 motion = Vector3.zero;
        if ((Following || rescueTarget != null || cooperationTarget != null) && !NeedsHelp && !roundOver)
        {
            Transform objective = rescueTarget != null ? rescueTarget : cooperationTarget != null ? cooperationTarget : player;
            int target = Cell(objective.position);
            if (destination < 0 && clock >= nextPlan && target != current)
            {
                nextPlan = clock + .18f;
                destination = FindNext(current, target);
                gate = destination >= 0 && doors.TryGetValue(Edge(current, destination), out ReviewDoor barrier) ? barrier : null;
            }
            if (destination >= 0)
            {
                Vector3 offset = Center(destination) - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < .0324f)
                {
                    current = destination;
                    destination = -1;
                }
                else if (gate == null || DistanceToGate() > 1.35f || gate.RequestPassage())
                {
                    float speed = autonomous ? Speed : (objective.position - transform.position).sqrMagnitude > 22f ? 3.8f : Speed;
                    motion = Vector3.ClampMagnitude(offset, speed * seconds);
                }
            }
            else if (target == current)
            {
                Vector3 offset = objective.position - transform.position;
                offset.y = 0f;
                float stop = cooperationTarget != null && rescueTarget == null ? .18f : 1.15f;
                if (offset.sqrMagnitude > stop * stop)
                    motion = Vector3.ClampMagnitude(offset, Speed * seconds);
            }
        }

        if (motion.sqrMagnitude > 0f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(motion), 390f * seconds);
        Vector3 previous = transform.position;
        mover.Move(motion + Vector3.down * (6f * seconds));
        Vector3 translation = transform.position - previous;
        translation.y = 0f;
        bool nowWalking = translation.sqrMagnitude > 1e-6f;
        if (Application.isPlaying && nowWalking != walking)
        {
            animator.CrossFade(nowWalking ? walkState : idleState, .12f, 0);
            walking = nowWalking;
        }
    }

    private float DistanceToGate()
    {
        Vector3 offset = gate.transform.position - transform.position;
        offset.y = 0;
        return offset.magnitude;
    }

    private int Edge(int a, int b) => Mathf.Min(a, b) * passages.Length + Mathf.Max(a, b);

    private int Cell(Vector3 position) =>
        Mathf.Clamp(Mathf.FloorToInt(position.z / pitch), 0, size - 1) * size +
        Mathf.Clamp(Mathf.FloorToInt(position.x / pitch), 0, size - 1);

    private Vector3 Center(int cell) => new Vector3((cell % size + .5f) * pitch, 0f, (cell / size + .5f) * pitch);

    private int FindNext(int from, int goal)
    {
        Array.Fill(parents, -1);
        parents[from] = from;
        queue.Clear();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            if (cell == goal)
            {
                while (parents[cell] != from)
                    cell = parents[cell];
                return cell;
            }
            for (int direction = 0; direction < 4; direction++)
            {
                if ((passages[cell] & (1 << direction)) == 0)
                    continue;
                int next = cell + Directions[direction].x + size * Directions[direction].y;
                if (parents[next] >= 0 || (doors.TryGetValue(Edge(cell, next), out ReviewDoor barrier) && barrier.IsLocked))
                    continue;
                parents[next] = cell;
                queue.Enqueue(next);
            }
        }
        return -1;
    }
}
