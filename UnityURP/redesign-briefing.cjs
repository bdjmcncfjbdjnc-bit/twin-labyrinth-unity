const fs=require('node:fs');
const path='Assets/ArtReview/PrisonPresentation.cs';
let s=fs.readFileSync(path,'utf8');
const a=s.indexOf('        Fill(new Rect(0,0,1280,720)'),b=s.indexOf('    private void Label',a);
if(a<0||b<0)throw Error('Expected briefing boundaries');
s=s.slice(0,a)+String.raw`        Fill(new Rect(0,0,1280,720),Color.black);
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
`+s.slice(b);
s=s.replace('Fill(new Rect(x,y,2,h),new Color(.37f,.48f,.43f,.8f));','');
s=s.replace('new Color(.23f,.27f,.25f)','new Color(1,1,1,.16f)');
fs.writeFileSync(path,s);
