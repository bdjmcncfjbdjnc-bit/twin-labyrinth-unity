const assert=require('node:assert/strict'),map=require('./map.json'),{Rooms}=require('./rooms.cjs');let time=1000,seq=0,steps=0;
const game=new Rooms(map,()=>time,n=>Number(process.env.COOP_ORDINARY||0)%n),host=game.enter({mode:'create'},{}),guest=game.enter({mode:'join',code:host.room.code},{}),room=host.room,players=[host.p,guest.p];
const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
function frame(p,goal=p,action='',target=-1){time+=50;steps++;const enemy=room.actors.find(n=>n.hp>0&&distance(p,n)<1.7&&game.visible(room,p,n));let yaw=0;if(enemy){yaw=Math.atan2(enemy.x-p.x,enemy.z-p.z)*180/Math.PI;if(!action){action='attack';target=enemy.id;}}game.frame(room,p,{seq:++seq,x:goal.x,z:goal.z,yaw,pitch:0,torch:!enemy,weapon:enemy?'knife':'torch',action,target});game.tick();assert(!room.failed,'both players down');}
const cell=p=>Math.floor(p.z/4)*map.size+Math.floor(p.x/4),centre=c=>({x:(c%map.size+.5)*4,z:(Math.floor(c/map.size)+.5)*4});
function path(from,to){const q=[from],prev=new Map([[from,-1]]);for(const c of q){if(c===to)break;[1,map.size,-1,-map.size].forEach((delta,d)=>{const n=c+delta;if((map.passages[c]&(1<<d))&&!prev.has(n)){prev.set(n,c);q.push(n);}});}assert(prev.has(to));const p=[];for(let c=to;c!==from;c=prev.get(c))p.push(c);return p.reverse();}
function move(goal){let tries=0;while(players.some(p=>distance(p,goal)>.03)&&!room.won){if(++tries>300)throw Error('Movement stalled near '+JSON.stringify(players.map(p=>({x:p.x,z:p.z})))+' goal '+JSON.stringify(goal));for(const p of players){
 for(let i=0;i<map.doors.length;i++)if(!room.doors[i]&&distance(p,map.doors[i])<2.8&&game.clear(p,map.doors[i]))frame(p,p,'door',i);
 const d=distance(p,goal),f=d>0?Math.min(1,.18/d):1;frame(p,{x:p.x+(goal.x-p.x)*f,z:p.z+(goal.z-p.z)*f});
}}}
function travel(target){move(centre(cell(players[0])));for(const c of path(cell(players[0]),cell(target)))move(centre(c));}
for(const p of players){frame(p,p,"pickupKnife");assert(p.hasKnife,"knife pickup failed at real table");} travel(map.relays[0]);frame(players[0],players[0],'relay',0);assert(room.a,'A did not activate from its real approach cell');
travel(map.relays[1]);frame(players[1],players[1],'relay',1);assert(room.b,'B did not activate with both players present');
travel(map.exit);assert(room.won,'Both players did not escape');
console.log(`PASS server combat-active two-player full route: A → joint B → joint exit; ${steps} validated movement/action frames; ${((time-1000)/1000).toFixed(1)} simulated seconds`);


console.log('Health',players.map(p=>p.hp),'kills',players.map(p=>p.kills),'innocent casualties',room.alarm);

