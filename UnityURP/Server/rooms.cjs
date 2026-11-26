const {randomInt,randomBytes}=require('node:crypto');
const dist=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const {Combat}=require('./combat.cjs');
class Rooms {
 constructor(map,now=()=>Date.now(),choose=randomInt){this.map=map;this.now=now;this.choose=choose;this.rooms=new Map();}
 enter(message,socket){
  this.clean();let room;
  if(message.mode==='create'){
   if(this.rooms.size>=64)throw Error('房间已满，请稍后再试');
   let code;do{code=String(randomInt(100000,1000000));}while(this.rooms.has(code));
   const ordinary=this.map.actors?.length?this.choose(this.map.actors.length):0;
   room={code,players:[null,null],doors:this.map.doors.map(()=>false),doorAt:this.map.doors.map(()=>0),a:false,b:false,won:false,failed:false,alarm:0,updated:this.now(),actors:(this.map.actors||[]).map((n,id)=>({...n,id,home:{x:n.x,z:n.z},hp:100,mode:'idle',hit:0,swing:0,armed:id!==ordinary,until:0,nextAttack:0}))};this.rooms.set(code,room);
  }else room=this.rooms.get(String(message.code));
  if(!room)throw Error('房间不存在或已过期，请重新创建');
  let slot=room.players.findIndex(p=>p&&p.token===message.token);
  if(slot<0)slot=room.players.findIndex(p=>p===null);
  if(slot<0)throw Error('房间已有两名玩家');
  let p=room.players[slot];
  if(p?.socket&&p.socket!==socket)p.socket.close?.(4001,'replaced');
  if(!p){p={slot,token:randomBytes(18).toString('hex'),x:this.map.spawn.x+slot*.65,z:this.map.spawn.z,yaw:0,pitch:0,torch:true,hp:100,hasKnife:false,weapon:'torch',swing:0,hit:0,kills:0,nextAttack:0};room.players[slot]=p;}
  p.socket=socket;p.seq=-1;p.last=this.now();room.updated=this.now();
  return {room,p,hello:{type:'hello',code:room.code,slot,token:p.token}};
 }
 clean(){for(const [code,r] of this.rooms)if(!r.players.some(p=>p?.socket)&&this.now()-r.updated>120000)this.rooms.delete(code);}
 connected(r){return r.players.filter(p=>p?.socket).length;}
 valid(x,z){
  const m=this.map,cx=Math.floor(x/m.pitch),cz=Math.floor(z/m.pitch);
  if(cx<0||cz<0||cx>=m.size||cz>=m.size)return false;
  const localX=x-cx*m.pitch,localZ=z-cz*m.pitch,bits=m.passages[cz*m.size+cx],r=.18;
  if(localX>m.pitch-r&&!(bits&1)||localZ>m.pitch-r&&!(bits&2)||localX<r&&!(bits&4)||localZ<r&&!(bits&8))return false;
  return !m.obstacles.some(b=>x>b.x-b.w/2-r&&x<b.x+b.w/2+r&&z>b.z-b.d/2-r&&z<b.z+b.d/2+r);
 }
 clear(a,b){
  const n=Math.max(1,Math.ceil(dist(a,b)/.12));
  for(let i=1;i<=n;i++){const t=i/n;if(!this.valid(a.x+(b.x-a.x)*t,a.z+(b.z-a.z)*t))return false;}
  return true;
 }
 frame(r,p,m){
  if(!Number.isSafeInteger(m.seq)||m.seq<=p.seq)return;
  p.seq=m.seq;const now=this.now(),dt=Math.min(1,(now-p.last)/1000);p.last=now;r.updated=now;
  if(![m.x,m.z,m.yaw,m.pitch].every(Number.isFinite))return;
  const active=this.connected(r)===2&&!r.won&&!r.failed&&p.hp>0;
  if(active&&dist(p,m)<=3.6*dt+.25&&this.clear(p,m)){
   // Closed doors are authoritative, even if a modified client walks through them.
   const crosses=this.map.doors.some((d,i)=>!r.doors[i]&&this.crossDoor(p,m,d));
   if(!crosses){p.x=m.x;p.z=m.z;}
  }
  p.yaw=m.yaw%360;p.pitch=Math.max(-80,Math.min(80,m.pitch));p.weapon=m.weapon==='knife'&&p.hasKnife?'knife':'torch';p.torch=!!m.torch&&p.weapon==='torch'&&p.hp>0;
  if(!active)return;
  const feedback=this.combatAction(r,p,m,now);if(feedback)return feedback;
  if(m.action==='door'){
   const i=m.target,d=this.map.doors[i];
   if(!Number.isInteger(i)||!d||dist(p,d)>3.3||!this.clear(p,d))return;
   if(this.map.exitDoors.includes(i)&&!(r.a&&r.b))return '出口未供电：先接通 A，再共同启动 B';
   if(r.doors[i]&&r.players.some(q=>dist(q,d)<1.45))return '有人站在门边，请退后再关门';
   if(now-r.doorAt[i]<300)return;r.doorAt[i]=now;
   r.doors[i]=!r.doors[i];
  }
  if(m.action==='relay'){
   const i=m.target,d=this.map.relays[i];if(!d||dist(p,d)>3.2||!this.clear(p,d))return;
   if(i===0)r.a=true;
   if(i===1){if(!r.a)return '先接通配电 A';if(!r.players.every(q=>q.hp>0&&dist(q,d)<4.2&&this.clear(q,d)))return '配电 B 需要两人保持清醒并靠近后启动';r.b=true;}
   if(r.a&&r.b)for(const id of this.map.exitDoors)r.doors[id]=true;
  }
  if(r.a&&r.b&&r.players.every(q=>q.hp>0&&dist(q,this.map.exit)<2.5))r.won=true;
 }
 crossDoor(a,b,d){
  const nx=d.nx,nz=d.nz,da=(a.x-d.x)*nx+(a.z-d.z)*nz,db=(b.x-d.x)*nx+(b.z-d.z)*nz;
  return (da*db<0||Math.abs(db)<.18)&&Math.abs((b.x-d.x)*nz-(b.z-d.z)*nx)<1.9;
 }
 leave(r,p,socket){if(p.socket!==socket)return;p.socket=null;r.updated=this.now();}
 snapshot(r){return {type:'state',code:r.code,a:r.a,b:r.b,won:r.won,failed:r.failed,alarm:r.alarm,doors:r.doors,actors:r.actors.map(n=>({id:n.id,x:n.x,z:n.z,yaw:n.yaw||0,hp:n.hp,mode:n.mode,hit:n.hit,swing:n.swing,armed:n.armed&&(n.provoked||['warning','chase','attack','dead'].includes(n.mode))})),players:r.players.filter(Boolean).map(p=>({slot:p.slot,x:p.x,z:p.z,yaw:p.yaw,pitch:p.pitch,torch:p.torch,online:!!p.socket,hp:p.hp,hasKnife:p.hasKnife,weapon:p.weapon,swing:p.swing,hit:p.hit,kills:p.kills,revive:Math.min(1,p.reviveAt?(this.now()-p.reviveAt)/2000:0)}))};}
}
Object.assign(Rooms.prototype,Combat);
module.exports={Rooms};
