using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CellBlockVerification
{
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene("Assets/ArtReview/Expanded/CellBlockReview.unity");
        var doors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReviewDoor>()).ToArray();
        if (doors.Length != 9)
            throw new InvalidOperationException("Expected nine doors; got " + doors.Length);
        foreach (ReviewDoor door in doors)
            VerifyClearance(door);
    }

    private static void VerifyClearance(ReviewDoor door)
    {
        var settings = new SerializedObject(door);
        var hinge = (Transform)settings.FindProperty("hinge").objectReferenceValue;
        var leaf = hinge.GetComponentInChildren<BoxCollider>();
        Vector3 doorway = leaf.bounds.center;
        doorway.y = 0f;
        Vector3 inward = door.transform.forward;
        Vector3 origin = doorway - inward * .8f;
        Vector3 feet = origin + Vector3.up * .30f;
        Vector3 head = origin + Vector3.up * 1.51f;
        Physics.SyncTransforms();
        if (!Physics.CapsuleCast(feet, head, .27f, inward, out RaycastHit hit, 1.6f, ~0,
                QueryTriggerInteraction.Ignore) || hit.collider != leaf)
            throw new InvalidOperationException(door.name + " must block the player with its closed leaf.");

        Quaternion closed = hinge.localRotation;
        try
        {
            hinge.localRotation = closed * Quaternion.AngleAxis(settings.FindProperty("openAngle").floatValue, Vector3.up);
            Physics.SyncTransforms();
            if (Physics.CapsuleCast(feet, head, .27f, inward, out hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException(door.name + " is obstructed while open by " + hit.collider.name);
            if (!Physics.Raycast(doorway + inward * .8f + Vector3.up * .4f, Vector3.down, .5f, ~0,
                    QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException(door.name + " has no floor behind the threshold.");
            Debug.Log("PASS " + door.name + ": closed blocks capsule, open allows entry, room floor present");
        }
        finally
        {
            hinge.localRotation = closed;
            Physics.SyncTransforms();
        }
    }
}
