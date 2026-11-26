using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

internal static class PrisonHandsBuilder
{
    internal static float GripCurl = -48f;
    internal static float GripRoll = -65f;
    public static void Stage(Camera camera, Transform torch)
    {
        Transform left = Arm(camera.transform, "Left", new Vector3(-.25f, -.49f, .42f));
        Transform right = Arm(camera.transform, "Right", new Vector3(.235f, -.285f, .49f));
        torch.SetParent(right, true);
        // The cylinder belongs inside the fingers, not at a guessed camera offset.
        Transform Joint(string name)=>right.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:RightHand"+name);
        Vector3 grip=(Joint("Middle1").position+Joint("Middle3").position+Joint("Ring1").position+Joint("Ring3").position)*.25f;
        torch.rotation=camera.transform.rotation;
        Vector3 dimensions=CorridorReviewBuilder.ModelBounds(torch.gameObject).size;
        if(dimensions.x>dimensions.z && dimensions.x>dimensions.y)torch.rotation*=Quaternion.Euler(0,90,0);
        else if(dimensions.y>dimensions.z)torch.rotation*=Quaternion.Euler(90,0,0);
        torch.position+=grip+camera.transform.forward*.045f-CorridorReviewBuilder.ModelBounds(torch.gameObject).center;
        camera.gameObject.AddComponent<PrisonHands>().Configure(left, right);
    }

    public static void SetupOverlay(Camera camera)
    {
        const int layer = 29;
        if (camera.transform.Find("Hands render camera") != null)
            return;
        foreach (string name in new[] { "Left first-person hand", "Right first-person hand" })
            foreach (Transform part in camera.transform.Find(name).GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = layer;
        camera.cullingMask &= ~(1 << layer);
        var view = new GameObject("Hands render camera").AddComponent<Camera>();
        view.transform.SetParent(camera.transform, false);
        view.CopyFrom(camera);
        view.nearClipPlane = .025f;
        view.farClipPlane = 3f;
        view.cullingMask = 1 << layer;
        var overlay = view.GetUniversalAdditionalCameraData();
        overlay.renderType = CameraRenderType.Overlay;
        camera.GetUniversalAdditionalCameraData().cameraStack.Add(view);
    }

    private static Transform Arm(Transform camera, string side, Vector3 handPosition)
    {
        const string bodyPath = "Assets/ArtReview/Source/MazeCast/marcus_formal.glb";
        GameObject actor = UnityEngine.Object.Instantiate(CorridorReviewBuilder.LoadModel(bodyPath));
        foreach (Animator animation in actor.GetComponentsInChildren<Animator>(true))
            UnityEngine.Object.DestroyImmediate(animation);
        actor.name = side + " arm skeleton";
        Transform Joint(string name) => actor.GetComponentsInChildren<Transform>(true)
            .Single(joint => joint.name == "mixamorig:" + side + name);
        Transform wrist = Joint("Hand");
        Transform elbow = Joint("ForeArm");
        Vector3 forward = (wrist.position - elbow.position).normalized;
        Vector3 across = (Joint("HandIndex1").position - Joint("HandPinky1").position).normalized;
        Vector3 up = Vector3.Cross(forward, across).normalized * (side == "Right" ? 1f : -1f);
        Quaternion rest = Quaternion.LookRotation(forward, up);
        ValveHandPose.Apply(actor.transform, side,"fallback_relaxed");
        var fingers=wrist.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Hand")&&char.IsDigit(t.name[^1])&&t.childCount>0).ToArray();
        var relaxed=fingers.Select(t=>t.localRotation).ToArray();
        ValveHandPose.Apply(actor.transform, side);
        if(side=="Right")actor.AddComponent<HandGripBlend>().Configure(fingers,relaxed,fingers.Select(t=>t.localRotation).ToArray());

        int kept = 0;
        foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh original = skin.sharedMesh;
            BoneWeight[] weights = original.boneWeights;
            Transform[] bones = skin.bones;
            bool ArmBone(int index) => bones[index] != null && (bones[index] == elbow || bones[index].IsChildOf(elbow));
            bool IsArm(BoneWeight w) => (ArmBone(w.boneIndex0) ? w.weight0 : 0f) +
                (ArmBone(w.boneIndex1) ? w.weight1 : 0f) + (ArmBone(w.boneIndex2) ? w.weight2 : 0f) +
                (ArmBone(w.boneIndex3) ? w.weight3 : 0f) >= .46f;
            if (weights.Length != original.vertexCount)
                throw new InvalidOperationException("Source arm mesh has no skin weights.");
            var groups = new List<int[]>();
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                int[] indices = original.GetTriangles(sub);
                var arm = new List<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (IsArm(weights[indices[i]]) && IsArm(weights[indices[i + 1]]) && IsArm(weights[indices[i + 2]]))
                        arm.AddRange(new[] { indices[i], indices[i + 1], indices[i + 2] });
                }
                groups.Add(arm.ToArray());
            }
            if (groups.All(group => group.Length == 0))
            {
                UnityEngine.Object.DestroyImmediate(skin);
                continue;
            }
            string path = $"Assets/ArtReview/HauntCoop/{side}-arm-{kept++}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(original);
                for (int sub = 0; sub < groups.Count; sub++)
                    mesh.SetTriangles(groups[sub], sub);
                AssetDatabase.CreateAsset(mesh, path);
            }
            skin.sharedMesh = mesh;
            skin.sharedMaterials = skin.sharedMaterials.Select(material =>
            {
                if(!material.name.Contains("Top_shirt"))return material;
                const string sleevePath="Assets/ArtReview/HauntCoop/Prison first-person sleeve.mat";
                var sleeve=AssetDatabase.LoadAssetAtPath<Material>(sleevePath);
                if(sleeve==null)
                {
                    sleeve=new Material(material);
                    if(sleeve.HasProperty("baseColorFactor"))sleeve.SetColor("baseColorFactor",new Color(.42f,.46f,.43f));
                    if(sleeve.HasProperty("_BaseColor"))sleeve.SetColor("_BaseColor",new Color(.42f,.46f,.43f));
                    AssetDatabase.CreateAsset(sleeve,sleevePath);
                }
                return sleeve;
            }).ToArray();
            skin.quality = SkinQuality.Bone4;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.updateWhenOffscreen = true;
        }
        if (kept == 0)
            throw new InvalidOperationException("No first-person arm geometry found.");
        var carrier = new GameObject(side + " first-person hand").transform;
        carrier.SetParent(camera, false);
        carrier.localPosition = handPosition;
        carrier.localRotation = Quaternion.Euler(0f, 0f, side == "Right" ? 0f : 35f);
        actor.transform.SetParent(carrier, false);
        Vector3 direction=side=="Right" ? new Vector3(-.7f,.55f,.42f) : new Vector3(.16f,.38f,1f);
        actor.transform.localRotation = Quaternion.LookRotation(direction,side=="Right"?Vector3.Cross(direction,Vector3.forward):Vector3.up)*Quaternion.Inverse(rest);
        actor.transform.localPosition -= carrier.InverseTransformPoint(wrist.position);
        return carrier;
    }
}
