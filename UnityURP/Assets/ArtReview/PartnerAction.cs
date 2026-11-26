using UnityEngine;

// Interaction overlay stays in bone space and never changes bone lengths.
public sealed class PartnerAction : MonoBehaviour
{
    private Transform hand;
    private Transform target;
    private float until;
    private void Awake()
    {
        foreach (Transform bone in GetComponentsInChildren<Transform>())
            if (bone.name.ToLowerInvariant().Contains("hand") && bone.name.ToLowerInvariant().Contains("right")) { hand = bone; break; }
    }
    public void Operate(Transform point) { target = point; until = Time.time + 1.8f; }
    private void LateUpdate()
    {
        if (target == null || hand == null || Time.time >= until) return;
        float blend = Mathf.Sin(Mathf.Clamp01((until - Time.time) / 1.8f) * Mathf.PI);
        BoneReach.Solve(hand.parent.parent, hand.parent, hand, target.position, transform.right, blend * .8f);
    }
}
