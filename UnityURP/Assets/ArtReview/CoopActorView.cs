using System.Linq;
using UnityEngine;

// Network state drives poses; no local AI or client-side damage decisions.
[DefaultExecutionOrder(310)]
public sealed class CoopActorView : MonoBehaviour
{
    public int id;
    public GameObject knife;
    public AudioClip impact,fall,swish;
    public bool partner;
    private Animator animator;
    private Transform hips,spine,head,arm,forearm,wrist,leftThigh,rightThigh,leftShin,rightShin;
    private Transform[] bones;
    private Quaternion[] fallenPose;
    private Vector3[] fallenPositions;
    private Vector3 hipsLocal,ground;
    private float hurtAt=-10,strikeAt=-10,deathAt=-10,heading;
    private int hits,swings;
    private bool dead,floorAdjusted;
    private float floorLift;
    private GameObject droppedKnife;
    private string stateName;
    private AudioSource sound;
    public bool Dead=>dead;
    public int HitCount=>hits;
    void Awake()
    {
        animator=GetComponent<Animator>();bones=GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s=>s.bones).Where(t=>t!=null).Distinct().ToArray();
        Transform Joint(string suffix)=>bones.FirstOrDefault(t=>t.name=="mixamorig:"+suffix);
        hips=Joint("Hips");spine=Joint("Spine1")??Joint("Spine");head=Joint("Head");arm=Joint("RightArm");forearm=Joint("RightForeArm");wrist=Joint("RightHand");
        leftThigh=Joint("LeftUpLeg");rightThigh=Joint("RightUpLeg");leftShin=Joint("LeftLeg");rightShin=Joint("RightLeg");
        if(hips==null){Transform B(string n)=>bones.FirstOrDefault(t=>t.name==n);hips=B("pelvis");spine=B("spine_02");head=B("head");arm=B("upperarm_r");forearm=B("lowerarm_r");wrist=B("hand_r");leftThigh=B("thigh_l");rightThigh=B("thigh_r");leftShin=B("calf_l");rightShin=B("calf_r");}
        if(hips!=null)hipsLocal=transform.InverseTransformPoint(hips.position);
        sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=1;sound.minDistance=1.2f;sound.maxDistance=13;sound.rolloffMode=AudioRolloffMode.Linear;
    }
    public void Apply(float x,float z,float yaw,int hp,string mode,int hit,int swing,bool armed,bool snap=false)
    {
        if(animator==null)Awake();
        if(hit!=hits){if(hits>0||hit>0){hurtAt=Time.time;if(impact)sound.PlayOneShot(impact,.28f);}hits=hit;}
        if(swing!=swings){strikeAt=Time.time;swings=swing;if(swish)sound.PlayOneShot(swish,.24f);}
        if(hp<=0&&!dead)
        {
            if(snap){animator.Play("Base Layer."+(partner?"Prisoner_idle":"Idle"),0,0);animator.Update(0);}
            dead=true;deathAt=Time.time-(snap?2f:0f);ground=new Vector3(x,.08f,z);heading=yaw;floorAdjusted=false;floorLift=0;
            fallenPose=bones.Select(t=>t.localRotation).ToArray();fallenPositions=bones.Select(t=>t.localPosition).ToArray();animator.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;
            if(fall&&!snap)sound.PlayOneShot(fall,.32f);
            if(armed&&knife!=null&&!partner){droppedKnife=Instantiate(knife);droppedKnife.name="Dropped inmate knife";droppedKnife.transform.SetPositionAndRotation(ground+Vector3.up*.025f+Quaternion.Euler(0,yaw,0)*Vector3.right*.45f,Quaternion.Euler(90,yaw,0));droppedKnife.SetActive(true);}
        }
        if(hp>0&&dead){dead=false;animator.enabled=true;transform.rotation=Quaternion.Euler(0,yaw,0);foreach(var c in GetComponentsInChildren<Collider>())if(c is CapsuleCollider)c.enabled=true;}
        if(knife!=null)knife.SetActive(armed&&!dead);
        if(dead)return;
        var next=new Vector3(x,.08f,z);transform.position=snap||Vector3.Distance(transform.position,next)>3?next:Vector3.Lerp(transform.position,next,1-Mathf.Exp(-14*Time.deltaTime));
        transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,yaw,0),1-Mathf.Exp(-12*Time.deltaTime));
        string pose=mode=="chase"||mode=="return"||mode=="flee"||mode=="walk"?"Walk":"Idle";
        if(partner)pose=pose=="Walk"?"Prisoner_walk":"Prisoner_idle";
        if(stateName!=pose){animator.CrossFade("Base Layer."+pose,.16f);stateName=pose;}
    }
    void LateUpdate()
    {
        if(dead)
        {
            float t=Mathf.Clamp01((Time.time-deathAt)/1.15f),bend=Mathf.Sin(Mathf.Min(1,t*1.8f)*Mathf.PI*.5f);
            for(int i=0;i<bones.Length;i++){bones[i].localRotation=fallenPose[i];bones[i].localPosition=fallenPositions[i];}
            transform.rotation=Quaternion.Euler(0,heading,0)*Quaternion.Euler(8*t,0,82*Mathf.SmoothStep(0,1,t));
            transform.position=ground+Vector3.up*(Mathf.Lerp(hipsLocal.y,.24f,Mathf.SmoothStep(0,1,t))+floorLift)-transform.rotation*Vector3.Scale(hipsLocal,transform.lossyScale);
            Turn(leftThigh,transform.right,-35*bend);Turn(rightThigh,transform.right,-50*bend);Turn(leftShin,transform.right,70*bend);Turn(rightShin,transform.right,85*bend);
            Turn(spine,transform.right,15*bend);Turn(head,transform.forward,12*bend);
            if(arm!=null)Turn(arm,transform.forward,-25*bend);
            if(forearm!=null)Turn(forearm,transform.up,35*bend);
            if(t>=1&&!floorAdjusted)
            {
                float min=float.MaxValue;var mesh=new Mesh();
                foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices)min=Mathf.Min(min,skin.transform.TransformPoint(vertex).y);}
                Destroy(mesh);floorLift=Mathf.Clamp(.025f-min,-.5f,.6f);floorAdjusted=true;
            }
            return;
        }
        float hurt=1-Mathf.Clamp01((Time.time-hurtAt)/.48f);if(hurt>0){Turn(spine,transform.right,-14*Mathf.Sin(hurt*Mathf.PI));Turn(head,transform.right,9*hurt);}
        float strike=(Time.time-strikeAt)/.58f;
        if(strike>=0&&strike<1)BoneReach.Solve(arm,forearm,wrist,transform.position+Vector3.up*1.22f+transform.forward*.85f,transform.right,Mathf.Sin(strike*Mathf.PI)*.95f);
    }
    static void Turn(Transform bone,Vector3 axis,float angle){if(bone!=null)bone.rotation=Quaternion.AngleAxis(angle,axis)*bone.rotation;}
}
