const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const Combat={
 visible(r,a,b){return this.clear(a,b)&&!this.map.doors.some((d,i)=>{if(r.doors[i])return false;const da=(a.x-d.x)*d.nx+(a.z-d.z)*d.nz,db=(b.x-d.x)*d.nx+(b.z-d.z)*d.nz;if(da*db>0&&Math.abs(db)>.18)return false;const t=Math.abs(da-db)<.0001?1:Math.max(0,Math.min(1,da/(da-db))),x=a.x+(b.x-a.x)*t,z=a.z+(b.z-a.z)*t;return Math.abs((x-d.x)*d.nz-(z-d.z)*d.nx)<1.9;});},
 combatAction(r,p,m,now){
  if(m.action==='pickupKnife'){
   if(p.hasKnife)return '已经携带一把刀';
   if(!this.map.knife||distance(p,this.map.knife)>2.4)return;
   const d=distance(p,this.map.knife),ratio=Math.max(0,d-.65)/Math.max(.01,d),edge={x:p.x+(this.map.knife.x-p.x)*ratio,z:p.z+(this.map.knife.z-p.z)*ratio};
   if(!this.visible(r,p,edge))return;
   p.hasKnife=true;return '拿到刀：按 2 装备，鼠标左键挥刀；按 1 换回手电。';
  }
  if(m.action==='attack'){
   if(!p.hasKnife||p.weapon!=='knife'||now<p.nextAttack)return;
   p.nextAttack=now+900;p.swing++;p.pending={at:now+300,target:m.target};
  }
  if(m.action==='revive'){
   const q=r.players[m.target];
   if(!q||q===p||q.hp>0||distance(p,q)>2||!this.visible(r,p,q))return;
   if(!p.reviveAt||p.reviveTarget!==q.slot||now-(p.reviveLast||0)>350)p.reviveAt=now;
   p.reviveTarget=q.slot;p.reviveLast=now;
   if(now-p.reviveAt>=2000){q.hp=65;q.hit++;q.torch=q.weapon==='torch';p.reviveAt=0;return '已扶起朋友；一起离开危险区域。';}
  }
 },
 tick(){
  const now=this.now();
  for(const r of this.rooms.values()){
   const dt=Math.min(.15,Math.max(0,(now-(r.tickAt||now))/1000));r.tickAt=now;
   if(this.connected(r)!==2||r.won||r.failed)continue;
   for(const p of r.players){
    if(p.reviveAt&&now-p.reviveLast>350)p.reviveAt=0;
    if(p.pending&&now>=p.pending.at){
     const n=Number.isInteger(p.pending.target)?r.actors[p.pending.target]:null;p.pending=null;
     if(p.hp<=0||p.weapon!=='knife'||!n||n.hp<=0||distance(p,n)>1.85||Math.abs(p.pitch)>50||!this.visible(r,p,n))continue;
     const dx=n.x-p.x,dz=n.z-p.z,rad=p.yaw*Math.PI/180;
     if((dx*Math.sin(rad)+dz*Math.cos(rad))/Math.max(.01,distance(p,n))<.55)continue;
     n.hp=Math.max(0,n.hp-55);n.hit++;n.until=now+550;
     if(n.hp===0){n.mode='dead';p.kills++;if(!n.armed)r.alarm++;}
     else {n.mode='hurt';n.provoked=true;}
    }
   }
   const alive=r.players.filter(p=>p.hp>0);
   if(!alive.length){r.failed=true;continue;}
   for(const n of r.actors){
    if(n.hp<=0)continue;
    const target=alive.slice().sort((a,b)=>distance(a,n)-distance(b,n))[0];
    const d=distance(n,target),seen=d<6&&this.visible(r,n,target),inside=distance(n.home,target)<7;
    if(n.mode==='hurt'&&now<n.until)continue;
    if(n.mode==='attack'){
     if(now<n.until)continue;
     const victim=r.players[n.attackTarget];
     if(victim?.hp>0&&distance(n,victim)<1.65&&this.visible(r,n,victim)){victim.hp=Math.max(0,victim.hp-30);victim.hit++;if(!victim.hp)victim.torch=false;}
     n.mode='chase';n.nextAttack=now+1300;continue;
    }
    if(n.armed&&seen&&inside){
     if(n.mode==='idle'||n.mode==='return'){n.mode='warning';n.until=now+1600;}
     if(n.mode==='warning'&&now<n.until)continue;
     n.mode='chase';n.yaw=Math.atan2(target.x-n.x,target.z-n.z)*180/Math.PI;
     if(d<1.35&&now>=n.nextAttack){n.mode='attack';n.attackTarget=target.slot;n.swing++;n.until=now+350;continue;}
     if(d>1.05)this.moveActor(r,n,target,dt*1.6);
    }else if(!n.armed&&n.provoked&&seen&&d<4){
     n.mode='flee';const away={x:n.x+(n.x-target.x),z:n.z+(n.z-target.z)};this.moveActor(r,n,away,dt*1.7);
    }else{
     n.mode=distance(n,n.home)>.15?'return':'idle';if(n.mode==='return')this.moveActor(r,n,n.home,dt*1.2);
    }
   }
   if(r.players.every(p=>p.hp<=0))r.failed=true;
  }
 },
 moveActor(r,n,target,step){
  const d=distance(n,target);if(d<.01)return;
  const p={x:n.x+(target.x-n.x)*Math.min(1,step/d),z:n.z+(target.z-n.z)*Math.min(1,step/d)};
  if(distance(p,n.home)>7||!this.visible(r,n,p))return;
  n.yaw=Math.atan2(p.x-n.x,p.z-n.z)*180/Math.PI;n.x=p.x;n.z=p.z;
 }
};
module.exports={Combat};
