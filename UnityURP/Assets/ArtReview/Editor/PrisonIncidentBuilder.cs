using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PrisonIncidentBuilder
{
    public static void Stage(MazeLayout layout, MazeSession session)
    {
        if (session.GetComponent<PrisonGuidance>() == null)
            StageRouteNotes(layout, session);
        if (session.GetComponent<PrisonIncidents>() != null)
            return;
        var gates = Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None)
            .Where(door => door.name.StartsWith("CHECKPOINT ", StringComparison.Ordinal)).ToArray();
        if (gates.Length != 5)
            throw new InvalidOperationException("Temporary seals require five checkpoint gates.");
        var nodes = new[] { 27, 87, 131 }.Select(station => PlaceBreaker(layout, station)).ToArray();
        session.gameObject.AddComponent<PrisonIncidents>().Configure(session, gates, nodes);
        Debug.Log("Staged temporary checkpoint seals and three local power breaker points.");
    }

    private static void StageRouteNotes(MazeLayout layout, MazeSession session)
    {
        var relays = Object.FindObjectsByType<HauntRelay>(FindObjectsSortMode.None).OrderBy(relay => relay.Index).ToArray();
        if (relays.Length != 2)
            throw new InvalidOperationException("Route notes require both escape relays.");
        var destination = new GameObject("Route hint exit target").transform;
        destination.position = MazeLayout.Center(layout.Exit);
        session.gameObject.AddComponent<PrisonGuidance>().Configure(session, relays, destination);
        foreach (int station in new[] { 8, 48, 95, 138 })
        {
            int cell = layout.Route[station];
            Vector3 outward = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
            Vector3 position = MazeLayout.Center(cell) + outward * 1.65f;
            TextMesh text = CorridorReviewBuilder.PlaceRoomSign("MAINTENANCE ROUTE\nF  READ NOTE",
                position + Vector3.up * 1.57f, Quaternion.LookRotation(outward));
            text.characterSize = .047f;
            text.color = new Color(.78f, .88f, .70f);
            var focus = text.gameObject.AddComponent<BoxCollider>();
            focus.size = new Vector3(1.12f, .65f, .18f);
            focus.isTrigger = true;
            text.gameObject.AddComponent<PrisonRouteBoard>();
        }
    }

    private static PrisonBreaker PlaceBreaker(MazeLayout layout, int station)
    {
        int cell = layout.Route[station];
        Vector3 outward = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
        Vector3 position = MazeLayout.Center(cell) + outward * 1.76f;
        var root = new GameObject($"Local breaker {station}");
        root.transform.SetPositionAndRotation(position + Vector3.up * 1.42f, Quaternion.LookRotation(outward));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(CorridorReviewBuilder.LoadEnvironment("power_box_01"), root.transform);
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        model.transform.localPosition -= root.transform.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(model).center);
        BoxCollider target = root.AddComponent<BoxCollider>();
        target.size = new Vector3(.8f, .9f, .42f);
        target.center = new Vector3(0f, 0f, -.13f);
        target.isTrigger = true;
        TextMesh status = CorridorReviewBuilder.PlaceRoomSign("BREAKER", position + Vector3.up * 2.05f - outward * .12f,
            Quaternion.LookRotation(outward));
        status.characterSize = .047f;
        var breaker = root.AddComponent<PrisonBreaker>();
        breaker.Configure(status);
        breaker.SetFault(false);
        return breaker;
    }
}
