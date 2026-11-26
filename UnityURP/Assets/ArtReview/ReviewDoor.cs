using UnityEngine;

public sealed class ReviewDoor : MonoBehaviour
{
    [SerializeField] private Transform hinge;
    [SerializeField] private float openAngle;
    [SerializeField] private float sweepRadius;
    [SerializeField] private bool locked;

    private Quaternion closedRotation;
    private CharacterController visitor;
    private float position;
    private bool open;
    public bool IsSealed { get; private set; }

    public string Prompt => IsSealed ? "Temporarily sealed" : locked ? "Locked - activate relays" : open ? "F  Close door" : "F  Open door";

    public void Lock()
    {
        locked = true;
        open = false;
    }
    public void Unlock() => locked = false;
    public bool IsLocked => locked || IsSealed;
    public bool IsOpen => open;
    public void ApplyNetworkState(bool opened,bool isLocked){visitor=null;IsSealed=false;locked=isLocked;open=opened&&!locked;}

    public bool TrySeal()
    {
        if (IsLocked)
            return false;
        IsSealed = true;
        open = false;
        return true;
    }

    public void LiftSeal() => IsSealed = false;

    public bool RequestPassage()
    {
        if (IsLocked)
            return false;
        open = true;
        return position > .78f;
    }

    public void Configure(Transform pivot, float angle, float radius)
    {
        hinge = pivot;
        openAngle = angle;
        sweepRadius = radius;
    }

    private void Awake() => Initialize();
    public void Initialize() => closedRotation = hinge.localRotation;

    public void Toggle(CharacterController actor)
    {
        if (IsLocked)
            return;
        visitor = actor;
        open = !open;
    }

    private void Update() => Step(Time.deltaTime);

    public void Step(float seconds)
    {
        if (!open && position > 0f && visitor != null)
        {
            Vector3 offset = visitor.transform.position - hinge.position;
            offset.y = 0f;
            // Leave the route open if the player is inside the closing sweep.
            if (offset.sqrMagnitude < Mathf.Pow(sweepRadius + visitor.radius, 2f))
                open = true;
        }

        float next = Mathf.MoveTowards(position, open ? 1f : 0f, seconds / .65f);
        if (next == position)
            return;

        position = next;
        float eased = Mathf.SmoothStep(0f, 1f, position);
        hinge.localRotation = closedRotation * Quaternion.AngleAxis(openAngle * eased, Vector3.up);
    }
}
