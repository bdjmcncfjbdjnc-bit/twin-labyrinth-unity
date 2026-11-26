using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class CharacterCoverage
{
    private const string Source = "Assets/ArtReview/Source/Characters/";
    private const string Output = "Assets/ArtReview/Expanded/";

    [Serializable] private sealed class Outfit
    {
        public string top;
        public int mask;
    }

    [Serializable] private sealed class Catalog
    {
        public Outfit[] outfits;
    }

    public static bool Apply(GameObject character)
    {
        var body = character.GetComponentsInChildren<SkinnedMeshRenderer>().SingleOrDefault(renderer => renderer.name == "Body");
        if (body == null)
            return false;

        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Source + "outfit-masks.json"));
        var outfit = catalog.outfits.Single(entry => character.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Any(renderer => AssetDatabase.GetAssetPath(renderer.sharedMesh).EndsWith("/clothing_" + entry.top + ".glb",
                StringComparison.Ordinal)));
        string path = Output + "Covered Body " + outfit.top + ".asset";
        var covered = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (covered == null)
        {
            covered = CutCoveredSkin(body.sharedMesh, outfit);
            AssetDatabase.CreateAsset(covered, path);
        }
        bool changed = body.sharedMesh != covered;
        if (changed)
            body.sharedMesh = covered;
        string face = outfit.top switch
        {
            "tshirt" => "face_jaw_width_incr",
            "hoodie" => "face_nose_height_decr",
            "shirt" => "face_chin_incr",
            "jacket" => "face_nose_width_incr",
            _ => throw new InvalidDataException("Unknown outfit: " + outfit.top)
        };
        foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            changed |= ApplyCharacterShape(renderer, face);
        return changed;
    }

    private static bool ApplyCharacterShape(SkinnedMeshRenderer renderer, string face)
    {
        bool changed = false;
        Mesh mesh = renderer.sharedMesh;
        for (int index = 0; index < mesh.blendShapeCount; index++)
        {
            // These authoring GLBs default every shape key to one. The source viewer
            // supplies its own weights; Unity needs the chosen character shape explicitly.
            float weight = mesh.GetBlendShapeName(index) == face ? FullWeight(mesh, index) * .45f : 0f;
            if (Mathf.Approximately(renderer.GetBlendShapeWeight(index), weight))
                continue;
            renderer.SetBlendShapeWeight(index, weight);
            changed = true;
        }
        return changed;
    }

    private static float FullWeight(Mesh mesh, int index) =>
        mesh.GetBlendShapeFrameWeight(index, mesh.GetBlendShapeFrameCount(index) - 1);

    private static Mesh CutCoveredSkin(Mesh source, Outfit outfit)
    {
        int[] zones = ReadZones(source);
        var covered = UnityEngine.Object.Instantiate(source);
        covered.name = "Covered Body " + outfit.top;
        int removed = 0;
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            int[] original = source.GetTriangles(submesh);
            var visible = new List<int>(original.Length);
            for (int i = 0; i < original.Length; i += 3)
            {
                int a = original[i];
                int b = original[i + 1];
                int c = original[i + 2];
                if ((zones[a] & zones[b] & zones[c] & outfit.mask) != 0)
                {
                    removed++;
                    continue;
                }
                visible.Add(a);
                visible.Add(b);
                visible.Add(c);
            }
            covered.SetTriangles(visible, submesh, false);
        }
        if (removed == 0)
            throw new InvalidDataException("No covered triangles for " + outfit.top);
        Debug.Log("Character coverage " + outfit.top + ": removed " + removed + " covered skin triangles.");
        return covered;
    }

    private static int[] ReadZones(Mesh source)
    {
        using var data = new BinaryReader(File.OpenRead(Source + "body-zones.bytes"));
        int count = data.ReadInt32();
        var vertices = source.vertices;
        if (count != vertices.Length)
            throw new InvalidDataException("Imported body vertex count differs from the coverage map.");
        for (int i = 0; i < count; i++)
        {
            // glTFast changes handedness by mirroring local X.
            var expected = new Vector3(-data.ReadSingle(), data.ReadSingle(), data.ReadSingle());
            if ((expected - vertices[i]).sqrMagnitude > 1e-10f)
                throw new InvalidDataException("Imported body vertex order differs from the coverage map at " + i);
        }
        var zones = new int[count];
        for (int i = 0; i < count; i++)
            zones[i] = data.ReadUInt16();
        return zones;
    }
}
