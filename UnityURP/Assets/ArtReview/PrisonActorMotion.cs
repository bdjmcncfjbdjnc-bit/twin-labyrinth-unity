using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class PrisonActorMotion : MonoBehaviour
{
    private Animator animator;
    private CharacterController body;
    private Transform shoulder, elbow, wrist;
    private string gesture;
    private float until;
    private float strikeUntil;
    private Vector3 strikeTarget;
    private string playing;
    private float idleTime;
    private MazeSession session;
    private void Awake()
    {
        animator=GetComponent<Animator>();body=GetComponent<CharacterController>();session=FindFirstObjectByType<MazeSession>();
        foreach(var t in GetComponentsInChildren<Transform>())
        {
            if(t.name=="mixamorig:RightArm")shoulder=t;
            if(t.name=="mixamorig:RightForeArm")elbow=t;
            if(t.name=="mixamorig:RightHand")wrist=t;
        }
        idleTime=Time.time+Random.Range(8f,16f);
    }
    public void Gesture(string pose,float duration){gesture=pose;until=Time.time+duration;}
    public void Strike(Vector3 point){strikeTarget=point;strikeUntil=Time.time+.6f;Gesture("Argue",.6f);}
    private void Update()
    {
        if(session==null || session.IsOver) return;
        float speed=new Vector2(body.velocity.x,body.velocity.z).magnitude;
        if(speed<.1f && Time.time>=idleTime && Time.time>=until)
        {Gesture("Think",2.5f);idleTime=Time.time+Random.Range(14f,24f);}
        string pose=Time.time<until ? gesture : speed>.1f ? "Walk" : "Idle";
        if(pose!=playing){animator.CrossFade("Base Layer."+pose,.2f);playing=pose;}
        animator.speed=pose=="Walk" ? Mathf.Clamp(speed/1.65f,.75f,1.8f) : 1f;
    }
    private void LateUpdate()
    {
        if(Time.time>=strikeUntil)return;
        float t=1f-(strikeUntil-Time.time)/.6f;
        BoneReach.Solve(shoulder,elbow,wrist,strikeTarget,transform.right,Mathf.Sin(t*Mathf.PI)*.9f);
    }
}
