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
using UnityEngine.Rendering.Universal;

public static class CorridorReviewBuilder
{
    private const string Root = "Assets/ArtReview";
    private const string Source = Root + "/Source";
    private const string Output = Root + "/Expanded";
    private const string ScenePath = Output + "/CellBlockReview.unity";
    private const string LampModel = Source + "/Environment/industrial_wall_lamp/industrial_wall_lamp_1k.gltf";
    private const string PowerBoxModel = Source + "/Environment/power_box_01/power_box_01_1k.gltf";
    private const string CellFrontModel = Source + "/Environment/dungeon_cell_bar_front.glb";
    private const string CharacterRoot = Source + "/Characters/";
    private const string BodyModel = CharacterRoot + "base_body_anim.glb";

    [MenuItem("Twin Labyrinth/Create Expanded Cell Block")]
    public static void CreateReview()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("A review scene already exists. Open it to continue work.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder();
        ConfigurePipeline();
        GameObject lampPrefab = LoadModel(LampModel);
        GameObject boxPrefab = LoadModel(PowerBoxModel);
        GameObject cellFront = LoadModel(CellFrontModel);
        Material floor = CreateMaterial("concrete_floor_02", "Floor", .82f);
        Material concrete = CreateMaterial("dirty_concrete", "Walls", .67f);
        Material metal = CreateMaterial("rusty_metal_sheet", "Steel", .72f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(.025f, .031f, .037f);
        RenderSettings.fogDensity = .029f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.14f, .15f, .16f);
        RenderSettings.reflectionIntensity = 0f;

        BuildStructure(floor, concrete, metal, cellFront);
        var bed = LoadEnvironment("old_bed_frame");
        var rack = LoadEnvironment("worn_metal_rack");
        for (int i = 0; i < 6; i++)
            PlaceFloorProp(bed, new Vector3(4.6f, 0, 4.65f + 4 * i), 90);
        PlaceFloorProp(rack, new Vector3(-5.2f, 0, 22.7f), 180);
        PlaceFloorProp(rack, new Vector3(-10.7f, 0, 10.1f), 0);
        var fixtures = new List<Light>();
        var lampMeshes = new List<Renderer>();
        for (int i = 0; i < 6; i++)
        {
            float z = 2.8f + i * 4.8f;
            if (z > 13f && z < 17f)
                z = 17.7f;
            AddFixture(lampPrefab, new Vector3(-1.68f, 2.27f, z), 90, Vector3.right, fixtures, lampMeshes);
        }
        AddFixture(lampPrefab, new Vector3(-10.1f, 2.27f, 16.92f), 180, Vector3.back, fixtures, lampMeshes);
        foreach (Vector3 position in new[] { new Vector3(-5.2f, 2.35f, 22), new Vector3(-10.6f, 2.35f, 22),
                     new Vector3(-10.6f, 2.35f, 10), new Vector3(4.7f, 2.35f, 9), new Vector3(4.7f, 2.35f, 21) })
        {
            Light fill = AddLight("Room fixture", position, LightType.Point, new Color(.77f, .83f, .9f), .55f, 4.7f);
            fill.shadows = LightShadows.None;
            fixtures.Add(fill);
        }

        PlaceModel(boxPrefab, new Vector3(-4.2f, 1.33f, 23.32f), Quaternion.Euler(0, 180, 0));
        PlaceModel(boxPrefab, new Vector3(1.66f, 1.33f, 28), Quaternion.Euler(0, -90, 0));
        Light emergency = AddLight("Emergency light", new Vector3(-.6f, 2.2f, 15), LightType.Point,
            new Color(.73f, .82f, 1f), .9f, 6.5f);
        emergency.enabled = false;
        Animator prisoner = BuildCharacters();
        BuildPlayer(fixtures.ToArray(), lampMeshes.ToArray(), emergency, prisoner);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save " + ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    public static void BuildReview()
    {
        if (!File.Exists(ScenePath))
            CreateReview();

        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool sceneUpdated = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out TextMesh sign))
                sceneUpdated |= AssignSignMaterial(sign);
            if (root.TryGetComponent<Animator>(out _))
                sceneUpdated |= CharacterCoverage.Apply(root);
        }
        if (sceneUpdated && !EditorSceneManager.SaveScene(scene))
            throw new BuildFailedException("Could not save the revised scene.");

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string outputPath = target switch
        {
            BuildTarget.StandaloneWindows64 => "Builds/CellBlock-Windows/TwinLabyrinth-CellBlock.exe",
            BuildTarget.WebGL => "Builds/Web",
            _ => throw new BuildFailedException("Switch to Windows x64 or Web before building the review.")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        BuildReport result = BuildPipeline.BuildPlayer(new[] { ScenePath }, outputPath, target, BuildOptions.None);
        if (result.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Corridor review build failed: " + result.summary.result);
    }

    internal static GameObject LoadModel(string path)
    {
        // Refresh materials with the newly assigned render pipeline.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return model != null ? model : throw new InvalidOperationException("Import staged source before building: " + path);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Output))
            AssetDatabase.CreateFolder(Root, "Expanded");
    }

    private static void ConfigurePipeline()
    {
        const string path = Output + "/CorridorURP.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipeline == null)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Output + "/CorridorRenderer.asset");
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, path);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var settings = new SerializedObject(pipeline);
        settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
        settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
        settings.FindProperty("m_ShadowDistance").floatValue = 24f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }

    private static Material CreateMaterial(string stem, string label, float smoothnessScale)
    {
        string textureRoot = Source + "/Materials/" + stem;
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_Diffuse.jpg");
        string normalPath = textureRoot + "_nor_gl.jpg";
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit is unavailable.");
        var material = new Material(shader);
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.SetTexture("_MetallicGlossMap", PackSmoothness(textureRoot + "_Rough.jpg", label, smoothnessScale));
        material.SetFloat("_Smoothness", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        AssetDatabase.CreateAsset(material, Output + "/" + label + ".mat");
        return material;
    }

    private static Texture2D PackSmoothness(string path, string label, float scale)
    {
        var packed = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!ImageConversion.LoadImage(packed, File.ReadAllBytes(path)))
            throw new InvalidDataException("Could not decode roughness texture: " + path);

        Color32[] pixels = packed.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt((255 - pixels[i].r) * scale));

        // JPEG decoding can switch the texture to RGB24, so pack into RGBA32 before encoding.
        var rgba = new Texture2D(packed.width, packed.height, TextureFormat.RGBA32, false, true);
        rgba.SetPixels32(pixels);
        rgba.Apply();
        string packedPath = Output + "/" + label + "_Smoothness.png";
        File.WriteAllBytes(packedPath, ImageConversion.EncodeToPNG(rgba));
        UnityEngine.Object.DestroyImmediate(packed);
        UnityEngine.Object.DestroyImmediate(rgba);
        AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(packedPath);
        importer.sRGBTexture = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
    }

    private static void BuildStructure(Material floor, Material wall, Material steel, GameObject cellFront)
    {
        Deck("Main corridor", -1.8f, 1.8f, 0, 30, floor, wall);
        Deck("Service branch", -12.8f, -1.8f, 13, 17, floor, wall);
        WallX(-1.8f, 0, 13, wall);
        WallX(-1.8f, 17, 30, wall);
        WallX(1.8f, 0, 3, wall);
        WallX(1.8f, 27, 30, wall);
        WallX(-12.8f, 13, 17, wall);
        WallZ(0, -1.8f, 1.8f, wall);
        WallZ(30, -1.8f, 1.8f, wall);
        WallZ(13, -8.4f, -1.8f, wall);
        WallZ(17, -4, -1.8f, wall);
        Surface("Steel end panel", new Vector3(0, 1.08f, 29.86f), new Vector3(1.3f, 2.16f, .06f), steel);

        for (int i = 0; i < 6; i++)
        {
            float start = 3f + 4f * i;
            Deck("Cell " + (i + 1), 1.8f, 5.7f, start, start + 4f, floor, wall);
            WallX(5.7f, start, start + 4f, wall);
            WallZ(start, 1.8f, 5.7f, wall);
            AddEntrance(cellFront, steel, wall, "CELL 0" + (i + 1), new Vector3(1.8f, 0, start + 2), 90, 4);
        }
        WallZ(27, 1.8f, 5.7f, wall);

        AddServiceRoom("STORAGE", -12.8f, -8.4f, 17, 23.4f, true, floor, wall, steel, cellFront);
        AddServiceRoom("ELECTRICAL", -8.4f, -4, 17, 23.4f, true, floor, wall, steel, cellFront);
        AddServiceRoom("STAFF", -12.8f, -8.4f, 9, 13, false, floor, wall, steel, cellFront);
    }

    private static void AddServiceRoom(string label, float left, float right, float start, float end, bool north,
        Material floor, Material wall, Material steel, GameObject cellFront)
    {
        Deck(label, left, right, start, end, floor, wall);
        WallX(left, start, end, wall);
        // The two north rooms share a partition.
        if (label != "STORAGE")
            WallX(right, start, end, wall);
        WallZ(north ? end : start, left, right, wall);
        AddEntrance(cellFront, steel, wall, label, new Vector3((left + right) * .5f, 0, north ? start : end),
            north ? 0 : 180, right - left);
    }

    private static void Deck(string name, float left, float right, float start, float end, Material floor, Material wall)
    {
        Vector3 center = new Vector3((left + right) * .5f, 0, (start + end) * .5f);
        Surface(name + " floor", center + Vector3.down * .13f, new Vector3(right - left, .26f, end - start), floor);
        Surface(name + " ceiling", center + Vector3.up * 2.91f, new Vector3(right - left, .22f, end - start), wall);
    }

    private static void WallX(float x, float start, float end, Material wall) =>
        Surface("Concrete wall", new Vector3(x, 1.4f, (start + end) * .5f), new Vector3(.2f, 2.8f, end - start), wall);

    private static void WallZ(float z, float left, float right, Material wall) =>
        Surface("Concrete partition", new Vector3((left + right) * .5f, 1.4f, z), new Vector3(right - left, 2.8f, .2f), wall);

    internal static ReviewDoor AddEntrance(GameObject prefab, Material steel, Material wall, string label, Vector3 floor, float yaw, float span)
    {
        const float gateWidth = 3.5f;
        Quaternion orientation = Quaternion.Euler(0, yaw, 0);
        Vector3 across = orientation * Vector3.right;
        float sideWidth = (span - gateWidth) * .5f;
        Vector3 jambSize = Mathf.Abs(across.z) > .5f
            ? new Vector3(.2f, 2.8f, sideWidth) : new Vector3(sideWidth, 2.8f, .2f);
        Vector3 jamb = across * (gateWidth + sideWidth) * .5f;
        Surface("Entrance jamb", floor + Vector3.up * 1.4f - jamb, jambSize, wall);
        Surface("Entrance jamb", floor + Vector3.up * 1.4f + jamb, jambSize, wall);
        ReviewDoor door = PlaceCellFront(prefab, label, floor, orientation, gateWidth, steel);

        PlaceRoomSign(label, floor + Vector3.up * 2.5f - orientation * Vector3.forward * .13f, orientation);
        return door;
    }

    internal static TextMesh PlaceRoomSign(string label, Vector3 position, Quaternion orientation)
    {
        var sign = new GameObject(label).AddComponent<TextMesh>();
        sign.anchor = TextAnchor.MiddleCenter;
        sign.alignment = TextAlignment.Center;
        sign.characterSize = .105f;
        sign.fontSize = 48;
        sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        sign.color = new Color(.74f, .77f, .78f);
        sign.text = label;
        AssignSignMaterial(sign);
        sign.transform.SetPositionAndRotation(position, orientation);
        return sign;
    }

    private static bool AssignSignMaterial(TextMesh sign)
    {
        const string path = Output + "/Room Signs.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("TwinLabyrinth/Room Sign");
            if (shader == null)
                throw new InvalidOperationException("Room sign shader is unavailable.");
            material = new Material(sign.font.material) { shader = shader };
            AssetDatabase.CreateAsset(material, path);
        }
        var renderer = sign.GetComponent<MeshRenderer>();
        if (renderer.sharedMaterial == material)
            return false;
        renderer.sharedMaterial = material;
        return true;
    }

    internal static void Surface(string label, Vector3 position, Vector3 size, Material material)
    {
        var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = label;
        surface.transform.position = position;
        surface.transform.localScale = size;
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        // The built-in cube has separate face normals. Project UVs in metres for each face.
        var filter = surface.GetComponent<MeshFilter>();
        Mesh original = filter.sharedMesh;
        var mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = label + " mesh";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = Vector3.Scale(vertices[i], size) + position;
            Vector3 normal = normals[i];
            uvs[i] = Mathf.Abs(normal.y) > .5f ? new Vector2(vertex.x, vertex.z) :
                Mathf.Abs(normal.x) > .5f ? new Vector2(vertex.z, vertex.y) :
                new Vector2(vertex.x, vertex.y);
            uvs[i] *= .5f;
        }
        mesh.uv = uvs;
        mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Output + "/" + label + ".asset"));
        filter.sharedMesh = mesh;
    }

    private static GameObject PlaceModel(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.transform.SetPositionAndRotation(position, rotation);
        return model;
    }

    internal static GameObject LoadEnvironment(string name) =>
        LoadModel(Source + "/Environment/" + name + "/" + name + "_1k.gltf");

    internal static void PlaceFloorProp(GameObject prefab, Vector3 floor, float yaw)
    {
        GameObject model = PlaceModel(prefab, floor, Quaternion.Euler(0, yaw, 0));
        Bounds bounds = ModelBounds(model);
        model.transform.position += new Vector3(floor.x - bounds.center.x, -bounds.min.y, floor.z - bounds.center.z);
        bounds = ModelBounds(model);
        var obstacle = new GameObject(prefab.name + " collision").AddComponent<BoxCollider>();
        obstacle.transform.position = bounds.center;
        obstacle.size = bounds.size;
    }

    internal static void AddFixture(GameObject prefab, Vector3 mount, float yaw, Vector3 inward,
        List<Light> fixtures, List<Renderer> lampMeshes)
    {
        GameObject lamp = PlaceModel(prefab, mount, Quaternion.Euler(0, yaw, 0));
        lampMeshes.AddRange(lamp.GetComponentsInChildren<Renderer>());
        Light light = AddLight("Caged fixture", mount + inward * .42f + Vector3.down * .18f, LightType.Spot,
            new Color(.81f, .88f, 1f), 1.5f, 7.8f);
        light.transform.rotation = Quaternion.LookRotation(inward + Vector3.down * .7f);
        light.spotAngle = 115f;
        fixtures.Add(light);
    }

    private static ReviewDoor PlaceCellFront(GameObject prefab, string label, Vector3 floor, Quaternion orientation,
        float width, Material steel)
    {
        var parent = new GameObject(label + " gate");
        parent.transform.SetPositionAndRotation(floor, orientation);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        Bounds bounds = ModelBounds(model);
        Vector3 across = parent.transform.right;
        model.transform.localScale = Vector3.one * (width / Vector3.Dot(bounds.size, Abs(across)));
        bounds = ModelBounds(model);
        model.transform.position += new Vector3(floor.x - bounds.center.x, -bounds.min.y, floor.z - bounds.center.z);
        UseCellSteel(parent, steel);

        Transform hinge = model.GetComponentsInChildren<Transform>().Single(child => child.name == "cell_door");
        MeshFilter leaf = hinge.GetComponentInChildren<MeshFilter>();
        Bounds leafBounds = leaf.GetComponent<Renderer>().bounds;
        var movingCollider = leaf.gameObject.AddComponent<BoxCollider>();
        movingCollider.center = leaf.sharedMesh.bounds.center;
        movingCollider.size = leaf.sharedMesh.bounds.size;

        float center = parent.transform.InverseTransformPoint(leafBounds.center).x;
        float half = Vector3.Dot(leafBounds.extents, Abs(across));
        float left = center - half;
        float right = center + half;
        float height = Mathf.Min(leafBounds.max.y, 2.8f);
        AddGateCollider(parent, -width / 2f, left, 0, 2.8f);
        AddGateCollider(parent, right, width / 2f, 0, 2.8f);
        AddGateCollider(parent, left, right, height, 2.8f);
        AddGateCollider(parent, left, right, 0, height, true);

        Vector3 radial = leafBounds.center - hinge.position;
        radial.y = 0;
        float direction = Vector3.Dot(Quaternion.AngleAxis(90, Vector3.up) * radial, parent.transform.forward);
        float sweep = Mathf.Max(Mathf.Abs(left - parent.transform.InverseTransformPoint(hinge.position).x),
            Mathf.Abs(right - parent.transform.InverseTransformPoint(hinge.position).x));
        var door = parent.AddComponent<ReviewDoor>();
        door.Configure(hinge, direction >= 0 ? 100f : -100f, sweep);
        return door;
    }

    private static Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

    private static void AddGateCollider(GameObject parent, float left, float right, float bottom, float top, bool trigger = false)
    {
        if (right - left < .01f || top - bottom < .01f)
            return;
        var collider = parent.AddComponent<BoxCollider>();
        collider.center = new Vector3((left + right) * .5f, (bottom + top) * .5f, 0);
        collider.size = new Vector3(right - left, top - bottom, trigger ? .16f : .1f);
        collider.isTrigger = trigger;
    }

    private static void UseCellSteel(GameObject cell, Material steel)
    {
        foreach (Renderer renderer in cell.GetComponentsInChildren<Renderer>())
        {
            Material[] slots = renderer.sharedMaterials;
            for (int index = 0; index < slots.Length; index++)
                slots[index] = steel;
            renderer.sharedMaterials = slots;
        }
    }

    internal static Bounds ModelBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            throw new InvalidDataException("Model has no renderable meshes: " + model.name);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Animator BuildCharacters()
    {
        GameObject bodyPrefab = LoadModel(BodyModel);
        var parts = new Dictionary<string, GameObject>();
        foreach (string name in new[] { "clothing_tshirt", "clothing_jeans", "clothing_shoes", "clothing_shirt",
                     "clothing_jacket", "clothing_hoodie", "hair_short02", "hair_bob02" })
            parts.Add(name, LoadModel(CharacterRoot + name + ".glb"));

        var clips = AssetDatabase.LoadAllAssetsAtPath(BodyModel).OfType<AnimationClip>()
            .ToDictionary(clip => clip.name, StringComparer.Ordinal);
        var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(Output + "/Prisoner.controller", CreateLoop(clips["idle"]));
        controller.AddMotion(CreateLoop(clips["walk"]));
        controller.AddMotion(CreateLoop(clips["run"]));

        Animator lead = PlaceCharacter(bodyPrefab, parts, controller, "Prisoner 01", new Vector3(.64f, 0, 8.8f), 170,
            1f, "clothing_tshirt", "hair_short02");
        PlaceCharacter(bodyPrefab, parts, controller, "Prisoner 02", new Vector3(3.05f, 0, 12.55f), -90,
            .95f, "clothing_hoodie", "hair_bob02");
        PlaceCharacter(bodyPrefab, parts, controller, "Maintenance inmate", new Vector3(-6.9f, 0, 21), 170,
            1.02f, "clothing_shirt", null);
        PlaceCharacter(bodyPrefab, parts, controller, "Guard costume review", new Vector3(-10.5f, 0, 14.9f), 65,
            1.065f, "clothing_jacket", "hair_short02");
        return lead;
    }

    private static Animator PlaceCharacter(GameObject prefab, Dictionary<string, GameObject> parts, AnimatorController controller,
        string label, Vector3 position, float yaw, float stature, string top, string hair)
    {
        var body = UnityEngine.Object.Instantiate(prefab);
        body.name = label;
        body.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        body.transform.localScale *= stature;
        var bodySkin = body.GetComponentsInChildren<SkinnedMeshRenderer>().First(skin => skin.bones.Length > 0);
        var bones = bodySkin.bones.ToDictionary(bone => bone.name, StringComparer.Ordinal);
        foreach (string part in new[] { top, "clothing_jeans", "clothing_shoes", hair })
        {
            if (part == null)
                continue;
            var item = UnityEngine.Object.Instantiate(parts[part], body.transform);
            var skin = item.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.bones.Any(bone => !bones.ContainsKey(bone.name)))
                throw new InvalidDataException("Accessory skeleton does not match the body: " + part);
            skin.bones = skin.bones.Select(bone => bones[bone.name]).ToArray();
            skin.rootBone = bodySkin.rootBone;
        }

        if (body.TryGetComponent(out Animation legacy))
            UnityEngine.Object.DestroyImmediate(legacy);
        var animator = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        CharacterCoverage.Apply(body);
        return animator;
    }

    internal static Animator CreateMazeAnomaly(Vector3 position, float yaw) =>
        CreateMazeRole("The Imitator", position, yaw, 1.04f, "clothing_hoodie", "hair_bob02");

    internal static Animator CreateMazePartner(Vector3 position, float yaw) =>
        CreateMazeRole("Partner", position, yaw, 1f, "clothing_hoodie", "hair_bob02");

    private static Animator CreateMazeRole(string label, Vector3 position, float yaw, float stature, string top, string hair)
    {
        GameObject model = LoadModel(BodyModel);
        var parts = new Dictionary<string, GameObject>();
        foreach (string name in new[] { top, "clothing_jeans", "clothing_shoes", hair })
            parts.Add(name, LoadModel(CharacterRoot + name + ".glb"));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/Prisoner.controller");
        if (controller == null)
            throw new InvalidOperationException("Maze roles require the existing Cell Block motion controller.");
        return PlaceCharacter(model, parts, controller, label, position, yaw, stature, top, hair);
    }

    private static AnimationClip CreateLoop(AnimationClip source)
    {
        var clip = UnityEngine.Object.Instantiate(source);
        clip.name = source.name;
        clip.legacy = false;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, Output + "/Prisoner_" + clip.name + ".anim");
        return clip;
    }

    internal static Light AddLight(string label, Vector3 position, LightType type, Color color, float intensity, float range)
    {
        Light light = new GameObject(label).AddComponent<Light>();
        light.transform.position = position;
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        return light;
    }

    internal static GameObject BuildPlayer(Light[] fixtures, Renderer[] lampMeshes, Light emergency, Animator prisoner)
    {
        var player = new GameObject("First-person camera rig");
        player.transform.position = new Vector3(0, .08f, 1.5f);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.78f;
        controller.radius = .27f;
        controller.center = new Vector3(0, .89f, 0);
        Camera camera = new GameObject("Eyes").AddComponent<Camera>();
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = new Vector3(0, 1.63f, 0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 42f;
        camera.fieldOfView = 68f;
        Light torch = AddLight("Handheld beam", Vector3.zero, LightType.Spot, new Color(.91f, .94f, 1f), 4f, 13f);
        torch.transform.SetParent(camera.transform, false);
        torch.transform.localPosition = new Vector3(.16f, -.14f, .13f);
        torch.spotAngle = 50f;
        torch.innerSpotAngle = 28f;
        torch.enabled = false;
        player.AddComponent<CorridorReview>().Configure(camera.transform, torch, fixtures, lampMeshes, emergency, prisoner);
        return player;
    }
}
