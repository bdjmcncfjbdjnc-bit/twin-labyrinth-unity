using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CoopBuilder
{
    [Serializable] class Point{public float x,z;public Point(Vector3 v){x=v.x;z=v.z;}}
    [Serializable] class Gate{public float x,z,nx,nz;public Gate(Transform t){x=t.position.x;z=t.position.z;nx=t.forward.x;nz=t.forward.z;}}
    [Serializable] class Obstacle{public float x,z,w,d;public Obstacle(Bounds b){x=b.center.x;z=b.center.z;w=b.size.x;d=b.size.z;}}
    [Serializable] class Map{public int size;public float pitch;public int[] passages,exitDoors;public Point spawn,exit,knife;public Point[] relays,actors;public Gate[] doors;public Obstacle[] obstacles;}
    public static void Build()
    {Stage();BuildWeb();}
    public static void Preview()
    {Stage();CoopArtAudit.Capture("After");}
    public static void Stage()
    {
        var scene=EditorSceneManager.OpenScene(MazeReviewBuilder.ScenePath);
        var session=Object.FindFirstObjectByType<MazeSession>();var controls=session.GetComponent<CorridorReview>();var camera=session.GetComponentInChildren<Camera>();
        Object.DestroyImmediate(session.GetComponent<PrisonPresentation>());
        var inmates=Object.FindObjectsByType<PrisonInmate>(FindObjectsSortMode.None);
        foreach(var station in Object.FindObjectsByType<EscapeStation>(FindObjectsSortMode.None))station.gameObject.SetActive(false);
        foreach(var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if(behaviour.GetType().Assembly==typeof(MazeSession).Assembly&&behaviour is not ReviewDoor&&behaviour is not PrisonHands&&behaviour is not HandGripBlend&&behaviour is not ChineseWorldLabel)behaviour.enabled=false;
        foreach(var actor in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            if(actor.transform!=session.PartnerTransform&&actor.GetComponentInParent<PrisonHands>()==null)actor.gameObject.SetActive(false);
        var partner=session.PartnerTransform;
        foreach(var collider in partner.GetComponentsInChildren<Collider>())collider.enabled=false;
        var network=new GameObject("Online session").AddComponent<TwinNetwork>();network.player=session.transform;network.view=camera;network.remote=partner;
        var controlData=new SerializedObject(controls);network.torch=(Light)controlData.FindProperty("flashlight").objectReferenceValue;
        var fixtures=controlData.FindProperty("corridorLights");network.fixtures=Enumerable.Range(0,fixtures.arraySize).Select(i=>(Light)fixtures.GetArrayElementAtIndex(i).objectReferenceValue).Where(l=>l!=null).ToArray();
        network.doors=Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None).OrderBy(d=>d.name,StringComparer.Ordinal).ThenBy(d=>d.transform.position.x).ThenBy(d=>d.transform.position.z).ToArray();
        network.relays=Object.FindObjectsByType<HauntRelay>(FindObjectsSortMode.None).OrderBy(r=>r.Index).ToArray();
        var gates=new SerializedObject(session).FindProperty("exitDoors");network.exitDoors=Enumerable.Range(0,gates.arraySize).Select(i=>Array.IndexOf(network.doors,(ReviewDoor)gates.GetArrayElementAtIndex(i).objectReferenceValue)).ToArray();
        if(network.exitDoors.Any(i=>i<0)||network.relays.Length!=2||partner.GetComponent<Animator>()==null)throw new Exception("Incomplete co-op scene bindings");
        CoopArtRefinement.Stage(network,inmates);
        var layout=MazeLayout.Create();Physics.SyncTransforms();
        var boxes=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(b=>b.enabled&&!b.isTrigger&&b.GetComponentInParent<ReviewDoor>()==null&&b.GetComponentInParent<HauntPickup>()==null&&b.bounds.min.y<1.5f&&b.bounds.max.y>.45f);
        var map=new Map{size=MazeLayout.Size,pitch=MazeLayout.Pitch,passages=layout.CopyPassages(),spawn=new Point(MazeLayout.Center(MazeLayout.Start)),exit=new Point(session.ExitPosition),
            doors=network.doors.Select(d=>new Gate(d.transform)).ToArray(),exitDoors=network.exitDoors,knife=new Point(network.knifeRack.position),actors=network.actors.Select(a=>new Point(a.transform.position)).ToArray(),relays=network.relays.Select(r=>new Point(r.transform.position)).ToArray(),obstacles=boxes.Select(b=>new Obstacle(b.bounds)).ToArray()};
        Directory.CreateDirectory("Server");File.WriteAllText("Server/map.json",JsonUtility.ToJson(map,true));
        Directory.CreateDirectory("Assets/ArtReview/Coop");const string scenePath="Assets/ArtReview/Coop/PrisonOnline.unity";EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
        Debug.Log("PASS co-op scene: two player bindings, "+network.doors.Length+" doors, two relays, "+map.obstacles.Length+" static obstacles; no guard AI or solo protocol updates.");
        PlayerSettings.productName="双生迷城 · 双人联机";PlayerSettings.runInBackground=true;
    }
    public static void BuildWeb()
    {
        const string scenePath="Assets/ArtReview/Coop/PrisonOnline.unity";
        var report=BuildPipeline.BuildPlayer(new[]{scenePath},"Builds/Coop-Combat-Web",BuildTarget.WebGL,BuildOptions.None);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Co-op build failed: "+report.summary.result);
        File.Copy("ThirdPartyNotices.txt","Builds/Coop-Combat-Web/Rocketbox-Notices.txt",true);
        File.AppendAllText("Builds/Coop-Combat-Web/Rocketbox-Notices.txt","\n"+File.ReadAllText("Assets/ArtReview/Source/CombatAudio/SOURCE.txt"));
    }
    public static void BuildNative()
    {
        Stage();var report=BuildPipeline.BuildPlayer(new[]{"Assets/ArtReview/Coop/PrisonOnline.unity"},"Builds/Coop-Combat-Windows/TwinLabyrinth.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Native co-op QA build failed");
    }
}
