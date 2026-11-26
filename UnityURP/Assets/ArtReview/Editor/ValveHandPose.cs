using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Retarget bone DIRECTIONS, not local quaternions: Valve and Rocketbox use different bind axes.
internal static class ValveHandPose
{
    public static void Apply(Transform actor, string side, string overridePose=null)
    {
        string file = overridePose ?? (side == "Right" ? "fallback_fist" : "fallback_relaxed");
        string yaml = File.ReadAllText("Assets/ArtReview/Source/HandPoses/" + file + ".txt");
        string hand = yaml.Split(side == "Right" ? "  rightHand:" : "  leftHand:")[1].Split("  rightHand:")[0];
        float F(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        var positions = Regex.Matches(hand.Split("bonePositions:")[1].Split("boneRotations:")[0], @"\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}")
            .Cast<Match>().Select(m => new Vector3(F(m.Groups[1].Value),F(m.Groups[2].Value),F(m.Groups[3].Value))).ToArray();
        var rotations = Regex.Matches(hand.Split("boneRotations:")[1], @"\{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}")
            .Cast<Match>().Select(m => new Quaternion(F(m.Groups[1].Value),F(m.Groups[2].Value),F(m.Groups[3].Value),F(m.Groups[4].Value))).ToArray();
        int[] parents = { -1,0,1,2,3,4,1,6,7,8,9,1,11,12,13,14,1,16,17,18,19,1,21,22,23,24 };
        var world = new Vector3[26]; var orientation = new Quaternion[26];
        for(int i=0;i<26;i++)
        { int p=parents[i]; world[i]=p<0?positions[i]:world[p]+orientation[p]*positions[i]; orientation[i]=p<0?rotations[i]:orientation[p]*rotations[i]; }
        Transform Joint(string n)=>actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:"+side+n);
        var wrist=Joint("Hand");
        Vector3 forward=(Joint("HandMiddle1").position-wrist.position).normalized;
        Vector3 across=(Joint("HandIndex1").position-Joint("HandPinky1").position).normalized;
        Quaternion targetFrame=Quaternion.LookRotation(forward,Vector3.Cross(forward,across));
        Vector3 sourceForward=(world[12]-world[1]).normalized;
        Quaternion sourceFrame=Quaternion.LookRotation(sourceForward,Vector3.Cross(sourceForward,world[7]-world[22]));
        Quaternion conversion=targetFrame*Quaternion.Inverse(sourceFrame);
        string[] fingers={"Thumb","Index","Middle","Ring","Pinky"}; int[] start={2,7,12,17,22};
        for(int f=0;f<5;f++) for(int j=0;j<3;j++)
        {
            var bone=Joint("Hand"+fingers[f]+(j+1)); if(bone.childCount==0)continue;
            Vector3 desired=conversion*(world[start[f]+j+1]-world[start[f]+j]);
            bone.rotation=Quaternion.FromToRotation(bone.GetChild(0).position-bone.position,desired)*bone.rotation;
        }
    }
}
