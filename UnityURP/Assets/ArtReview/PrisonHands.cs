using UnityEngine;

public sealed class PrisonHands : MonoBehaviour
{
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    private float stride;
    private float pace;
    private float reach;
    private float wallTuck;
    private Vector3 leftRest;
    private Vector3 rightRest;
    private Quaternion leftRotation;
    private Quaternion rightRotation;
    private HauntExperience equipment;
    private Vector3 interactionPoint;
    private Vector3 smoothVelocity;
    private Quaternion previousView;
    private Vector2 sway;
    private float holdUntil;
    private float strikeAt=-10f;
    public bool KnifeEquipped { get; set; }
    public void Strike(){strikeAt=Time.time;}

    public void Configure(Transform leftHand, Transform rightHand)
    {
        left = leftHand;
        right = rightHand;
    }

    private void Awake()
    {
        if (left == null || right == null) { enabled = false; return; }
        leftRest = left.localPosition;
        rightRest = right.localPosition;
        leftRotation = left.localRotation;
        rightRotation = right.localRotation;
        equipment = GetComponentInParent<HauntExperience>();
        previousView = transform.rotation;
    }

    public void Step(float distance)
    {
        stride += Mathf.Min(distance, .15f) * 5.4f;
        pace = Mathf.MoveTowards(pace, Mathf.Clamp01(distance / Mathf.Max(Time.deltaTime, .001f) / 2.1f), Time.deltaTime * 6f);
    }

    public void Interact() { if (reach <= 0f) reach = 1f; }
    public void Interact(Vector3 point) { interactionPoint = point; Interact(); }
    public void Hold(Vector3 point) { interactionPoint = point; holdUntil = Time.time + .12f; }

    private void LateUpdate()
    {
        reach = Mathf.MoveTowards(reach, Time.time < holdUntil ? .5f : 0f, Time.deltaTime * 1.8f);
        float extension = Mathf.Sin(reach * Mathf.PI);
        float swing = Mathf.Sin(stride) * .007f * pace;
        float breath = Mathf.Sin(Time.time * 1.8f) * .0015f;
        float target = Physics.SphereCast(transform.position, .09f, transform.forward, out RaycastHit hit, .73f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? 1f - Mathf.InverseLerp(.2f, .73f, hit.distance) : 0f;
        wallTuck = Mathf.MoveTowards(wallTuck, target, Time.deltaTime * 6f);
        Vector3 tuck = new Vector3(0f, -.052f, -.13f) * wallTuck;
        Quaternion viewDelta = Quaternion.Inverse(previousView) * transform.rotation;
        Vector3 turn = viewDelta.eulerAngles;
        var targetSway = new Vector2(Mathf.DeltaAngle(0, turn.y), Mathf.DeltaAngle(0, turn.x));
        sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(targetSway, 2f), 1f-Mathf.Exp(-10f*Time.deltaTime));
        previousView = transform.rotation;
        Vector3 reachOffset = interactionPoint == Vector3.zero ? Vector3.zero :
            Vector3.ClampMagnitude(transform.InverseTransformPoint(interactionPoint) - leftRest, .11f) * extension;
        // Keep both wrists in their authored frame: movement never rotates finger bones.
        left.localPosition = Vector3.SmoothDamp(left.localPosition, leftRest + tuck + reachOffset +
            new Vector3(.012f * extension, breath - swing + .045f * extension, .025f * extension), ref smoothVelocity, .12f);
        Vector3 lowered = equipment != null && !equipment.HasTorch ? new Vector3(.025f, -.26f, -.03f) : Vector3.zero;
        right.localPosition = Vector3.Lerp(right.localPosition, rightRest + tuck + lowered + new Vector3(0f, breath + swing, 0f), 1f - Mathf.Exp(-14f * Time.deltaTime));
        left.localRotation = leftRotation * Quaternion.Euler(-5f * extension, 0f, 1.2f * pace * Mathf.Sin(stride));
        right.localRotation = rightRotation * Quaternion.Euler(-sway.y*.45f, -sway.x*.45f, -.6f * pace * Mathf.Sin(stride));
        if(KnifeEquipped)
        {
            float t=(Time.time-strikeAt)/.72f;
            float jab=t<0||t>1?0:t<.2f?-Mathf.Sin(t/.2f*Mathf.PI*.5f)*.3f:t<.46f?Mathf.Lerp(-.3f,1,Mathf.SmoothStep(0,1,(t-.2f)/.26f)):Mathf.Lerp(1,0,Mathf.SmoothStep(0,1,(t-.46f)/.54f));
            right.localPosition=rightRest+tuck+lowered+new Vector3(0,breath+swing,0)+new Vector3(-.018f*jab,.018f*jab,.15f*jab)*(1-wallTuck*.7f);
            right.localRotation*=Quaternion.Euler(-8*jab,4*jab,0);
        }
    }
}
