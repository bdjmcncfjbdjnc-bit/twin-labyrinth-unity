using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MazeReviewBuilder
{
    internal const string ScenePath = "Assets/ArtReview/HauntCoop/PrisonCoop.unity";
    private const string Output = "Assets/ArtReview/HauntCoop";
    private const string Materials = "Assets/ArtReview/Expanded/";
    private const string Source = "Assets/ArtReview/Source/";
    private const string Cast = Source + "MazeCast/";

    [MenuItem("Twin Labyrinth/Create Partner Protocol")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("The maze scene already exists.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!AssetDatabase.IsValidFolder(Output))
            AssetDatabase.CreateFolder("Assets/ArtReview", "HauntCoop");

        MazeLayout layout = MazeLayout.Create();
        var floor = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Floor.mat");
        var walls = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Walls.mat");
        var steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Steel.mat");
        if (floor == null || walls == null || steel == null)
            throw new InvalidOperationException("Generate the Cell Block materials before creating the maze.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(.025f, .031f, .037f);
        RenderSettings.fogDensity = .027f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.19f, .20f, .21f);
        RenderSettings.reflectionIntensity = 0f;

        MakeArchitecture(layout, floor, walls);
        PlaceGates(layout, steel, walls);
        PlaceMarker(layout, MazeLayout.Start, "INTAKE / START");
        PlaceMarker(layout, layout.Exit, "EXIT / SURFACE ACCESS");
        PlaceLandmarks(layout);
        var fixtures = new List<Light>();
        var lampMeshes = new List<Renderer>();
        Light emergency = LightMaze(layout, fixtures, lampMeshes);
        PlaceCells(layout, steel, walls, fixtures);

        Transform orderly = AddRocketbox("marcus_formal", "Intake orderly", layout, 9);
        Transform medic = AddRocketbox("hannah_casual", "Infirmary worker", layout, 60);
        Transform clerk = AddRocketbox("kenji_formal", "Records clerk", layout, 112);
        Vector3 anomalyPosition = ActorPosition(layout, 31, out float anomalyYaw);
        Animator anomaly = CorridorReviewBuilder.CreateMazeAnomaly(anomalyPosition, anomalyYaw);
        GameObject player = CorridorReviewBuilder.BuildPlayer(fixtures.ToArray(), lampMeshes.ToArray(), emergency, anomaly);
        Vector3 start = MazeLayout.Center(MazeLayout.Start);
        player.transform.SetPositionAndRotation(start + Vector3.up * .08f,
            Quaternion.LookRotation(MazeLayout.Center(layout.Route[1]) - start));
        var session = player.AddComponent<MazeSession>();
        session.Configure(MazeLayout.Center(layout.Exit));
        session.ConfigureExitDoors(PlaceExitDoors(layout, steel, walls));
        Animator partner = CorridorReviewBuilder.CreateMazePartner(
            start - player.transform.forward * 1.05f + player.transform.right * .55f,
            player.transform.eulerAngles.y);
        var capsule = partner.gameObject.AddComponent<CharacterController>();
        capsule.radius = .23f;
        capsule.height = 1.7f;
        capsule.center = Vector3.up * .85f;
        partner.gameObject.layer = 2;
        var companion = partner.gameObject.AddComponent<MazeCompanion>();
        companion.Configure(player.transform, partner, MazeLayout.Size, MazeLayout.Pitch, layout.CopyPassages(),
            UnityEngine.Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None));
        session.ConfigurePartner(companion);
        anomaly.gameObject.AddComponent<MazeAnomaly>().Configure(player.transform);
        HauntSceneSetup.AddExperience(layout, player, steel, new[] { orderly, medic, clerk, anomaly.transform });
        HauntSceneSetup.SetPartnerAudio(player.GetComponent<HauntExperience>());
        HauntSceneSetup.PlaceRelays(layout);
        PrisonGuardBuilder.Stage(layout, session);
        PrisonIncidentBuilder.Stage(layout, session);
        PrisonInmateBuilder.Stage(session);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save the prison maze.");
        if (!EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Maze generated: seed {layout.Seed}, {MazeLayout.Size * MazeLayout.Size} cells, {layout.Loops} loops, " +
            $"{layout.Route.Count - 1} steps, {(layout.Route.Count - 1) * MazeLayout.Pitch:F0} metres centre-line.");
    }

    [MenuItem("Twin Labyrinth/Update Partner Audio")]
    public static void UpdateAudio()
    {
        if (!File.Exists(ScenePath))
        {
            Create();
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath);
        var experience = UnityEngine.Object.FindFirstObjectByType<HauntExperience>();
        HauntSceneSetup.SetPartnerAudio(experience);
        EditorUtility.SetDirty(experience);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save Partner Protocol audio.");
    }

    [MenuItem("Twin Labyrinth/Stage Guard and Hands")]
    public static void StageGuard()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var session = UnityEngine.Object.FindFirstObjectByType<MazeSession>();
        MazeLayout layout = MazeLayout.Create();
        PrisonGuardBuilder.Stage(layout, session);
        PrisonIncidentBuilder.Stage(layout, session);
        PrisonInmateBuilder.Stage(session);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save the prison guard and first-person hands.");
    }

    public static void Build()
    {
        if (!File.Exists(ScenePath))
            Create();
        // Verification deliberately changes live objects. Build only the saved playable scene.
        EditorSceneManager.OpenScene(ScenePath);
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string path = target switch
        {
            BuildTarget.StandaloneWindows64 => "Builds/Blackout-Windows/TwinLabyrinth-Blackout.exe",
            BuildTarget.WebGL => "Builds/Blackout-Web",
            _ => throw new BuildFailedException("Switch to Windows x64 or Web.")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PlayerSettings.productName = "双生迷城 · 最后一次点名";
        BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, path, target, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Maze build failed: " + report.summary.result);
        string notices = target == BuildTarget.WebGL ? path : Path.GetDirectoryName(path);
        File.Copy(Cast + "Rocketbox-Notices.txt", Path.Combine(notices, "Rocketbox-Notices.txt"), true);
    }

    internal static void MakeArchitecture(MazeLayout layout, Material floor, Material walls)
    {
        float width = MazeLayout.Size * MazeLayout.Pitch;
        MakeShell("Maze slab", floor, new[] { (new Vector3(width / 2, -.14f, width / 2), new Vector3(width, .28f, width)) });
        var blocks = new List<(Vector3 position, Vector3 size)>
        {
            (new Vector3(width / 2, 2.92f, width / 2), new Vector3(width, .24f, width))
        };
        for (int cell = 0; cell < MazeLayout.Size * MazeLayout.Size; cell++)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                int neighbor = MazeLayout.Neighbor(cell, direction);
                if (layout.IsOpen(cell, direction) || (neighbor >= 0 && neighbor < cell))
                    continue;
                Vector3 outward = Direction(direction);
                Vector3 center = MazeLayout.Center(cell) + outward * (MazeLayout.Pitch / 2) + Vector3.up * 1.4f;
                Vector3 size = direction % 2 == 0
                    ? new Vector3(.25f, 2.8f, MazeLayout.Pitch + .24f)
                    : new Vector3(MazeLayout.Pitch + .24f, 2.8f, .25f);
                blocks.Add((center, size));
            }
        }
        MakeShell("Maze concrete shell", walls, blocks);
    }

    private static void MakeShell(string label, Material material, IEnumerable<(Vector3 position, Vector3 size)> blocks)
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cube = template.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] sourceVertices = cube.vertices;
        Vector3[] sourceNormals = cube.normals;
        int[] sourceTriangles = cube.triangles;
        UnityEngine.Object.DestroyImmediate(template);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (var block in blocks)
        {
            int offset = vertices.Count;
            foreach (int index in sourceTriangles)
                triangles.Add(offset + index);
            for (int index = 0; index < sourceVertices.Length; index++)
            {
                Vector3 position = Vector3.Scale(sourceVertices[index], block.size) + block.position;
                Vector3 normal = sourceNormals[index];
                vertices.Add(position);
                normals.Add(normal);
                var uv = Mathf.Abs(normal.y) > .5f ? new Vector2(position.x, position.z) :
                    Mathf.Abs(normal.x) > .5f ? new Vector2(position.z, position.y) : new Vector2(position.x, position.y);
                uvs.Add(uv * .5f);
            }
        }
        var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "/" + label + ".asset");
        if (existing == null) AssetDatabase.CreateAsset(mesh, Output + "/" + label + ".asset");
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
        var shell = new GameObject(label);
        shell.AddComponent<MeshFilter>().sharedMesh = mesh;
        shell.AddComponent<MeshRenderer>().sharedMaterial = material;
        shell.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void PlaceGates(MazeLayout layout, Material steel, Material wall)
    {
        var front = CorridorReviewBuilder.LoadModel(Source + "Environment/dungeon_cell_bar_front.glb");
        int[] stations = { 18, 45, 76, 108, 139 };
        for (int index = 0; index < stations.Length; index++)
        {
            Vector3 from = MazeLayout.Center(layout.Route[stations[index] - 1]);
            Vector3 to = MazeLayout.Center(layout.Route[stations[index]]);
            float yaw = Quaternion.LookRotation(to - from).eulerAngles.y;
            CorridorReviewBuilder.AddEntrance(front, steel, wall, $"CHECKPOINT {index + 1:00}", (from + to) / 2f, yaw, MazeLayout.Pitch);
        }
    }

    private static void PlaceCells(MazeLayout layout, Material steel, Material wall, List<Light> fixtures)
    {
        var front = CorridorReviewBuilder.LoadModel(Source + "Environment/dungeon_cell_bar_front.glb");
        var bed = CorridorReviewBuilder.LoadEnvironment("old_bed_frame");
        int[] distances = layout.Trace(out _);
        var roomOptions = Enumerable.Range(0, distances.Length).Where(cell => cell != layout.Exit &&
            cell != MazeLayout.Start && Enumerable.Range(0, 4).Count(direction => layout.IsOpen(cell, direction)) == 1).ToList();
        int index = 0;
        foreach (int distance in Enumerable.Range(0, 16).Select(index => 12 + 9 * index))
        {
            int cell = roomOptions.OrderBy(candidate => Math.Abs(distances[candidate] - distance)).First();
            roomOptions.Remove(cell);
            int direction = Enumerable.Range(0, 4).Single(side => layout.IsOpen(cell, side));
            Vector3 outward = Direction(direction);
            Vector3 center = MazeLayout.Center(cell);
            float inwardYaw = Quaternion.LookRotation(-outward).eulerAngles.y;
            CorridorReviewBuilder.AddEntrance(front, steel, wall, $"CELL A{++index:00}",
                center + outward * (MazeLayout.Pitch * .5f), inwardYaw, MazeLayout.Pitch);
            CorridorReviewBuilder.PlaceFloorProp(bed, center - outward * 1.31f, inwardYaw + 90f);
            Light light = CorridorReviewBuilder.AddLight("Cell " + index + " fixture", center - outward * 1.2f + Vector3.up * 2.3f,
                LightType.Point, new Color(.66f, .73f, .81f), .42f, 4.6f);
            light.shadows = LightShadows.None;
            fixtures.Add(light);
        }
    }

    private static ReviewDoor[] PlaceExitDoors(MazeLayout layout, Material steel, Material walls)
    {
        var front = CorridorReviewBuilder.LoadModel(Source + "Environment/dungeon_cell_bar_front.glb");
        var doors = new List<ReviewDoor>();
        for (int direction = 0; direction < 4; direction++)
        {
            if (!layout.IsOpen(layout.Exit, direction))
                continue;
            Vector3 outward = Direction(direction);
            Vector3 position = MazeLayout.Center(layout.Exit) + outward * (MazeLayout.Pitch / 2f);
            ReviewDoor door = CorridorReviewBuilder.AddEntrance(front, steel, walls, "EXIT GATE", position,
                Quaternion.LookRotation(-outward).eulerAngles.y, MazeLayout.Pitch);
            door.Lock();
            doors.Add(door);
        }
        return doors.ToArray();
    }

    private static void PlaceLandmarks(MazeLayout layout)
    {
        var bed = CorridorReviewBuilder.LoadEnvironment("old_bed_frame");
        var rack = CorridorReviewBuilder.LoadEnvironment("worn_metal_rack");
        var box = CorridorReviewBuilder.LoadEnvironment("power_box_01");
        foreach (var station in new[] { (24, "INFIRMARY", bed), (64, "SUPPLY", rack), (102, "RECORDS", rack) })
        {
            int cell = layout.Route[station.Item1];
            int side = WallSide(layout, cell);
            Vector3 outward = Direction(side);
            float yaw = Quaternion.LookRotation(outward).eulerAngles.y;
            CorridorReviewBuilder.PlaceFloorProp(station.Item3, MazeLayout.Center(cell) + outward * 1.22f, yaw);
            PlaceMarker(layout, cell, station.Item2);
        }
        int serviceCell = layout.Route[125];
        Vector3 wallNormal = Direction(WallSide(layout, serviceCell));
        GameObject transformer = (GameObject)PrefabUtility.InstantiatePrefab(box);
        transformer.transform.SetPositionAndRotation(MazeLayout.Center(serviceCell) + wallNormal * 1.82f + Vector3.up * 1.35f,
            Quaternion.LookRotation(-wallNormal));
        PlaceMarker(layout, serviceCell, "POWER SUBSTATION");
    }

    private static Light LightMaze(MazeLayout layout, List<Light> fixtures, List<Renderer> lampMeshes)
    {
        var lamp = CorridorReviewBuilder.LoadEnvironment("industrial_wall_lamp");
        var stations = new SortedSet<int>(layout.Route.Where((_, index) => index % 4 == 0));
        for (int cell = 5; cell < MazeLayout.Size * MazeLayout.Size; cell += 8)
            stations.Add(cell);
        foreach (int cell in stations)
        {
            int side = WallSide(layout, cell);
            Vector3 outward = Direction(side);
            Vector3 mount = MazeLayout.Center(cell) + outward * 1.85f + Vector3.up * 2.25f;
            CorridorReviewBuilder.AddFixture(lamp, mount, Quaternion.LookRotation(-outward).eulerAngles.y, -outward, fixtures, lampMeshes);
            if (cell != MazeLayout.Start && cell % 6 != 0)
                fixtures[^1].shadows = LightShadows.None;
        }
        Vector3 exit = MazeLayout.Center(layout.Exit);
        Light marker = CorridorReviewBuilder.AddLight("Exit beacon", exit + Vector3.up * 2.3f, LightType.Point,
            new Color(.57f, .83f, .72f), 1.2f, 6f);
        marker.shadows = LightShadows.None;
        Light emergency = CorridorReviewBuilder.AddLight("Intake emergency lamp", MazeLayout.Center(MazeLayout.Start) + Vector3.up * 2.4f,
            LightType.Point, new Color(.73f, .82f, 1f), .95f, 7f);
        emergency.enabled = false;
        return emergency;
    }

    private static void PlaceMarker(MazeLayout layout, int cell, string text)
    {
        Vector3 outward = Direction(WallSide(layout, cell));
        Vector3 position = MazeLayout.Center(cell) + outward * 1.86f + Vector3.up * 2.16f;
        CorridorReviewBuilder.PlaceRoomSign(text, position, Quaternion.LookRotation(outward));
    }

    internal static int WallSide(MazeLayout layout, int cell)
    {
        for (int side = 0; side < 4; side++)
            if (!layout.IsOpen(cell, side))
                return side;
        // At four-way junctions, hang a sign or fixture below the ceiling.
        return 0;
    }

    private static Vector3 ActorPosition(MazeLayout layout, int station, out float yaw)
    {
        int cell = layout.Route[station];
        Vector3 center = MazeLayout.Center(cell);
        Vector3 approach = MazeLayout.Center(layout.Route[station - 1]) - center;
        yaw = Quaternion.LookRotation(approach).eulerAngles.y;
        return center + Direction(WallSide(layout, cell)) * 1.12f;
    }

    private static Transform AddRocketbox(string asset, string label, MazeLayout layout, int station)
    {
        string path = Cast + asset + ".glb";
        GameObject prefab = CorridorReviewBuilder.LoadModel(path);
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        actor.name = label;
        actor.transform.SetPositionAndRotation(ActorPosition(layout, station, out float yaw), Quaternion.Euler(0, yaw, 0));
        var animator = actor.GetComponent<Animator>() ?? actor.AddComponent<Animator>();
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(clip => clip.name == "Idle");
        var loop = UnityEngine.Object.Instantiate(idle);
        var settings = AnimationUtility.GetAnimationClipSettings(loop);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loop, settings);
        AssetDatabase.CreateAsset(loop, Output + "/" + asset + " Idle.anim");
        animator.runtimeAnimatorController = AnimatorController.CreateAnimatorControllerAtPathWithClip(Output + "/" + asset + ".controller", loop);
        animator.applyRootMotion = false;
        return actor.transform;
    }

    internal static Vector3 Direction(int direction)
    {
        Vector2Int cardinal = MazeLayout.Directions[direction];
        return new Vector3(cardinal.x, 0, cardinal.y);
    }
}
