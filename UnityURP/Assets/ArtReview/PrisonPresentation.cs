using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class PrisonPresentation : MonoBehaviour
{
    public static bool Blocked { get; private set; }
    private MazeSession session; private HauntExperience equipment; private LastRollCall mission;
    private CorridorReview controls; private MazeCompanion partner; private TransferInterlock gate;
    private PrisonGuidance guidance; private PrisonIncidents incidents;
    private bool introduction=true, help; private GUIStyle text, title, small, button;
    private float hintCooldown;
    private static readonly Color Ink=new Color(0f,0f,0f,.68f);
    private static readonly Color Paper=new Color(.89f,.88f,.83f);
    private static readonly Color Accent=new Color(.72f,.62f,.43f);
    private void Awake()
    {
        session=GetComponent<MazeSession>(); equipment=GetComponent<HauntExperience>(); mission=GetComponent<LastRollCall>();
        controls=GetComponent<CorridorReview>(); gate=GetComponent<TransferInterlock>(); guidance=GetComponent<PrisonGuidance>();
        incidents=GetComponent<PrisonIncidents>(); partner=session.PartnerTransform.GetComponent<MazeCompanion>();
        introduction=!System.Array.Exists(System.Environment.GetCommandLineArgs(),x=>x=="-audit-round");
        SetPause(introduction);
        Debug.Log("PRESENTATION intro="+introduction+" paused="+Blocked+" timeScale="+Time.timeScale);
    }
    private void OnDestroy(){Blocked=false;Time.timeScale=1;AudioListener.pause=false;}
    private void SetPause(bool pause)
    {
        Blocked=pause; Time.timeScale=pause?0:1; AudioListener.pause=pause;
        Cursor.lockState=pause?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=pause;
    }
    private void Begin(){introduction=false;help=false;SetPause(false);guidance.Reveal();Debug.Log("PRESENTATION entered game: paused="+Blocked+" timeScale="+Time.timeScale);}
    private void Update()
    {
        if(RoundPlaytest.Active)return;
        bool toggle=Input.GetKeyDown(KeyCode.Escape);
#if !UNITY_WEBGL
        toggle|=Input.GetKeyDown(KeyCode.Tab);
#endif
        if(toggle)ToggleNotebook();
        if(!Blocked&&!session.IsOver&&Input.GetKeyDown(KeyCode.H)&&Time.time>=hintCooldown)
        {guidance.Reveal();hintCooldown=Time.time+20;}
        if(session.IsOver){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
    [UnityEngine.Scripting.Preserve]
    public void ToggleNotebook()
    {
        if(introduction||session==null||session.IsOver)return;
        help=!help;SetPause(help);
    }
    private void Styles()
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=PrisonChinese.Font,fontSize=18,wordWrap=true,richText=false,normal={textColor=Paper}};
        small=new GUIStyle(text){fontSize=15,normal={textColor=new Color(.62f,.63f,.59f)}};
        title=new GUIStyle(text){fontSize=30,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=PrisonChinese.Font,fontSize=18,padding=new RectOffset(14,14,8,8)};
    }
    private void OnGUI()
    {
        Styles();GUI.depth=-100;
        Matrix4x4 previous=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2),Quaternion.identity,Vector3.one*scale);
        if(introduction||help){DrawBriefing();GUI.matrix=previous;return;}
        if(session.IsOver)
        {
            Panel(340,200,600,300);Label(380,235,520,55,session.Escaped?"你们一起逃了出来":"最终封锁已完成",title);
            Label(380,305,520,100,session.Escaped?"你没有丢下同牢的伙伴。地面通道已经打开。\n"+session.PresentationResult:"出口已经封死。再试一次：先完成准备，再供电，最后双门接应。\n"+session.PresentationResult);
            Label(380,440,520,30,"按 R 重新开始",small);GUI.matrix=previous;return;
        }
        var protocol=session.PresentationProtocol; if(protocol==null){GUI.matrix=previous;return;}
        Panel(28,24,510,105);Label(46,35,125,22,"距最终封锁",small);
        int remaining=Mathf.CeilToInt(protocol.Remaining);Label(45,60,125,44,$"{remaining/60:00}:{remaining%60:00}",title);
        Fill(new Rect(180,42,1,62),new Color(1,1,1,.18f));
        Label(199,35,320,28,PrisonChinese.T(session.PresentationProtocolName)+$"  /  {Mathf.CeilToInt(protocol.ProtocolRemaining)} 秒");
        Label(199,72,320,48,protocol.Active==PartnerProtocols.Mode.Free?"允许探索。听广播，别丢下伙伴。":session.PresentationRule,small);
        Panel(908,24,344,176);Label(928,37,304,23,"当前目标",small);
        Label(928,70,304,80,CurrentGoal());
        Label(928,163,304,24,$"供电  A {(session.RelayAOn?"●":"○")}   B {(session.ExitUnlocked?"●":"○")}     Tab 查看计划",small);
        string incident=incidents?.PresentationAlert;
        if(!string.IsNullOrEmpty(incident)){Panel(908,212,344,66);Label(928,224,304,50,incident,small);}
        float distance=Vector3.Distance(transform.position,partner.transform.position);
        Label(32,646,390,28,$"伙伴 / {(partner.NeedsHelp?"需要救援":partner.Cooperating?"正在接应":partner.Following?"跟随中":"原地等待")} · {distance:0} 米",text);
        Label(32,678,500,24,"T 呼叫伙伴    H 路线提示    Tab 越狱笔记",small);
        if(protocol.ExposureProgress>0)Bar(485,114,310,protocol.ExposureProgress,"危险暴露",new Color(.8f,.42f,.31f));
        Label(630,346,30,30,"·",title);
        string hint=mission.OwnsInteraction?mission.InteractionHint:controls.InteractionHint;
        if(gate.Current==TransferInterlock.Phase.SendPartner&&Vector3.Distance(transform.position,gate.entryConsole.position)<3f)
            hint="对准入口面板按住 F，让队友先穿过内门";
        if(!string.IsNullOrEmpty(hint)){Panel(370,480,540,62);Label(392,493,496,44,hint);}
        float progress=mission.Plan.Progress>0?mission.Plan.Progress:session.RelayBProgress;
        if(progress>0)Bar(395,548,490,progress,"操作进度",Accent);
        string alert=session.PresentationAlert ?? equipment.PresentationMessage;
        if(!string.IsNullOrEmpty(alert)){Panel(340,590,600,52);Label(360,601,560,40,alert,small);}
        string direction=guidance.PresentationHint;
        if(!string.IsNullOrEmpty(direction))Label(420,636,500,26,direction,small);
        var vitals=equipment.PresentationVitals;
        Panel(1030,611,222,90);Bar(1046,622,190,vitals.Stamina,vitals.OutOfBreath?"精力 / 停下喘口气":"精力",Paper);
        Bar(1046,660,190,vitals.HasTorch?vitals.Charge:0,vitals.HasTorch?$"手电 / {(vitals.TorchOn?"开启":"关闭")}  L":"手电 / 尚未拾取",Accent);
        GUI.matrix=previous;
    }
    private string CurrentGoal()
    {
        if(!equipment.HasTorch)return "拿起桌上的手电筒。\n靠近后按 F 拾取，L 开灯。";
        if(!mission.Plan.Briefed)return "读起点墙上的点名板。\n对准后按住 F，确认逃生路线。";
        if(!mission.Plan.Prepared)return PrisonChinese.T(mission.CurrentGoal);
        if(!session.RelayAOn)return "找到配电 A，按 F 接通。\n按 H 查看下个路口的方向。";
        if(!session.ExitUnlocked)return "前往配电 B，按住 F 协作供电。\n保持队友在附近，直到进度完成。";
        return PrisonChinese.T(mission.CurrentGoal);
    }
    private void DrawBriefing()
    {
        Fill(new Rect(0,0,1280,720),Color.black);
        Fill(new Rect(76,64,42,3),Accent);
        Label(76,88,450,24,"双生迷城    /    地下监区 017",small);
        Label(72,146,520,180,introduction?"最后一次\n点名":"越狱笔记",new GUIStyle(title){fontSize=58,fontStyle=FontStyle.Normal});
        Label(76,326,455,150,introduction?
            "昨夜，被叫走的人没有再回来。\n\n广播说，十八分钟后，这座监区将永久封锁。\n你和同牢的伙伴必须找到地面通道。\n两道铁门之间，总得有人留下接应。":CurrentGoal());
        Label(76,531,455,56,"带上伙伴。听清广播。\n两个人都走出去，才算逃生。",small);
        Fill(new Rect(600,100,1,480),new Color(1,1,1,.14f));
        BriefStep(1,120,"先拿手电，再读点名板","起点桌上拿手电：F 拾取，L 开灯。\n读墙上的点名板，确认本局逃生方案。");
        BriefStep(2,266,"准备路线，恢复供电","按当前目标领取物品、完成校验或维修。\n先接通 A，再带队友一起接通 B。迷路按 H。");
        BriefStep(3,412,"双门接应，一起离开","入口台按住 F，让伙伴先过门解除回锁。\n随后进去会合，两人共同到达出口。");
        Fill(new Rect(76,611,1128,1),new Color(1,1,1,.18f));
        var area=new Rect(76,640,260,48);bool hover=area.Contains(Event.current.mousePosition);
        Fill(area,hover?Paper:Accent);
        GUI.Label(area,introduction?"进入监区  →":"继续逃生  →",new GUIStyle(text){alignment=TextAnchor.MiddleCenter,normal={textColor=Color.black}});
        if(GUI.Button(area,GUIContent.none,GUIStyle.none))Begin();
        Label(380,639,825,50,"WASD 移动    Shift 奔跑    F 交互 / 按住操作    L 手电\nT 伙伴跟随 / 等待    H 路线提示    Tab 打开笔记并暂停",small);
    }
    private void BriefStep(int number,float y,string heading,string description)
    {
        Label(651,y,45,40,number.ToString("00"),new GUIStyle(title){fontSize=23,normal={textColor=Accent}});
        Label(718,y,470,35,heading,new GUIStyle(title){fontSize=23,fontStyle=FontStyle.Normal});
        Label(718,y+49,470,75,description,small);
    }
    private void Label(float x,float y,float w,float h,string value,GUIStyle style=null)=>GUI.Label(new Rect(x,y,w,h),PrisonChinese.T(value),style??text);
    private static void Fill(Rect area,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=old;}
    private static void Panel(float x,float y,float w,float h){Fill(new Rect(x,y,w,h),Ink);}
    private void Bar(float x,float y,float w,float value,string name,Color tint){Label(x,y,w,22,name,small);Fill(new Rect(x,y+24,w,4),new Color(1,1,1,.16f));Fill(new Rect(x,y+24,w*Mathf.Clamp01(value),4),tint);}
}
