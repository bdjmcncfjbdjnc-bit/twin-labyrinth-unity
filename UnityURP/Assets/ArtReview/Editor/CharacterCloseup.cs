using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CharacterCloseup
{
    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene("Assets/ArtReview/Expanded/CellBlockReview.unity");
        var actor = GameObject.Find("Prisoner 01");
        Vector3 feet = actor.transform.position;
        var camera = new GameObject("Closeup camera").AddComponent<Camera>();
        camera.transform.position = feet + Vector3.up * 1.3f + actor.transform.forward * 1.9f;
        camera.transform.LookAt(feet + Vector3.up * 1.17f);
        camera.fieldOfView = 39;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var key = camera.gameObject.AddComponent<Light>();
        key.type = LightType.Spot;
        key.spotAngle = 80;
        key.range = 4;
        key.intensity = 2.4f;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ArtReview/Expanded/Prisoner_run.anim");
        clip.SampleAnimation(actor, clip.length * .22f);
        Save(camera, "Character-shapes-before");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent<Animator>(out _))
                CharacterCoverage.Apply(root);
        }
        Save(camera, "Character-shapes-after");
    }

    private static void Save(Camera camera, string label)
    {
        var output = new RenderTexture(720, 720, 24);
        var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = output });
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = output;
        image.ReadPixels(new Rect(0, 0, 720, 720), 0, 0);
        RenderTexture.active = active;
        File.WriteAllBytes("Logs/" + label + ".png", image.EncodeToPNG());
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(output);
    }
}
