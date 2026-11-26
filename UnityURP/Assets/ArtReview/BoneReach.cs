using UnityEngine;

public static class BoneReach
{
    public static void Solve(Transform shoulder, Transform elbow, Transform wrist, Vector3 target, Vector3 pole, float weight)
    {
        if (shoulder == null || elbow == null || wrist == null || weight <= 0f) return;
        float a = Vector3.Distance(shoulder.position, elbow.position), b = Vector3.Distance(elbow.position, wrist.position);
        Vector3 delta = target - shoulder.position;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a-b)+.005f, a+b-.005f);
        Vector3 direction = delta.normalized;
        Vector3 perpendicular = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (perpendicular.sqrMagnitude < .1f) perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
        float along = (a*a+d*d-b*b)/(2f*d);
        Vector3 bend = shoulder.position + direction*along + perpendicular*Mathf.Sqrt(Mathf.Max(0f,a*a-along*along));
        Quaternion upper = Quaternion.FromToRotation(elbow.position-shoulder.position, bend-shoulder.position)*shoulder.rotation;
        shoulder.rotation = Quaternion.Slerp(shoulder.rotation, upper, weight);
        Quaternion lower = Quaternion.FromToRotation(wrist.position-elbow.position, target-elbow.position)*elbow.rotation;
        elbow.rotation = Quaternion.Slerp(elbow.rotation, lower, weight);
    }
}
