#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit native-only QA harness. Not compiled into the public WebGL client.
public sealed class CoopVisualTest : MonoBehaviour
{
    TwinNetwork net;TwinNetwork.State state;string folder;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Environment.GetCommandLineArgs().Contains("-coopqa"))new GameObject("Co-op visual QA").AddComponent<CoopVisualTest>();}
    void Field(string name,object value)=>typeof(TwinNetwork).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(net,value);
    void Push(){net.Receive(JsonUtility.ToJson(state));Field("menu",false);}
    void LateUpdate(){if(net!=null&&state!=null)Push();}
    IEnumerator Start()
    {
        yield return new WaitForSeconds(1);net=FindFirstObjectByType<TwinNetwork>();if(net==null){Debug.LogError("QA missing network");Application.Quit(2);yield break;}
        folder=Path.Combine(Application.dataPath,"../../../Logs");Directory.CreateDirectory(folder);
        state=new TwinNetwork.State{type="state",doors=new bool[net.doors.Length],players=new[]{new TwinNetwork.Peer{slot=0,x=2,z=2,hp=100,torch=true,online=true,hasKnife=true,weapon="torch"},new TwinNetwork.Peer{slot=1,x=2.7f,z=5,hp=100,torch=true,online=true,weapon="torch"}},actors=net.actors.Select((a,i)=>new TwinNetwork.Actor{id=i,x=a.transform.position.x,z=a.transform.position.z,hp=100,mode="idle",yaw=180}).ToArray()};
        net.Receive("{\"type\":\"hello\",\"slot\":0,\"code\":\"999999\"}");Push();yield return new WaitForSeconds(1);
        yield return Shot("Torch-forward");
        Field("weapon","knife");state.players[0].weapon="knife";yield return new WaitForSeconds(.3f);yield return Shot("Knife-idle");
        state.players[0].swing++;Push();yield return new WaitForSeconds(.28f);yield return Shot("Knife-strike");yield return new WaitForSeconds(.6f);
        state.players[0].x=2;state.players[0].z=7.8f;state.players[1].x=3;state.players[1].z=7;
        state.actors[0].x=2;state.actors[0].z=10;state.actors[0].hit=1;state.actors[0].hp=45;state.actors[0].mode="hurt";Push();yield return new WaitForSeconds(.15f);yield return Shot("NPC-hit");
        state.actors[0].hit=2;state.actors[0].hp=0;state.actors[0].mode="dead";Push();yield return new WaitForSeconds(.5f);yield return Shot("NPC-falling");yield return new WaitForSeconds(.9f);
        net.view.transform.localRotation=Quaternion.Euler(32,0,0);Field("pitch",32f);yield return Shot("NPC-ground");
        foreach(var actor in net.actors){if(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.bones.Length<15))throw new Exception("Invalid character skin");}
        state.players[1].hp=0;state.players[1].hit++;Push();yield return new WaitForSeconds(1.3f);yield return Shot("Partner-down");state.players[1].hp=65;state.players[1].hit++;Push();yield return new WaitForSeconds(.5f);
        Field("weapon","torch");state.players[0].weapon="torch";state.players[0].x=.38f;state.players[0].z=6;state.players[0].yaw=270;net.player.rotation=Quaternion.Euler(0,270,0);net.view.transform.localRotation=Quaternion.identity;Field("pitch",0f);Push();yield return new WaitForSeconds(.5f);yield return Shot("Torch-near-wall");
        foreach(var room in FindObjectsByType<PrisonRoom>(FindObjectsSortMode.None))
        {
            int cell=room.cells[0];var p=new Vector3((cell%17)*4+2,0,(cell/17)*4+2);state.players[0].x=p.x;state.players[0].z=p.z;var dir=room.volume.center-p;dir.y=0;net.player.rotation=Quaternion.LookRotation(dir);state.players[0].yaw=net.player.eulerAngles.y;Push();yield return new WaitForSeconds(.35f);yield return Shot("Room-"+room.roomName);
        }
        Debug.Log("PASS native co-op visual sequence: torch, knife idle/strike, NPC hit/fall/ground, partner down/revive, near-wall exposure, six rooms. This uses scripted snapshot fixtures, not real networking.");
        Application.Quit(0);
    }
    IEnumerator Shot(string label)
    {
        yield return null;
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        RenderPipeline.SubmitRenderRequest(net.view,new RenderPipeline.StandardRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(folder,"Coop-QA-"+label+".png"),image.EncodeToPNG());Destroy(image);Destroy(target);Debug.Log("QA SCREENSHOT "+label);
    }
}
#endif
