using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PrisonInmateBuilder
{
    public static void Stage(MazeSession session)
    {
        if (session.GetComponent<PrisonInmateRoster>() != null)
            return;
        const string knifePath = "Assets/ArtReview/Source/Environment/prison_kitchen_knife.glb";
        GameObject prefab = CorridorReviewBuilder.LoadModel(knifePath);
        var actors = new[]
        {
            (name: "Intake orderly", model: "hannah_casual"),
            (name: "Infirmary worker", model: "hannah_casual"),
            (name: "Records clerk", model: "kenji_formal")
        };
        var roster = new PrisonInmate[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            GameObject actor = GameObject.Find(actors[i].name);
            Animator animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = Controller(actors[i].model);
            Transform hand = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:RightHand");
            Transform finger = hand.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:RightHandMiddle1");
            Vector3 forward = (finger.position - hand.position).normalized;
            var mount = new GameObject("Hidden inmate knife").transform;
            mount.SetParent(hand, true);
            mount.position = hand.position + forward * .071f;
            mount.rotation = Quaternion.LookRotation(forward, actor.transform.up);
            var knife = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mount);
            Bounds bounds = CorridorReviewBuilder.ModelBounds(knife);
            Vector3 dimensions = bounds.size;
            knife.transform.localRotation = dimensions.x >= dimensions.y && dimensions.x >= dimensions.z
                ? Quaternion.Euler(0f, 90f, 0f) : dimensions.y >= dimensions.z ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            float length = Mathf.Max(dimensions.x, dimensions.y, dimensions.z);
            knife.transform.localScale *= .245f / Mathf.Max(.001f, length);
            knife.transform.localPosition -= mount.InverseTransformPoint(CorridorReviewBuilder.ModelBounds(knife).center);
            knife.transform.localPosition += new Vector3(.018f, -.018f, .058f);
            mount.gameObject.SetActive(false);

            actor.layer = 2;
            CharacterController body = actor.AddComponent<CharacterController>();
            body.height = 1.72f;
            body.radius = .21f;
            body.center = Vector3.up * .86f;
            PrisonInmate inmate = actor.AddComponent<PrisonInmate>();
            inmate.Configure(animator, mount.gameObject);
            roster[i] = inmate;
        }
        session.gameObject.AddComponent<PrisonInmateRoster>().Configure(session,
            session.GetComponentInChildren<Camera>().transform, roster);
        Debug.Log("Staged three inmates sharing ordinary and hidden-knife roles.");
    }

    private static AnimatorController Controller(string model)
    {
        string root = "Assets/ArtReview/HauntCoop/";
        string path = root + model + " Inmate.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null)
            return controller;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath("Assets/ArtReview/Source/MazeCast/" + model + ".glb")
            .OfType<AnimationClip>().ToArray();
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var states = controller.layers[0].stateMachine;
        foreach (string name in new[] { "Idle", "Walk" })
        {
            var clip = Object.Instantiate(clips.Single(motion => motion.name == name));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, root + model + " Inmate " + name + ".anim");
            AnimatorState state = states.AddState(name);
            state.motion = clip;
            if (name == "Idle")
                states.defaultState = state;
        }
        return controller;
    }
}
