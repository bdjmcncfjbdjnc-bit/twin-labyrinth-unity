using System.Collections.Generic;
using UnityEngine;

public sealed class MazeAnomaly : MonoBehaviour
{
    [SerializeField] private Transform observer;
    private readonly List<(SkinnedMeshRenderer renderer, int index, float neutral, float distorted)> shapes = new();
    private float amount;

    public void Configure(Transform player) => observer = player;

    private void Awake()
    {
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            AddShape(renderer, "face_chin_incr", .35f);
            AddShape(renderer, "face_mouth_width_incr", .25f);
            AddShape(renderer, "face_jaw_width_decr", .2f);
        }
    }

    private void AddShape(SkinnedMeshRenderer renderer, string name, float strength)
    {
        Mesh mesh = renderer.sharedMesh;
        int index = mesh.GetBlendShapeIndex(name);
        if (index < 0)
            return;
        float frame = mesh.GetBlendShapeFrameWeight(index, mesh.GetBlendShapeFrameCount(index) - 1);
        shapes.Add((renderer, index, renderer.GetBlendShapeWeight(index), frame * strength));
    }

    private void LateUpdate()
    {
        float distance = Vector3.Distance(observer.position, transform.position);
        float target = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 5.5f, distance));
        amount = Mathf.MoveTowards(amount, target, Time.deltaTime * .75f);
        foreach (var shape in shapes)
            shape.renderer.SetBlendShapeWeight(shape.index, Mathf.Lerp(shape.neutral, shape.distorted, amount));
    }
}
