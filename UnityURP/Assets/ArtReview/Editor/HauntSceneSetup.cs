using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class HauntSceneSetup
{
    public static void AddExperience(MazeLayout layout, GameObject player, Material steel, Transform[] figures)
    {
        var flashlight = CorridorReviewBuilder.LoadEnvironment("small_plastic_torch");
        var jug = CorridorReviewBuilder.LoadEnvironment("plastic_bottle_gallon");
        var camera = player.GetComponentInChildren<Camera>();
        camera.gameObject.AddComponent<AudioListener>();
        var torch = camera.GetComponentInChildren<Light>(true);
        var held = (GameObject)PrefabUtility.InstantiatePrefab(flashlight, camera.transform);
        held.name = "Held flashlight";
        Vector3 dimensions = CorridorReviewBuilder.ModelBounds(flashlight).size;
        held.transform.localRotation = dimensions.x > dimensions.z && dimensions.x > dimensions.y
            ? Quaternion.Euler(0, 90, 0)
            : dimensions.y > dimensions.z ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        Vector3 center = camera.transform.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(held).center);
        held.transform.localPosition += new Vector3(.244f, -.245f, .51f) - center;
        foreach (var renderer in held.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        held.SetActive(false);

        Vector3 start = MazeLayout.Center(MazeLayout.Start);
        Vector3 intake = start + player.transform.forward * 1.2f + player.transform.right * .72f;
        var pickupLight = CorridorReviewBuilder.AddLight("Intake supply marker", intake + Vector3.up * 2f,
            LightType.Point, new Color(.83f, .78f, .66f), .38f, 2.5f);
        pickupLight.shadows = LightShadows.None;
        PlaceShelf(intake, steel, .68f);
        PlacePickup(HauntPickup.Kind.Torch, intake + Vector3.up * 1.26f, flashlight);
        CorridorReviewBuilder.PlaceRoomSign("FLASHLIGHT", intake + Vector3.up * 1.57f, player.transform.rotation).characterSize = .055f;

        foreach (int station in new[] { 34, 70, 106, 143 })
        {
            int cell = layout.Route[station];
            Vector3 outward = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
            Vector3 position = MazeLayout.Center(cell) + outward * 1.48f;
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            PlaceShelf(position, steel, .92f);
            PlacePickup(HauntPickup.Kind.Water, position + across * .25f + Vector3.up * 1.26f, jug);
            PlaceBatteries(position - across * .23f + Vector3.up * 1.28f, across, steel);
            CorridorReviewBuilder.PlaceRoomSign("SUPPLIES", position + outward * .37f + Vector3.up * 1.88f,
                Quaternion.LookRotation(outward)).characterSize = .065f;
        }

        player.AddComponent<HauntExperience>().Configure(camera.transform, torch, held, Clip("distant-vent"), Clip("breathing"),
            Enumerable.Range(2, 5).Select(RecordedStep).ToArray(),
            Clip("torch-click"), Clip("collect"), Clip("metal-latch"), Clip("figure"), Clip("anomaly"), figures);
    }

    private static AudioClip RecordedStep(int index)
    {
        string path = $"Assets/ArtReview/Source/RecordedSteps/{index:00}-footstep.ogg";
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
            throw new InvalidDataException("Missing recorded footstep: " + path);
    }

    public static void SetPartnerAudio(HauntExperience experience)
    {
        AudioClip[] walk = Enumerable.Range(1, 6).Select(i => PartnerClip($"walk-{i:00}.wav")).ToArray();
        AudioClip[] run = Enumerable.Range(1, 8).Select(i => PartnerClip($"run-{i:00}.wav")).ToArray();
        experience.ConfigurePartnerAudio(walk, run, PartnerClip("intake.ogg"), PartnerClip("explore.ogg"),
            PartnerClip("blackout.ogg"), PartnerClip("anomaly.ogg"), PartnerClip("sting.wav"),
            PartnerClip("exhausted.wav"), Clip("heartbeat"));
    }

    private static AudioClip PartnerClip(string name)
    {
        string path = "Assets/ArtReview/Source/PartnerAudio/" + name;
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
            throw new InvalidDataException("Missing partner audio: " + path);
    }

    public static void PlaceRelays(MazeLayout layout)
    {
        GameObject panel = CorridorReviewBuilder.LoadEnvironment("power_box_01");
        int[] stations = { 52, 118 };
        for (int index = 0; index < stations.Length; index++)
        {
            int cell = layout.Route[stations[index]];
            Vector3 outward = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
            Quaternion orientation = Quaternion.LookRotation(outward);
            Vector3 wall = MazeLayout.Center(cell) + outward * 1.76f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(panel);
            model.name = index == 0 ? "Relay A control panel" : "Relay B control panel";
            model.transform.SetPositionAndRotation(wall + Vector3.up * 1.42f, Quaternion.LookRotation(-outward));
            Bounds bounds = CorridorReviewBuilder.ModelBounds(model);
            var trigger = new GameObject("Relay focus range");
            trigger.transform.SetParent(model.transform, true);
            trigger.transform.SetPositionAndRotation(bounds.center - outward * .1f, Quaternion.identity);
            var collider = trigger.AddComponent<BoxCollider>();
            collider.size = Vector3.Max(bounds.size, new Vector3(.66f, .66f, .66f));
            collider.isTrigger = true;
            Light indicator = CorridorReviewBuilder.AddLight("Relay online indicator",
                wall - outward * .52f + Vector3.up * 1.63f, LightType.Point, new Color(.5f, .91f, .58f), .75f, 2.4f);
            indicator.shadows = LightShadows.None;
            indicator.enabled = false;
            var relay = model.AddComponent<HauntRelay>();
            relay.Configure(index, indicator);
            CorridorReviewBuilder.PlaceRoomSign(relay.Label, wall + Vector3.up * 2.12f, orientation);
        }
    }

    private static AudioClip Clip(string label)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtReview/Source/HauntAudio/" + label + ".wav");
        return clip != null ? clip : throw new InvalidDataException("Generate haunt audio before building: " + label);
    }

    private static void PlaceShelf(Vector3 center, Material steel, float width)
    {
        CorridorReviewBuilder.Surface("Supply stand", center + Vector3.up * 1.19f, new Vector3(width, .12f, width * .6f), steel);
        CorridorReviewBuilder.Surface("Stand support", center + Vector3.up * .57f, new Vector3(.21f, 1.14f, .2f), steel);
    }

    private static void PlacePickup(HauntPickup.Kind kind, Vector3 basePosition, GameObject prefab)
    {
        var pickup = NewPickup(kind, basePosition, out BoxCollider collider);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pickup.transform);
        Bounds bounds = CorridorReviewBuilder.ModelBounds(model);
        if (kind == HauntPickup.Kind.Water)
            model.transform.localScale *= .73f;
        bounds = CorridorReviewBuilder.ModelBounds(model);
        model.transform.position += new Vector3(basePosition.x - bounds.center.x, basePosition.y - bounds.min.y,
            basePosition.z - bounds.center.z);
        bounds = CorridorReviewBuilder.ModelBounds(model);
        collider.center = pickup.transform.InverseTransformPoint(bounds.center);
        collider.size = Vector3.Max(bounds.size, new Vector3(.4f, .23f, .4f));
    }

    private static void PlaceBatteries(Vector3 center, Vector3 across, Material steel)
    {
        var pickup = NewPickup(HauntPickup.Kind.Batteries, center, out BoxCollider collider);
        collider.center = new Vector3(0, .12f, 0);
        collider.size = new Vector3(.38f, .32f, .38f);
        for (int index = 0; index < 2; index++)
        {
            var battery = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            battery.name = "Battery";
            battery.transform.SetParent(pickup.transform, false);
            battery.transform.localPosition = across * (index == 0 ? -.042f : .042f) + Vector3.up * .066f;
            battery.transform.localScale = new Vector3(.048f, .066f, .048f);
            battery.GetComponent<Renderer>().sharedMaterial = steel;
            UnityEngine.Object.DestroyImmediate(battery.GetComponent<Collider>());
        }
    }

    private static HauntPickup NewPickup(HauntPickup.Kind kind, Vector3 position, out BoxCollider collider)
    {
        var pickup = new GameObject(kind.ToString()).AddComponent<HauntPickup>();
        pickup.Configure(kind);
        pickup.transform.position = position;
        collider = pickup.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        return pickup;
    }
}
