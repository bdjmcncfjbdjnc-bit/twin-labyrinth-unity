using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

internal static class CoopArtRefinement
{
    const string KnifePath="Assets/ArtReview/Source/Environment/prison_kitchen_knife.glb";
    public static void Stage(TwinNetwork net,PrisonInmate[] inmates)
    {
        CleanRooms();
        var camera=net.view;var hand=camera.transform.Find("Right first-person hand");
        camera.transform.Find("Hands soft light").GetComponent<Light>().intensity=.18f;
        var held=hand.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Held flashlight");
        net.heldTorch=held.gameObject;
        // Author a grip frame from the actual metacarpals, not the whole flashlight bounds.
        Transform Joint(string n)=>hand.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:RightHand"+n);
        var actor=hand.GetChild(0);
        var gripBlend=actor.GetComponent<HandGripBlend>();var so=new SerializedObject(gripBlend);
        var fingers=so.FindProperty("fingers");var holding=so.FindProperty("holding");var relaxed=so.FindProperty("relaxed");
        for(int i=0;i<fingers.arraySize;i++)((Transform)fingers.GetArrayElementAtIndex(i).objectReferenceValue).localRotation=Quaternion.Slerp(relaxed.GetArrayElementAtIndex(i).quaternionValue,holding.GetArrayElementAtIndex(i).quaternionValue,.87f);
        hand.localPosition=new Vector3(.245f,-.31f,.48f);
        // Remove the sharply cocked wrist roll while retaining the existing skeleton/skin weights.
        actor.localRotation=Quaternion.AngleAxis(-12,camera.transform.forward)*actor.localRotation;
        var wrist=Joint("");actor.localPosition-=hand.InverseTransformPoint(wrist.position);
        Vector3 grip=(Joint("Middle1").position+Joint("Middle3").position+Joint("Ring1").position+Joint("Ring3").position)*.25f;
        held.localScale*=.85f;
        // The lamp body is 22 cm long; place its rear handle, rather than its midpoint, inside the palm.
        held.position+=grip+camera.transform.forward*.025f-CorridorReviewBuilder.ModelBounds(held.gameObject).center;
        var knife=Object.Instantiate(CorridorReviewBuilder.LoadModel(KnifePath),hand);knife.name="Held knife";
        AlignKnife(knife,grip,camera.transform.rotation,.285f);
        net.heldKnife=knife;knife.SetActive(false);
        foreach(var t in hand.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
        foreach(var r in held.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
        foreach(var r in knife.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
        var pickup=Object.FindObjectsByType<HauntPickup>(FindObjectsSortMode.None).First(p=>p.Type==HauntPickup.Kind.Torch);
        var rack=new GameObject("起点刀具 · 每人一把").AddComponent<CoopKnifeRack>();rack.transform.position=pickup.transform.position;
        var item=Object.Instantiate(CorridorReviewBuilder.LoadModel(KnifePath),rack.transform);item.name="Table knife";
        AlignKnife(item,rack.transform.position,Quaternion.Euler(0,-35,90),.285f);
        var b=CorridorReviewBuilder.ModelBounds(item);item.transform.position+=Vector3.up*(pickup.transform.position.y-b.min.y);
        var trigger=rack.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=Vector3.up*.08f;trigger.size=new Vector3(.5f,.3f,.5f);net.knifeRack=rack.transform;
        net.swingSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtReview/Source/CombatAudio/knife-swish.wav");
        net.hitSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtReview/Source/CombatAudio/body-hit.wav");
        var layout=MazeLayout.Create();int[] stops={2,11,36};
        net.actors=inmates.OrderBy(n=>n.name,StringComparer.Ordinal).Select((n,i)=>
        {
            n.gameObject.SetActive(true);n.transform.SetPositionAndRotation(MazeLayout.Center(layout.Route[stops[i]])+Vector3.up*.08f,Quaternion.Euler(0,180,0));
            foreach(var c in n.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var t in n.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
            var target=n.gameObject.AddComponent<CapsuleCollider>();target.isTrigger=false;target.center=Vector3.up*.88f;target.height=1.76f;target.radius=.29f;
            var v=n.gameObject.AddComponent<CoopActorView>();v.id=i;v.knife=(GameObject)new SerializedObject(n).FindProperty("knife").objectReferenceValue;
            foreach(Transform child in v.knife.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            var blade=Object.Instantiate(CorridorReviewBuilder.LoadModel(KnifePath),v.knife.transform);AlignKnife(blade,v.knife.transform.position,v.knife.transform.rotation,.285f);
            v.knife.SetActive(false);Audio(v,net);return v;
        }).ToArray();
        net.partnerView=net.remote.gameObject.AddComponent<CoopActorView>();net.partnerView.partner=true;net.partnerView.id=-1;Audio(net.partnerView,net);
        var partnerHand=net.remote.GetComponentsInChildren<Transform>(true).First(t=>t.name=="hand_r"||t.name=="mixamorig:RightHand");
        var remoteKnife=Object.Instantiate(CorridorReviewBuilder.LoadModel(KnifePath),partnerHand);remoteKnife.name="Partner held knife";
        AlignKnife(remoteKnife,partnerHand.position+net.remote.forward*.055f,net.remote.rotation,.285f);remoteKnife.SetActive(false);net.partnerView.knife=remoteKnife;
        // Keep lighting dim but reserve slots for the two real flashlights.
        RenderSettings.ambientLight=new Color(.18f,.195f,.21f);RenderSettings.fogDensity=.014f;
        Debug.Log("PASS co-op art: grip/knife bindings, three real skinned inmates, partner weapon, cleaned room furnishings.");
    }
    static void Audio(CoopActorView actor,TwinNetwork net){actor.impact=net.hitSound;actor.swish=net.swingSound;actor.fall=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtReview/Source/CombatAudio/body-fall.wav");}
    static void AlignKnife(GameObject knife,Vector3 grip,Quaternion frame,float length)
    {
        knife.transform.rotation=Quaternion.identity;var size=CorridorReviewBuilder.ModelBounds(knife).size;
        Quaternion axis=size.x>=size.y&&size.x>=size.z?Quaternion.Euler(0,90,0):size.y>=size.z?Quaternion.Euler(90,0,0):Quaternion.identity;
        // This source model's black riveted handle is at +X after import; reverse the old orientation.
        knife.transform.rotation=frame*Quaternion.Euler(0,180,0)*axis;knife.transform.localScale*=length/Mathf.Max(size.x,size.y,size.z);
        knife.transform.position+=grip+frame*Vector3.forward*(length*.24f)-CorridorReviewBuilder.ModelBounds(knife).center;
    }
    static void CleanRooms()
    {
        var rooms=Object.FindObjectsByType<PrisonRoom>(FindObjectsSortMode.None);int removed=0;
        string[] furniture={"old_bed_frame","worn_metal_rack","metal_office_desk","industrial_storage_cart","metal_stool","tool_cart","drawer_cabinet","wheelchair","metal_toolbox"};
        // Old cell props survived the expanded room shell. Remove only their model roots and matching old boxes.
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).ToArray())
        {
            if(t==null||t.GetComponentInParent<PrisonRoom>()!=null||t.GetComponentInParent<PrisonHands>()!=null)continue;
            if(!furniture.Any(n=>t.name.StartsWith(n))||(!t.name.Contains("Clone")&&!t.name.EndsWith("collision")))continue;
            var rs=t.GetComponentsInChildren<Renderer>();var pos=rs.Length>0?CorridorReviewBuilder.ModelBounds(t.gameObject).center:t.position;
            if(rooms.Any(r=>Mathf.Abs(pos.x-r.volume.center.x)<3.7f&&Mathf.Abs(pos.z-r.volume.center.z)<3.7f)){Object.DestroyImmediate(t.gameObject);removed++;}
        }
        foreach(var room in rooms)
        {
            foreach(var box in room.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Room furnishing collision").ToArray())Object.DestroyImmediate(box.gameObject);
            var props=room.transform.Cast<Transform>().Where(t=>furniture.Any(n=>t.name.StartsWith(n))).ToArray();
            var desk=props.FirstOrDefault(t=>t.name.StartsWith("metal_office_desk"));
            if(desk!=null)
            {
                var b=CorridorReviewBuilder.ModelBounds(desk.gameObject);desk.localScale*=.75f/b.size.y;var after=CorridorReviewBuilder.ModelBounds(desk.gameObject);desk.position+=new Vector3(b.center.x-after.center.x,-after.min.y,b.center.z-after.center.z);
                var radio=room.transform.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("vintage_radio"));if(radio!=null){var rb=CorridorReviewBuilder.ModelBounds(radio.gameObject);radio.position+=new Vector3(b.center.x-rb.center.x,.75f-rb.min.y,b.center.z-rb.center.z);}
                var stool=props.FirstOrDefault(t=>t.name.StartsWith("metal_stool"));if(stool!=null){var centre=room.volume.center;var dir=(new Vector3(centre.x,0,centre.z)-new Vector3(b.center.x,0,b.center.z)).normalized;var sb=CorridorReviewBuilder.ModelBounds(stool.gameObject);stool.position+=new Vector3(b.center.x+dir.x*.95f-sb.center.x,-sb.min.y,b.center.z+dir.z*.95f-sb.center.z);}
            }
            var cart=props.FirstOrDefault(t=>t.name.StartsWith("tool_cart"));var toolbox=props.FirstOrDefault(t=>t.name.StartsWith("metal_toolbox"));
            if(cart!=null&&toolbox!=null){var cb=CorridorReviewBuilder.ModelBounds(cart.gameObject);var tb=CorridorReviewBuilder.ModelBounds(toolbox.gameObject);toolbox.position+=new Vector3(cb.center.x-tb.center.x,cb.max.y-tb.min.y,cb.center.z-tb.center.z);}
            var power=room.transform.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("power_box"));if(power!=null)
            {
                var pb=CorridorReviewBuilder.ModelBounds(power.gameObject);power.position+=Vector3.up*(1.25f-pb.center.y);
                var gap=new[]{pb.min.x-room.volume.min.x,room.volume.max.x-pb.max.x,pb.min.z-room.volume.min.z,room.volume.max.z-pb.max.z};int side=Array.IndexOf(gap,gap.Min());
                var offset=side==0?Vector3.left:side==1?Vector3.right:side==2?Vector3.back:Vector3.forward;power.position+=offset*Mathf.Max(0,gap[side]-.1f);
            }
            foreach(var prop in props){if(prop==toolbox&&cart!=null)continue;var b=CorridorReviewBuilder.ModelBounds(prop.gameObject);var box=new GameObject("Room furnishing collision").AddComponent<BoxCollider>();box.transform.SetParent(room.transform);box.transform.position=b.center;box.size=b.size;}
        }
        Debug.Log("PASS removed "+removed+" legacy furniture/colliders inside expanded rooms; aligned desk/stool/toolbox and wall power box.");
    }
}
