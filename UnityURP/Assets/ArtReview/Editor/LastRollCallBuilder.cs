using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class LastRollCallBuilder
{
    public static void ReleaseWindows()
    {
        Stage();
        LastRollCallVerification.Verify();
        MazeReviewBuilder.Build();
    }
    public static void PoseReview()
    {
        foreach (float roll in new[] { 0f, -65f, 65f })
        {
            PrisonHandsBuilder.GripRoll = roll;
            Stage();
            File.Copy("Logs/LastRollCall-hands.png", "Logs/Grip-" + roll + ".png", true);
        }
    }
    public static void Stage()
    {
        var scene = EditorSceneManager.OpenScene(MazeReviewBuilder.ScenePath);
        var session = Object.FindFirstObjectByType<MazeSession>();
        var camera = session.GetComponentInChildren<Camera>();
        var torch = camera.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Held flashlight");
        torch.SetParent(camera.transform, true);
        // Remove the previous rig, preserving the referenced flashlight object.
        var previous = camera.GetComponent<PrisonHands>();
        if (previous != null) Object.DestroyImmediate(previous);
        camera.GetUniversalAdditionalCameraData().cameraStack.Clear();
        foreach (string name in new[] { "Left first-person hand", "Right first-person hand", "Hands render camera", "Hands soft light" })
        {
            var old = camera.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        torch.localRotation = Quaternion.identity;
        Vector3 dimensions = CorridorReviewBuilder.ModelBounds(torch.gameObject).size;
        torch.localRotation = dimensions.x > dimensions.z && dimensions.x > dimensions.y ? Quaternion.Euler(0, 90, 0)
            : dimensions.y > dimensions.z ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        torch.localPosition = Vector3.zero;
        torch.localPosition += new Vector3(.24f, -.27f, .55f) - camera.transform.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(torch.gameObject).center);
        PrisonHandsBuilder.Stage(camera, torch);
        PrisonHandsBuilder.SetupOverlay(camera);
        foreach (Light light in camera.GetComponentsInChildren<Light>(true)) light.cullingMask &= ~(1 << 29);
        var fill = new GameObject("Hands soft light").AddComponent<Light>();
        fill.transform.SetParent(camera.transform, false);
        fill.transform.localPosition = new Vector3(-.15f, .1f, .1f);
        fill.type = LightType.Point;
        fill.range = 1.1f;
        fill.intensity = .3f;
        fill.cullingMask = 1 << 29;
        fill.shadows = LightShadows.None;
        foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            skin.quality = SkinQuality.Bone4;
        foreach (string actor in new[] { "Partner", "The Imitator" })
            CharacterCoverage.Apply(GameObject.Find(actor));
        var layout = MazeLayout.Create();
        if (GameObject.Find("Last roll call dressing") == null) Dress(layout);
        RefineSurfaces();
        StageMission(layout, session);
        DistrictBuilder.Stage(layout, session);
        ActorMotionBuilder.Stage();
        RealRoomsBuilder.Stage(layout,session);
        PresentationBuilder.Stage(session);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("PASS Last Roll Call art staging and hand replacement.");
    }

    private static void RefineSurfaces()
    {
        foreach (string name in new[] { "Walls", "Floor" })
        {
            string path = "Assets/ArtReview/HauntCoop/LastCall-" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Expanded/" + name + ".mat"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_BumpScale", name == "Walls" ? .3f : .45f);
            mat.SetFloat("_Smoothness", name == "Walls" ? .22f : .38f);
            mat.SetColor("_BaseColor", name == "Walls" ? new Color(.8f, .81f, .78f) : new Color(.7f, .72f, .72f));
            GameObject.Find(name == "Walls" ? "Maze concrete shell" : "Maze slab").GetComponent<Renderer>().sharedMaterial = mat;
            EditorUtility.SetDirty(mat);
        }
        foreach (TextMesh text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            text.characterSize = Mathf.Min(text.characterSize, .032f);
            text.color = new Color(.6f, .64f, .62f);
        }
    }

    private static void StageMission(MazeLayout layout, MazeSession session)
    {
        if (session.GetComponent<LastRollCall>() != null)
        {
            if (session.GetComponent<TransferInterlock>() == null) ConfigureAlternates(layout, session.GetComponent<LastRollCall>());
            return;
        }
        var mission = session.gameObject.AddComponent<LastRollCall>();
        int[] positions = { 2, 36, 78, 154, 52 };
        string[] transfer = { "ROLL CALL / HOLD F", "TRANSFER PASS", "VALIDATE PASS", "RELEASE TRANSFER GATE", "SUPPLY CACHE / NOISY" };
        string[] maintenance = { "ROLL CALL / HOLD F", "MAINTENANCE TOOL", "ISOLATE STEAM", "RELEASE SERVICE GATE", "SUPPLY CACHE / NOISY" };
        mission.stations = new EscapeStation[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            int cell = layout.Route[positions[i]];
            Vector3 wall = MazeReviewBuilder.Direction(MazeReviewBuilder.WallSide(layout, cell));
            Vector3 point = MazeLayout.Center(cell) + wall * 1.72f;
            var root = new GameObject("Escape station " + i);
            root.transform.SetPositionAndRotation(point + Vector3.up * 1.3f, Quaternion.LookRotation(-wall));
            var panel = Object.Instantiate(CorridorReviewBuilder.LoadEnvironment("power_box_01"), root.transform);
            Bounds bounds = CorridorReviewBuilder.ModelBounds(panel);
            panel.transform.localScale *= .55f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            panel.transform.position += root.transform.position - CorridorReviewBuilder.ModelBounds(panel).center;
            var trigger = root.AddComponent<BoxCollider>();
            trigger.size = new Vector3(.8f, .8f, .4f);
            trigger.isTrigger = true;
            var station = root.AddComponent<EscapeStation>();
            station.index = i;
            station.transferLabel = transfer[i];
            station.maintenanceLabel = maintenance[i];
            station.label = CorridorReviewBuilder.PlaceRoomSign(transfer[i], point + Vector3.up * 1.95f, Quaternion.LookRotation(wall));
            station.label.characterSize = .045f;
            mission.stations[i] = station;
        }
        ConfigureAlternates(layout, mission);
    }

    private static void ConfigureAlternates(MazeLayout layout, LastRollCall mission)
    {
        foreach (var station in mission.stations)
        {
            station.label.transform.SetParent(station.transform, true);
            if (station.index != 1 && station.index != 2) continue;
            int target = station.index == 1 ? 36 : 78;
            var options = layout.Route.Skip(target - 7).Take(15).SelectMany(cell => Enumerable.Range(0, 4)
                .Where(side => layout.IsOpen(cell, side)).Select(side => MazeLayout.Neighbor(cell, side)))
                .Where(cell => cell >= 0 && !layout.Route.Contains(cell)).Distinct().ToArray();
            if (options.Length == 0) continue;
            int alternate = options.OrderBy(cell => (MazeLayout.Center(cell) - station.transform.position).sqrMagnitude).First();
            int wallSide = MazeReviewBuilder.WallSide(layout, alternate);
            if (layout.IsOpen(alternate, wallSide)) continue;
            Vector3 wall = MazeReviewBuilder.Direction(wallSide);
            station.alternateLocation = true;
            station.maintenancePosition = MazeLayout.Center(alternate) + wall * 1.72f + Vector3.up * 1.3f;
            station.maintenanceRotation = Quaternion.LookRotation(-wall);
        }
    }

    private static void Dress(MazeLayout layout)
    {
        var root = new GameObject("Last roll call dressing").transform;
        string[] zones = { "A / OLD CELLS", "B / DUTY OFFICE", "C / LAUNDRY", "D / MAINTENANCE", "E / TRANSFER" };
        for (int station = 0; station < layout.Route.Count; station += 7)
        {
            int cell = layout.Route[station];
            int wall = MazeReviewBuilder.WallSide(layout, cell);
            if (layout.IsOpen(cell, wall)) continue;
            Vector3 outward = MazeReviewBuilder.Direction(wall);
            Vector3 mount = MazeLayout.Center(cell) + outward * 1.86f;
            var sign = CorridorReviewBuilder.PlaceRoomSign(zones[Mathf.Min(4, station / 33)] + "\n" + (station + 100),
                mount + Vector3.up * 2.16f, Quaternion.LookRotation(outward));
            sign.characterSize = .042f;
            sign.color = new Color(.67f, .7f, .68f);
            sign.transform.SetParent(root);
            string asset = station % 14 == 0 ? "modular_industrial_pipes_01" : "power_box_01";
            var prop = Object.Instantiate(CorridorReviewBuilder.LoadEnvironment(asset), root);
            prop.transform.rotation = Quaternion.LookRotation(-outward);
            Bounds bounds = CorridorReviewBuilder.ModelBounds(prop);
            float scale = .85f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            prop.transform.localScale *= scale;
            prop.transform.position += mount + Vector3.up * (asset == "power_box_01" ? 1.4f : 2.52f) - CorridorReviewBuilder.ModelBounds(prop).center;
        }
        RenderSettings.fogDensity = .019f;
        RenderSettings.ambientLight = new Color(.145f, .16f, .175f);
    }

    public static void Capture()
    {
        var player = Object.FindFirstObjectByType<MazeSession>();
        if (player == null) { EditorSceneManager.OpenScene(MazeReviewBuilder.ScenePath); player = Object.FindFirstObjectByType<MazeSession>(); }
        var camera = player.GetComponentInChildren<Camera>();
        var controls = player.GetComponent<CorridorReview>();
        typeof(CorridorReview).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controls, null);
        camera.GetComponentInChildren<Light>(true).enabled = true;
        foreach (var t in camera.GetComponentsInChildren<Transform>(true))
            if (t.name == "Held flashlight") t.gameObject.SetActive(true);
        Save(camera, "LastRollCall-hands");
        var layout = MazeLayout.Create();
        player.transform.position = MazeLayout.Center(layout.Route[24]) + Vector3.up * .08f;
        player.transform.rotation = Quaternion.LookRotation(MazeLayout.Center(layout.Route[25]) - MazeLayout.Center(layout.Route[24]));
        Save(camera, "LastRollCall-corridor");
        for (int i = 0; i < layout.Districts.Count; i++)
        {
            camera.transform.localRotation=Quaternion.identity;
            var cells = layout.Districts[i];
            Vector3 middle = cells.Select(MazeLayout.Center).Aggregate(Vector3.zero, (a,b) => a+b) / cells.Length;
            player.transform.position = MazeLayout.Center(cells[0]) + Vector3.up * .08f;
            player.transform.rotation = Quaternion.LookRotation(middle - MazeLayout.Center(cells[0]));
            typeof(CorridorReview).GetMethod("SetPower", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controls, new object[] { true });
            Save(camera, "District-" + i);
        }
    }

    private static void Save(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
        Object.DestroyImmediate(pixels);
        Object.DestroyImmediate(target);
    }
}
