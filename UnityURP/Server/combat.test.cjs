const assert=require('node:assert/strict');const {Rooms}=require('./rooms.cjs');
let now=1000;
const map={size:4,pitch:4,passages:Array(16).fill(15),spawn:{x:2,z:2},exit:{x:14,z:14},doors:[],exitDoors:[],relays:[{x:10,z:10},{x:14,z:10}],obstacles:[],knife:{x:2,z:2.5},actors:[{x:2,z:3.5},{x:10,z:10},{x:14,z:6}]};
const g=new Rooms(map,()=>now,()=>0),a=g.enter({mode:'create'},{}),b=g.enter({mode:'join',code:a.room.code},{}),r=a.room,p=a.p;let seq=0;
function action(type,target=-1,weapon='knife'){now+=100;return g.frame(r,p,{seq:++seq,x:p.x,z:p.z,yaw:p.yaw,pitch:0,torch:true,weapon,action:type,target});}
function tick(ms=350){now+=ms;g.tick();}
action('attack',0);tick();assert.equal(r.actors[0].hp,100,'unowned knife can damage');
assert.match(action('pickupKnife'),/拿到刀/);assert(p.hasKnife);
action('attack',0);tick();assert.equal(r.actors[0].hp,45);const swings=p.swing;
action('attack',0);tick(100);assert.equal(p.swing,swings,'cooldown bypass');
tick(1000);p.yaw=180;action('attack',0);tick();assert.equal(r.actors[0].hp,45,'backwards hit accepted');
tick(1000);p.yaw=0;p.x=6;action('attack',0);tick();assert.equal(r.actors[0].hp,45,'remote hit accepted');
tick(1000);p.x=r.actors[0].x;p.z=r.actors[0].z-1.2;action('attack',0);tick();assert.equal(r.actors[0].hp,0);assert.equal(r.alarm,1);assert.equal(p.kills,1);
tick(1000);action('attack',0);tick();assert.equal(p.kills,1,'corpse repeatedly scores');
assert.equal(g.snapshot(r).actors[0].mode,'dead');assert.equal(g.snapshot(r).players[0].weapon,'knife');
b.p.hp=0;b.p.x=p.x+.6;b.p.z=p.z;
for(let i=0;i<23;i++)action('revive',1);assert.equal(b.p.hp,65,'held rescue failed');
b.p.hp=0;action('revive',1);tick(1000);action('revive',1);assert.equal(b.p.hp,0,'interrupted rescue completed');
// A closed door blocks visibility, hits, and enemy damage.
map.doors.push({x:2,z:3,nx:0,nz:1});r.doors.push(false);r.doorAt.push(0);r.actors[0].hp=100;r.actors[0].mode='idle';r.actors[0].provoked=false;r.actors[0].x=2;r.actors[0].z=3.5;p.x=2;p.z=2;tick(1000);action('attack',0);tick();assert.equal(r.actors[0].hp,100);
assert.equal(g.visible(r,p,r.actors[0]),false);r.doors[0]=true;assert.equal(g.visible(r,p,r.actors[0]),true);
// An armed inmate warns before attacking; ordinary inmates do not initiate attacks.
b.p.hp=100;p.x=10;p.z=8.9;p.hp=100;b.p.x=14;b.p.z=14;
tick(80);assert.equal(r.actors[1].mode,'warning');assert.equal(p.hp,100);tick(1700);assert.equal(r.actors[1].mode,'attack');assert.equal(p.hp,100);tick(550);assert.equal(p.hp,70);
p.x=2;p.z=2;tick(500);assert.notEqual(r.actors[1].mode,'attack','pursuit never disengages');
p.hp=0;b.p.hp=0;tick();assert.equal(r.failed,true);
console.log('PASS combat: ownership, pickup, range, direction, cooldown, hit/death, innocent penalty, corpse idempotence, shared snapshots, interrupted/complete rescue, closed-door occlusion, warning/windup, damage, disengage, joint defeat');
