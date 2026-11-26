using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

internal static class PrisonGuardBuilder
{
    private const string Output = "Assets/ArtReview/HauntCoop/";
    private const string Source = "Assets/ArtReview/Source/";

    public static void Stage(MazeLayout layout, MazeSession session)
    {
        var camera = session.GetComponentInChildren<Camera>();
        if (camera.GetComponent<PrisonHands>() == null)
            PrisonHandsBuilder.Stage(camera, camera.transform.Find("Held flashlight"));
        PrisonHandsBuilder.SetupOverlay(camera);
        if (session.HasGuard)
        {
            SetHoldingCheckpoints(layout, Object.FindFirstObjectByType<MazeGuard>());
            return;
        }

        Vector3 Station(int station) => MazeLayout.Center(layout.Route[station]);
        GameObject officer = GameObject.Find("Intake orderly");
        GameObject deputy = Object.Instantiate(GameObject.Find("Infirmary worker"), officer.transform.position, officer.transform.rotation);
        deputy.name = "Intake orderly";
        officer.name = "Prison guard";
        officer.transform.position = Station(17);
        officer.transform.rotation = Quaternion.LookRotation(Station(18) - Station(17));
        var audio = session.GetComponent<HauntExperience>();
        audio.ReplaceFigure(officer.transform, deputy.transform);
        var oldImitator = Object.FindFirstObjectByType<MazeAnomaly>();
        if (oldImitator != null)
        {
            Animator imitation = CorridorReviewBuilder.CreateMazeAnomaly(oldImitator.transform.position, oldImitator.transform.eulerAngles.y);
            imitation.gameObject.AddComponent<MazeAnomaly>().Configure(session.transform);
            audio.ReplaceFigure(oldImitator.transform, imitation.transform);
            Object.DestroyImmediate(oldImitator.gameObject);
        }

        var animator = officer.GetComponent<Animator>();
        animator.runtimeAnimatorController = GuardController();
        foreach (var skin in officer.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.sharedMaterials = skin.sharedMaterials.Select(material =>
                material.name.Contains("Trousers") || material.name.Contains("Top_shirt") ? DarkUniform(material) : material).ToArray();
        }

        var view = new GameObject("Guard view").transform;
        view.SetParent(officer.transform, false);
        view.localPosition = new Vector3(.14f, 1.48f, .23f);
        Light beam = CorridorReviewBuilder.AddLight("Guard flashlight", view.position, LightType.Spot, new Color(1f, .91f, .73f), 2.6f, 12f);
        beam.transform.SetParent(view, false);
        beam.transform.localPosition = Vector3.zero;
        beam.spotAngle = 44f;
        beam.innerSpotAngle = 21f;
        beam.shadows = LightShadows.None;
        beam.renderMode = LightRenderMode.ForcePixel;

        var torch = (GameObject)PrefabUtility.InstantiatePrefab(CorridorReviewBuilder.LoadEnvironment("small_plastic_torch"), view);
        torch.name = "Officer torch";
        Vector3 dimensions = CorridorReviewBuilder.ModelBounds(torch).size;
        torch.transform.localRotation = dimensions.x > dimensions.z && dimensions.x > dimensions.y
            ? Quaternion.Euler(0, 90, 0) : dimensions.y > dimensions.z ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        torch.transform.localPosition -= view.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(torch).center);
        torch.transform.localPosition += new Vector3(.12f, -.25f, .06f);
        var radio = (GameObject)PrefabUtility.InstantiatePrefab(CorridorReviewBuilder.LoadEnvironment("power_box_01"), officer.transform);
        radio.name = "Shoulder radio";
        radio.transform.localScale *= .19f;
        radio.transform.localPosition = new Vector3(-.17f, 1.43f, .1f) -
            officer.transform.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(radio).center);

        var body = officer.AddComponent<CharacterController>();
        body.height = 1.76f;
        body.radius = .24f;
        body.center = Vector3.up * .88f;
        officer.layer = 2;
        var destination = new GameObject("Guard navigation target").transform;
        destination.position = Station(17);
        var navigator = officer.AddComponent<MazeCompanion>();
        navigator.Configure(destination, animator, MazeLayout.Size, MazeLayout.Pitch, layout.CopyPassages(),
            Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None));
        navigator.MarkPatrol(session.GetComponent<CharacterController>());
        var holding = new GameObject("Holding checkpoint").transform;
        holding.position = Station(46);
        Vector3 wall = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, layout.Route[46]));
        CorridorReviewBuilder.PlaceRoomSign("HOLDING", holding.position + wall * 1.89f + Vector3.up * 2.2f,
            Quaternion.LookRotation(wall)).characterSize = .07f;

        AudioClip knock = Audio("metal-latch");
        var security = officer.AddComponent<MazeGuard>();
        security.Configure(session, view, destination, holding,
            new[] { Station(17), Station(23), Station(27), Station(20) },
            new[] { Station(65), Station(82), Station(99), Station(116), Station(110) },
            new[] { Station(137), Station(145), Station(153), Station(143) },
            AssetDatabase.LoadAssetAtPath<AudioClip>(Source + "PartnerAudio/run-06.wav"), Audio("guard-radio"));
        session.ConfigureGuard(security, Audio("partner-reply"));
        SetHoldingCheckpoints(layout, security);

        foreach (int station in new[] { 22, 68, 95, 115, 145 })
            PlaceDecoy(layout, station, knock);
        Debug.Log("Staged guard patrol, five pipe decoys and first-person arms.");
    }

    private static void SetHoldingCheckpoints(MazeLayout layout, MazeGuard guard)
    {
        var checkpoints = new[] { 20, 75, 104, 135, 151 }.Select(station =>
        {
            string label = $"Holding checkpoint {station}";
            var existing = GameObject.Find(label);
            if (existing != null)
                return existing.transform;
            int cell = layout.Route[station];
            Vector3 center = MazeLayout.Center(cell);
            Vector3 wall = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
            var marker = new GameObject(label).transform;
            marker.position = center;
            CorridorReviewBuilder.PlaceRoomSign("HOLDING", center + wall * 1.89f + Vector3.up * 2.2f,
                Quaternion.LookRotation(wall)).characterSize = .07f;
            return marker;
        }).ToArray();
        guard.ConfigureCheckpoints(checkpoints);
        EditorUtility.SetDirty(guard);
    }

    private static void PlaceDecoy(MazeLayout layout, int station, AudioClip knock)
    {
        int cell = layout.Route[station];
        Vector3 outward = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
        Vector3 position = MazeLayout.Center(cell) + outward * 1.75f;
        var root = new GameObject("Noise diversion pipe");
        root.transform.SetPositionAndRotation(position + Vector3.up * 1.35f, Quaternion.LookRotation(outward));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(CorridorReviewBuilder.LoadEnvironment("modular_industrial_pipes_01"), root.transform);
        Bounds bounds = CorridorReviewBuilder.ModelBounds(model);
        float scale = .82f / Mathf.Max(.82f, bounds.size.magnitude);
        model.transform.localScale *= scale;
        model.transform.localPosition -= root.transform.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(model).center);
        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(.92f, 1.08f, .28f);
        collider.isTrigger = true;
        root.AddComponent<PrisonDecoy>().Configure(knock);
        CorridorReviewBuilder.PlaceRoomSign("PIPE", position + Vector3.up * 2.01f, Quaternion.LookRotation(outward)).characterSize = .048f;
    }

    private static AudioClip Audio(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Source + "HauntAudio/" + name + ".wav");

    private static AnimatorController GuardController()
    {
        string path = Output + "Guard.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null)
            return controller;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(Source + "MazeCast/marcus_formal.glb").OfType<AnimationClip>().ToArray();
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var states = controller.layers[0].stateMachine;
        foreach (string name in new[] { "Idle", "Walk" })
        {
            var motion = Object.Instantiate(clips.Single(clip => clip.name == name));
            var settings = AnimationUtility.GetAnimationClipSettings(motion);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(motion, settings);
            AssetDatabase.CreateAsset(motion, Output + "Guard " + name + ".anim");
            AnimatorState state = states.AddState(name);
            state.motion = motion;
            if (name == "Idle")
                states.defaultState = state;
        }
        return controller;
    }

    private static Material DarkUniform(Material original)
    {
        string path = Output + "Guard " + original.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(original);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(.29f, .32f, .37f));
        if (material.HasProperty("baseColorFactor"))
            material.SetColor("baseColorFactor", new Color(.29f, .32f, .37f));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
