using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CoopArtAudit
{
    public static void Before()
    {
        EditorSceneManager.OpenScene("Assets/ArtReview/Coop/PrisonOnline.unity");
        Capture("Before");
    }
    public static void Capture(string prefix)
    {
        var net=Object.FindFirstObjectByType<TwinNetwork>();var cam=net.view;
        typeof(CorridorReview).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(net.player.GetComponent<CorridorReview>(),null);
        var text=new StringBuilder();
        foreach(var room in Object.FindObjectsByType<PrisonRoom>(FindObjectsSortMode.None))
        {
            text.AppendLine("ROOM "+room.roomName+" "+room.volume);
            foreach(Transform child in room.transform){var rs=child.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);text.AppendLine(child.name+" "+b);}
        }
        var hand=cam.transform.Find("Right first-person hand");
        foreach(var t in hand.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("RightHand")))text.AppendLine(t.name+" "+cam.transform.InverseTransformPoint(t.position).ToString("F4"));
        var held=hand.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Held flashlight");held.gameObject.SetActive(true);
        text.AppendLine("TORCH "+CorridorReviewBuilder.ModelBounds(held.gameObject));
        foreach(var blend in hand.GetComponentsInChildren<HandGripBlend>())
        {var so=new SerializedObject(blend);var f=so.FindProperty("fingers");var q=so.FindProperty("holding");for(int i=0;i<f.arraySize;i++)((Transform)f.GetArrayElementAtIndex(i).objectReferenceValue).localRotation=q.GetArrayElementAtIndex(i).quaternionValue;}
        net.torch.enabled=true;net.torch.intensity=2f;
        typeof(CorridorReview).GetMethod("SetPower",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(net.player.GetComponent<CorridorReview>(),new object[]{true});
        Save(cam,prefix+"-Hands");
        foreach(var room in Object.FindObjectsByType<PrisonRoom>(FindObjectsSortMode.None))
        {
            net.player.position=MazeLayout.Center(room.cells[0])+Vector3.up*.08f;
            net.player.rotation=Quaternion.LookRotation(room.volume.center-new Vector3(net.player.position.x,room.volume.center.y,net.player.position.z));
            typeof(CorridorReview).GetMethod("SetPower",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(net.player.GetComponent<CorridorReview>(),new object[]{true});
            Save(cam,prefix+"-Room-"+room.roomName);
        }
        File.WriteAllText("Logs/Coop-Art-"+prefix+".txt",text.ToString());
    }
    public static void Save(Camera cam,string name)
    {
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=old;
        File.WriteAllBytes("Logs/Coop-Art-"+name+".png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);
    }
}

