using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class TwinNetwork : MonoBehaviour
{
    public Transform player;
    public Camera view;
    public Light torch;
    public Transform remote;
    public ReviewDoor[] doors;
    public HauntRelay[] relays;
    public int[] exitDoors;
    public Light[] fixtures;
    public CoopActorView[] actors;
    public CoopActorView partnerView;
    public GameObject heldTorch,heldKnife;
    public Transform knifeRack;
    public AudioClip swingSound,hitSound;
    private CharacterController body;
    private HauntExperience audioRig;
    private PrisonHands hands;
    private Animator remoteAnimator;
    private Light remoteTorch,fill;
    private State state;
    private string code="",status="创建房间，或输入朋友发来的六位房间码。",notice="";
    private int slot=-1,sequence;
    private bool connected,menu=true,torchOn=true,walking;
    private float pitch,nextSend,lastState,noticeUntil,nextLights,mouseReadyAfter;
    private GUIStyle text,small,title,button,input;
    private Vector3 lastRemote;
    private string weapon="torch";
    private int ownSwing,ownHit,lastAlarm,ownHealth=100;
    private float damageUntil,hitUntil,nextRevive;
    private AudioSource feedback;
    private Vector3 eyeRest;
    private const string Controls="WASD 移动 · Shift 奔跑 · F 交互 / 按住救援 · 1 手电 · 2 刀 · 左键攻击 · L 灯 · Esc 菜单";
    [Serializable] public class Peer{public int slot,hp,swing,hit,kills;public float x,z,yaw,pitch,revive;public bool torch,online,hasKnife;public string weapon;}
    [Serializable] public class Actor{public int id,hp,hit,swing;public float x,z,yaw;public string mode;public bool armed;}
    [Serializable] public class State{public string type,code,token,message;public int slot,alarm;public bool a,b,won,failed;public bool[] doors;public Peer[] players;public Actor[] actors;}
    [Serializable] class Join{public string mode,code;}
    [Serializable] class Frame{public int seq,target;public float x,z,yaw,pitch;public bool torch;public string action,weapon;}
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TL_Connect(string message);
    [DllImport("__Internal")] static extern void TL_Send(string message);
    [DllImport("__Internal")] static extern void TL_Close();
    [DllImport("__Internal")] static extern void TL_InitialRoom();
#else
    static void TL_Connect(string message){Debug.Log("Online mode requires the browser build.");}
    static void TL_Send(string message){}
    static void TL_Close(){}
    static void TL_InitialRoom(){}
#endif
    void Start()
    {
        Time.timeScale=1;AudioListener.pause=false;Application.runInBackground=true;
        body=player.GetComponent<CharacterController>();audioRig=player.GetComponent<HauntExperience>();hands=view.GetComponent<PrisonHands>();
        feedback=gameObject.AddComponent<AudioSource>();feedback.spatialBlend=0;eyeRest=view.transform.localPosition;
        var pickup=FindObjectsByType<HauntPickup>(FindObjectsSortMode.None).FirstOrDefault(p=>p.Type==HauntPickup.Kind.Torch);
        if(pickup!=null)audioRig.Collect(pickup);
        foreach(var item in FindObjectsByType<HauntPickup>(FindObjectsSortMode.None))item.gameObject.SetActive(false);
        remoteAnimator=remote.GetComponent<Animator>();remote.gameObject.SetActive(false);
        torch.intensity=2f;torch.range=22;torch.spotAngle=68;torch.innerSpotAngle=24;torch.shadows=LightShadows.Soft;torch.shadowBias=.015f;torch.shadowNormalBias=.12f;
        torch.transform.SetParent(view.transform,false);torch.transform.localPosition=new Vector3(.06f,-.06f,.03f);torch.transform.localRotation=Quaternion.identity;
        torch.enabled=true;torch.renderMode=LightRenderMode.ForcePixel;
        fill=new GameObject("Online near spill").AddComponent<Light>();fill.transform.SetParent(view.transform,false);fill.type=LightType.Point;fill.range=3.2f;fill.intensity=.15f;fill.shadows=LightShadows.Soft;fill.cullingMask=~(1<<29);
        remoteTorch=new GameObject("Partner flashlight").AddComponent<Light>();remoteTorch.type=LightType.Spot;remoteTorch.intensity=1.4f;remoteTorch.range=20;remoteTorch.spotAngle=68;remoteTorch.innerSpotAngle=24;remoteTorch.shadows=LightShadows.Soft;remoteTorch.cullingMask=~(1<<29);remoteTorch.enabled=false;
        for(int i=0;i<doors.Length;i++)doors[i].ApplyNetworkState(false,exitDoors.Contains(i));
        foreach(var light in fixtures)if(light!=null){light.enabled=true;light.intensity=Mathf.Clamp(light.intensity,.45f,1.1f);}
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        TL_InitialRoom();
    }
    public void CreateRoom(){Leave();status="正在创建房间…";TL_Connect(JsonUtility.ToJson(new Join{mode="create",code=""}));}
    public void JoinRoom(string room){if(room.Length!=6||!room.All(char.IsDigit)){status="请输入六位数字房间码";return;}Leave();code=room;status="正在加入房间…";TL_Connect(JsonUtility.ToJson(new Join{mode="join",code=code}));}
    void Leave(){TL_Close();connected=false;slot=-1;state=null;remote.gameObject.SetActive(false);remoteTorch.enabled=false;menu=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    void OnDestroy(){TL_Close();}
    [UnityEngine.Scripting.Preserve] public void Receive(string json)
    {
        var message=JsonUtility.FromJson<State>(json);
        if(message.type=="hello"){slot=message.slot;code=message.code;connected=true;sequence=0;status="已连接房间 "+code;Debug.Log("COOP JOIN slot="+slot+" room="+code);return;}
        if(message.type=="error"||message.type=="disconnected"){connected=false;menu=true;status=message.message;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
        if(message.type=="notice"){notice=message.message;noticeUntil=Time.unscaledTime+4;return;}
        if(message.type!="state"||slot<0)return;
        bool first=state==null;state=message;lastState=Time.unscaledTime;connected=true;status="连接正常 · 房间 "+code;
        if(state.players.Count(p=>p.online)<2){menu=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        if(state.a)relays[0].MarkActive();if(state.b)relays[1].MarkActive();
        var self=state.players.FirstOrDefault(p=>p.slot==slot);
        if(first&&self!=null){torchOn=self.torch;weapon=self.weapon??"torch";pitch=self.pitch;player.rotation=Quaternion.Euler(0,self.yaw,0);view.transform.localRotation=Quaternion.Euler(pitch,0,0);ownSwing=self.swing;ownHit=self.hit;}
        if(self!=null){if(self.swing!=ownSwing){ownSwing=self.swing;hands.Strike();if(swingSound)feedback.PlayOneShot(swingSound,.18f);}if(self.hit!=ownHit){ownHit=self.hit;if(self.hp<ownHealth){damageUntil=Time.unscaledTime+.4f;if(hitSound)feedback.PlayOneShot(hitSound,.25f);}}ownHealth=self.hp;}
        if(message.actors!=null)foreach(var n in message.actors){if(n.id<0||n.id>=actors.Length)continue;var actor=actors[n.id];if(!first&&n.hit>actor.HitCount)hitUntil=Time.unscaledTime+.18f;if(!first&&n.hp<=0&&!actor.Dead){hitUntil=Time.unscaledTime+.5f;notice="囚犯倒地。留意四周，继续与朋友会合。";noticeUntil=Time.unscaledTime+3;}actor.Apply(n.x,n.z,n.yaw,n.hp,n.mode,n.hit,n.swing,n.armed,first);}
        if(message.alarm>lastAlarm){notice="误伤普通囚犯：本局记录一次无辜伤亡。";noticeUntil=Time.unscaledTime+5;}lastAlarm=message.alarm;
        if(knifeRack!=null)foreach(var mesh in knifeRack.GetComponentsInChildren<Renderer>())mesh.enabled=!(state.players.Length==2&&state.players.All(p=>p.hasKnife));
        if(self!=null&&(first||Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(self.x,self.z))>1.2f))
        {body.enabled=false;player.position=new Vector3(self.x,.08f,self.z);body.enabled=true;}
        for(int i=0;i<doors.Length;i++){
            bool open=i<state.doors.Length&&state.doors[i];bool changed=doors[i].IsOpen!=open;
            doors[i].ApplyNetworkState(open,exitDoors.Contains(i)&&!state.b);if(changed)audioRig.DoorSound(doors[i]);
        }
        if(state.won||state.failed){menu=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
    bool Ready=>connected&&state?.players?.Count(p=>p.online)==2&&Time.unscaledTime-lastState<3;
    void Update()
    {
        if(body==null)return;
        if(Time.unscaledTime>=nextLights){nextLights=Time.unscaledTime+.2f;SelectLights();}
        if(Ready&&!menu&&Input.GetMouseButtonDown(0)&&Cursor.lockState!=CursorLockMode.Locked)EnterView();
        if(Input.GetKeyDown(KeyCode.Escape)){if(menu)EnterView();else{menu=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
        if(connected&&Time.unscaledTime-lastState>5){status="等待服务器响应…";}
        var self=state?.players?.FirstOrDefault(p=>p.slot==slot);
        if(!menu&&self!=null){if(Input.GetKeyDown(KeyCode.Alpha1)){weapon="torch";torchOn=true;}if(Input.GetKeyDown(KeyCode.Alpha2)){if(self.hasKnife)weapon="knife";else{notice="起点桌上有刀，靠近按 F 拿取。";noticeUntil=Time.unscaledTime+3;}}if(Input.GetKeyDown(KeyCode.L)){weapon="torch";torchOn=!torchOn;}}
        if(Ready&&!menu&&!state.won&&!state.failed&&self.hp>0&&Cursor.lockState==CursorLockMode.Locked)
        {
            if(Time.unscaledTime>=mouseReadyAfter){player.Rotate(0,Input.GetAxisRaw("Mouse X")*2,0);pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-80,80);view.transform.localRotation=Quaternion.Euler(pitch,0,0);}
            float horizontal=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float vertical=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            var movement=Vector3.ClampMagnitude(player.right*horizontal+player.forward*vertical,1);
            bool sprint=Input.GetKey(KeyCode.LeftShift);float speed=audioRig.RequestedSpeed(sprint);var before=player.position;
            body.Move((movement*speed+Vector3.down*2)*Time.deltaTime);
            audioRig.UpdateMovement(movement.sqrMagnitude>.01f,sprint);audioRig.RechargeOnlineTorch();audioRig.MovementFeedback(player.position-before);hands.Step((player.position-before).magnitude);
            var down=state.players.FirstOrDefault(p=>p.slot!=slot&&p.hp<=0);
            if(down!=null&&Input.GetKey(KeyCode.F)&&Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(down.x,down.z))<2){if(Time.unscaledTime>=nextRevive){nextRevive=Time.unscaledTime+.1f;Send("revive",down.slot);}}
            else if(Input.GetKeyDown(KeyCode.F))Interact();
            if(weapon=="knife"&&Input.GetMouseButtonDown(0)&&Time.unscaledTime>mouseReadyAfter){int target=-1;if(Physics.SphereCast(view.transform.position,.16f,view.transform.forward,out var struck,2.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)){var actor=struck.collider.GetComponentInParent<CoopActorView>();if(actor!=null&&!actor.partner)target=actor.id;}Send("attack",target);}
        }
        else {audioRig.UpdateMovement(false,false);audioRig.RechargeOnlineTorch();hands.Step(0);}
        // This mode intentionally has no power flicker or battery depletion.
        bool lit=torchOn&&weapon=="torch"&&(self==null||self.hp>0);
        torch.enabled=lit;fill.enabled=lit;heldTorch.SetActive(weapon=="torch");heldKnife.SetActive(weapon=="knife"&&self!=null&&self.hasKnife);hands.KnifeEquipped=heldKnife.activeSelf;
        // A close wall must not turn into a blown-out white disk or swallow the light origin.
        float wall=Physics.Raycast(view.transform.position,view.transform.forward,out var near,2.4f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?near.distance:2.4f;
        torch.intensity=Mathf.Clamp(wall*wall*.32f,.018f,2f);
        fill.intensity=Mathf.Min(.15f,wall*wall*.12f);
        view.transform.localPosition=Vector3.Lerp(view.transform.localPosition,eyeRest+(self!=null&&self.hp<=0?Vector3.down*.85f:Vector3.zero),1-Mathf.Exp(-6*Time.deltaTime));
        if(connected&&Time.unscaledTime>=nextSend){nextSend=Time.unscaledTime+.08f;Send("",-1);}
        UpdatePartner();
    }
    void Send(string action,int target){TL_Send(JsonUtility.ToJson(new Frame{seq=++sequence,x=player.position.x,z=player.position.z,yaw=player.eulerAngles.y,pitch=pitch,torch=torchOn,weapon=weapon,action=action,target=target}));}
    void EnterView(){menu=false;mouseReadyAfter=Time.unscaledTime+.2f;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    void SelectLights()
    {
        var closest=fixtures.Where(l=>l!=null&&(l.transform.position-view.transform.position).sqrMagnitude<100&&!Physics.Linecast(view.transform.position,l.transform.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)).OrderBy(l=>(l.transform.position-view.transform.position).sqrMagnitude).Take(2).ToArray();
        foreach(var l in fixtures)if(l!=null){l.enabled=Array.IndexOf(closest,l)>=0;if(l.enabled)l.shadows=LightShadows.Soft;}
    }
    void Interact()
    {
        if(knifeRack!=null&&Vector3.Distance(player.position+Vector3.up*.8f,knifeRack.position)<2.4f&&!state.players.First(p=>p.slot==slot).hasKnife){Send("pickupKnife",0);return;}
        if(!Physics.Raycast(view.transform.position,view.transform.forward,out var hit,3.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide))return;
        var door=hit.collider.GetComponentInParent<ReviewDoor>();var relay=hit.collider.GetComponentInParent<HauntRelay>();
        if(door!=null){hands.Interact(hit.point);Send("door",Array.IndexOf(doors,door));}
        else if(relay!=null){hands.Interact(hit.point);Send("relay",Array.IndexOf(relays,relay));}
    }
    void UpdatePartner()
    {
        var peer=state?.players?.FirstOrDefault(p=>p.slot!=slot&&p.online);bool visible=connected&&peer!=null;
        remote.gameObject.SetActive(visible);remoteTorch.enabled=visible&&peer.torch;if(!visible)return;
        var goal=new Vector3(peer.x,.08f,peer.z);
        bool moving=(goal-lastRemote).sqrMagnitude>.00004f;lastRemote=goal;
        partnerView.Apply(peer.x,peer.z,peer.yaw,peer.hp,moving?"walk":"idle",peer.hit,peer.swing,peer.weapon=="knife");
        remoteTorch.transform.SetPositionAndRotation(remote.position+Vector3.up*1.48f+remote.forward*.23f,Quaternion.Euler(peer.pitch,peer.yaw,0));
    }
    void Styles(){if(text!=null)return;text=new GUIStyle(GUI.skin.label){font=PrisonChinese.Font,fontSize=20,wordWrap=true,normal={textColor=new Color(.9f,.89f,.85f)}};small=new GUIStyle(text){fontSize=16};title=new GUIStyle(text){fontSize=40};button=new GUIStyle(GUI.skin.button){font=PrisonChinese.Font,fontSize=20};input=new GUIStyle(GUI.skin.textField){font=PrisonChinese.Font,fontSize=28,alignment=TextAnchor.MiddleCenter};}
    void OnGUI()
    {
        Styles();GUI.depth=-200;var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2),Quaternion.identity,Vector3.one*scale);
        if(menu||!Ready||state?.won==true||state?.failed==true){
            Shade(0,0,1280,720,.97f);Label(90,70,1050,60,state?.won==true?"你们一起走出了监区":state?.failed==true?"两人失去行动能力":"双生迷城 · 双人联机",title);
            Label(90,151,1030,76,"异常封锁切断了监区供电。你和朋友必须接通 A、共同启动 B，再一起找到出口。\n起点桌上可拿刀。囚犯身份无法靠外表判断；对方亮刀时可后退，也可近身自卫。",text);
            if(slot<0){Label(90,258,600,40,status,small);if(GUI.Button(new Rect(90,322,280,58),"创建双人房间",button))CreateRoom();
                code=GUI.TextField(new Rect(420,322,260,58),code,6,input);if(GUI.Button(new Rect(710,322,230,58),"输入房间码加入",button))JoinRoom(code);
            }else{
                Label(90,255,1000,65,"房间码  "+code,title);Label(90,329,1000,55,Ready?"两名玩家已连接。各自点击进入监区即可。":"等待另一名玩家。把房间码或当前网址发给朋友。",text);
                Label(90,390,1000,35,status,small);
                if(Ready&&!state.won&&!state.failed&&GUI.Button(new Rect(90,458,300,58),"进入监区",button))EnterView();
                if(state.won||state.failed)Label(90,452,310,72,"无辜伤亡："+state.alarm+"\n离开后可创建新房间",text);
                if(GUI.Button(new Rect(430,458,220,58),"离开房间",button)){Leave();status="已离开房间。";}
            }
            Label(90,596,1080,60,Controls+"\n菜单只暂停你自己的操作，不暂停朋友；掉线后会尝试重连，两分钟内保留房间。",small);
        }else{
            Shade(24,22,390,82,.7f);Label(42,33,355,30,"房间 "+code+"  /  玩家 "+(slot+1)+"  /  2 人在线",text);
            Label(42,70,355,26,weapon=="knife"?"持刀 · 左键攻击 · 1 换手电":"手电 "+(torchOn?"开启":"关闭")+" · L 开关 · 2 拔刀",small);
            Shade(835,22,420,105,.7f);Label(854,34,380,80,!state.a?"共同目标：找到配电 A，按 F 接通。":!state.b?"共同目标：两人靠近配电 B，按 F 协作供电。":"出口已打开。两人共同到达地面通道。",text);
            var peer=state.players.FirstOrDefault(p=>p.slot!=slot);if(peer!=null)Label(28,655,550,30,"朋友距你 "+Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(peer.x,peer.z)).ToString("0")+" 米  ·  供电 A "+(state.a?"●":"○")+"  B "+(state.b?"●":"○"),small);
            var self=state.players.First(p=>p.slot==slot);Label(28,615,620,34,"生命 "+self.hp+" / 100  ·  "+(self.hasKnife?"刀已携带":"起点桌上有刀 · 靠近按 F")+"  ·  Esc 菜单",small);
            var danger=state.actors?.FirstOrDefault(n=>n.hp>0&&(n.mode=="warning"||n.mode=="chase"||n.mode=="attack")&&Vector2.Distance(new Vector2(n.x,n.z),new Vector2(player.position.x,player.position.z))<6);
            if(danger!=null&&self.hp>0)Label(410,410,650,32,danger.mode=="warning"?"对方亮刀：后退拉开距离，或按 2 自卫。":"囚犯正在接近：退离这段走廊，避免贴身停留。",small);
            if(self.hp<=0)Label(370,260,650,80,"你已倒地：等待朋友靠近，按住 F 扶起。",text);
            else if(peer!=null&&peer.hp<=0)Label(390,450,580,46,"朋友倒地：靠近后按住 F 救援 "+Mathf.RoundToInt(self.revive*100)+"%",text);
            Label(632,345,20,25,"·",text);
            string hint="";if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,3.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)){
                var d=hit.collider.GetComponentInParent<ReviewDoor>();var r=hit.collider.GetComponentInParent<HauntRelay>();
                if(d!=null)hint=d.IsLocked?"出口待供电":"F  "+(d.IsOpen?"关闭铁门":"打开铁门");if(r!=null)hint="F  启动配电 "+(r.Index==0?"A":"B（需要两人靠近）");
            }
            if(!self.hasKnife&&knifeRack!=null&&Vector3.Distance(player.position+Vector3.up*.8f,knifeRack.position)<2.4f)hint="F  从桌上拿刀（每人一把）";
            if(hint!=""){Shade(390,505,500,50,.8f);Label(412,514,456,35,hint,text);}
            if(Time.unscaledTime<damageUntil){var color=GUI.color;GUI.color=new Color(.55f,.05f,.035f,.32f);GUI.DrawTexture(new Rect(0,0,1280,12),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,708,1280,12),Texture2D.whiteTexture);GUI.color=color;}
            if(Time.unscaledTime<hitUntil)Label(617,325,60,45,"×",title);
            if(Time.unscaledTime<noticeUntil)Label(350,570,650,60,notice,text);
        }
        GUI.matrix=old;
    }
    void Label(float x,float y,float w,float h,string value,GUIStyle style)=>GUI.Label(new Rect(x,y,w,h),value,style);
    static void Shade(float x,float y,float w,float h,float alpha){var color=GUI.color;GUI.color=new Color(0,0,0,alpha);GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=color;}
}
