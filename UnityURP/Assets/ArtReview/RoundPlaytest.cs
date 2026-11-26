using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class RoundPlaytest : MonoBehaviour
{
    public static bool Active { get; private set; }
    private static readonly HashSet<KeyCode> keys = new();
    private static readonly HashSet<KeyCode> previous = new();
    public static bool Held(KeyCode key) => keys.Contains(key);
    public static bool Down(KeyCode key) => keys.Contains(key) && !previous.Contains(key);
    public static int? RequestedSeed
    {
        get { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-audit-seed");return i>=0 && i+1<args.Length ? int.Parse(args[i+1]) : null; }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
#if UNITY_STANDALONE
        if(!Environment.GetCommandLineArgs().Contains("-audit-round")) return;
        Active=true;new GameObject("Real input round playtest").AddComponent<RoundPlaytest>();
#endif
    }
    private MazeSession session;
    private LastRollCall mission;
    private HauntExperience gear;
    private TransferInterlock airlock;
    private Transform eyes;
    private HauntRelay[] relays;
    private ReviewDoor[] doors;
    private int stage;
    private Vector3? waypoint;
    private Vector3 lastGoal;
    private float started;
    private float nextLog;
    private bool ended;
    private string captureFolder;
    private void Start()
    {
        session=FindFirstObjectByType<MazeSession>();mission=session.GetComponent<LastRollCall>();gear=session.GetComponent<HauntExperience>();
        airlock=session.GetComponent<TransferInterlock>();eyes=session.GetComponentInChildren<Camera>().transform;
        relays=FindObjectsByType<HauntRelay>(FindObjectsSortMode.None);doors=FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None);
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=2f;
        started=Time.realtimeSinceStartup;
        captureFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));Directory.CreateDirectory(captureFolder);
        Debug.Log("ROUND PLAYTEST START: live NPCs, protocols, collision and input enabled; 2x simulation time; route="+mission.Plan.Selected);
    }
    private void Update()
    {
        previous.Clear();previous.UnionWith(keys);keys.Clear();
        if(session==null || ended)return;
        Cursor.lockState=CursorLockMode.None;
        if(session.IsOver) { StartCoroutine(Finish(session.Escaped));return; }
        if(Time.realtimeSinceStartup-started>900f) {StartCoroutine(Finish(false));return;}
        if(Time.time>=nextLog){nextLog=Time.time+30;Debug.Log($"ROUND progress stage={stage} time={session.ElapsedTime:0} position={session.transform.position} airlock={airlock.Current} detained={session.HasDetainee}");Capture("Round-live.png");}
        if(session.PlayerDetained) {waypoint=null;return;}
        var partner=session.PartnerTransform.GetComponent<MazeCompanion>();
        if(partner.NeedsHelp || session.HasDetainee)
        {
            if(session.CanAssist){Aim(session.PartnerTransform.position+Vector3.up);keys.Add(KeyCode.F);}else WalkTo(session.PartnerTransform.position);
            return;
        }
        if(stage==0)
        {
            if(gear.HasTorch){Advance();return;}
            var item=FindObjectsByType<HauntPickup>(FindObjectsSortMode.None).FirstOrDefault(p=>p.Type==HauntPickup.Kind.Torch);
            if(item==null){StartCoroutine(Finish(false));return;}
            Interact(item.GetComponent<Collider>().bounds.center,item.transform.position + (session.transform.position-item.transform.position).normalized*.9f);
        }
        else if(stage==1){if(mission.Plan.Briefed){Advance();return;}Station(0);}
        else if(stage==2){if(mission.Plan.Equipped){Advance();return;}Station(1);}
        else if(stage==3){var relay=relays.Single(r=>r.Index==0);if(session.RelayOn(relay)){Advance();return;}Interact(relay.transform.position,relay.transform.position+relay.transform.forward*1.3f);}
        else if(stage==4){if(mission.Plan.Prepared){Advance();return;}Station(2);}
        else if(stage==5){var relay=relays.Single(r=>r.Index==1);if(session.RelayOn(relay)){Advance();return;}Interact(relay.transform.position,relay.transform.position+relay.transform.forward*1.3f);}
        else if(stage==6){if(mission.Ready){Advance();return;}Station(3);}
        else if(stage==7)
        {
            if(airlock.Complete){Advance();return;}
            if(airlock.Current is TransferInterlock.Phase.SendPartner or TransferInterlock.Phase.ReturnControl)Station(3);
            else WalkTo(airlock.assemblyPoint.position);
        }
        else WalkTo(session.ExitPosition);
    }
    private void Advance()
    {
        Debug.Log("ROUND stage complete "+stage);stage++;waypoint=null;
        Capture("Round-stage-"+stage+".png");
    }
    private void Station(int index)
    {
        var target=mission.stations[index].transform;
        Interact(target.position,target.position+target.forward*1.05f);
    }
    private void Interact(Vector3 focus,Vector3 approach)
    {
        approach.y=session.transform.position.y;
        if(Vector2.Distance(new Vector2(session.transform.position.x,session.transform.position.z),new Vector2(approach.x,approach.z))>.35f)
        {WalkTo(approach);return;}
        waypoint=null;Aim(focus);keys.Add(KeyCode.F);
    }
    private void WalkTo(Vector3 target)
    {
        target.y=session.transform.position.y;
        if((target-lastGoal).sqrMagnitude>.3f){waypoint=null;lastGoal=target;}
        if(waypoint.HasValue && FlatDistance(waypoint.Value,session.transform.position)<.13f)waypoint=null;
        if(!waypoint.HasValue)waypoint=session.RouteWaypoint(target);
        Vector3 step=waypoint.Value;step.y=session.transform.position.y;
        if(FlatDistance(step,session.transform.position)<.08f)return;
        Aim(step+Vector3.up*1.6f);keys.Add(KeyCode.W);
        foreach(var door in doors)
            if(!door.IsLocked && Vector3.Distance(door.transform.position,session.transform.position)<2.5f &&
                Vector3.Dot(session.transform.forward,(door.transform.position-session.transform.position).normalized)>.5f)
            {Aim(door.transform.position+Vector3.up*1.1f);if(!door.IsOpen && !previous.Contains(KeyCode.F))keys.Add(KeyCode.F);break;}
    }
    private void Aim(Vector3 point)
    {
        Vector3 horizontal=point-session.transform.position;horizontal.y=0;
        if(horizontal.sqrMagnitude>.001f)session.transform.rotation=Quaternion.LookRotation(horizontal);
        eyes.rotation=Quaternion.LookRotation(point-eyes.position);
    }
    private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    private IEnumerator Finish(bool success)
    {
        ended=true;keys.Clear();Time.timeScale=1f;
        Debug.Log((success?"PASS":"FAIL")+$" FULL LIVE ROUND route={mission.Plan.Selected} stage={stage} elapsed={session.ElapsedTime:0.0}s wall={Time.realtimeSinceStartup-started:0.0}s airlock={airlock.Current}");
        yield return new WaitForSeconds(.2f);
        Capture("Round-final.png");
        yield return new WaitForSeconds(2);Application.Quit(success?0:2);
    }
    private void Capture(string file)
    {
        var camera=session.GetComponentInChildren<Camera>();
        var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Path.Combine(captureFolder,file),pixels.EncodeToPNG());Destroy(pixels);Destroy(target);
    }
}
