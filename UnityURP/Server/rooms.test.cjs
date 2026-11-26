const assert=require('node:assert/strict'),fs=require('node:fs');const {Rooms}=require('./rooms.cjs');
const map=JSON.parse(fs.readFileSync(__dirname+'/map.json','utf8'));let now=1000;const service=new Rooms(map,()=>now);
const s1={},s2={},s3={};const host=service.enter({mode:'create'},s1),guest=service.enter({mode:'join',code:host.room.code},s2);const r=host.room,a=host.p,b=guest.p;
assert.equal(service.connected(r),2);assert.throws(()=>service.enter({mode:'join',code:r.code},s3),/两名/);assert.throws(()=>service.enter({mode:'join',code:'bad'},s3),/不存在/);
let seq=0;function frame(p,action='',target=-1,overrides={}){now+=100;return service.frame(r,p,{seq:++seq,x:p.x,z:p.z,yaw:0,pitch:0,torch:true,action,target,...overrides});}
assert(service.valid(map.spawn.x,map.spawn.z),'spawn must be walkable');
const old=a.x;frame(a,'',-1,{x:60,z:60});assert.equal(a.x,old,'reject teleport');frame(a,'',-1,{x:NaN});assert.equal(a.x,old,'reject non-finite');
frame(a,'',-1,{torch:false,pitch:900});assert.equal(a.torch,false);assert.equal(a.pitch,80);
frame(a,'relay',0);assert.equal(r.a,false,'cannot activate distant relay');
const gate=map.doors.findIndex(d=>!map.exitDoors.includes(map.doors.indexOf(d)));const g=map.doors[gate];a.x=g.x+g.nx*2;a.z=g.z+g.nz*2;b.x=map.spawn.x;b.z=map.spawn.z;
frame(a,'door',gate);assert.equal(r.doors[gate],true,'open door is authoritative');b.x=g.x;b.z=g.z;assert.match(frame(a,'door',gate),/退后/);assert.equal(r.doors[gate],true);
b.x=a.x;b.z=a.z;now+=500;frame(a,'door',gate);assert.equal(r.doors[gate],false);now+=500;frame(a,'door',gate);frame(b,'door',gate);assert.equal(r.doors[gate],true,'simultaneous F must not double-toggle');
a.x=map.relays[1].x;a.z=map.relays[1].z;assert.match(frame(a,'relay',1),/先接通/);
a.x=map.relays[0].x;a.z=map.relays[0].z;frame(a,'relay',0);assert.equal(r.a,true);
a.x=map.relays[1].x;a.z=map.relays[1].z;b.x=map.spawn.x;b.z=map.spawn.z;assert.match(frame(a,'relay',1),/两人/);assert.equal(r.b,false);
const beside=[[.6,0],[-.6,0],[0,.6],[0,-.6]].map(([x,z])=>({x:a.x+x,z:a.z+z})).find(p=>service.clear(p,a)&&service.valid(p.x,p.z));assert(beside,'B must have room for a real second player');b.x=beside.x;b.z=beside.z;frame(a,'relay',1);assert.equal(r.b,true);assert(map.exitDoors.every(i=>r.doors[i]));
a.x=map.exit.x;a.z=map.exit.z;frame(a);assert.equal(r.won,false,'one player is not victory');b.x=map.exit.x+.6;b.z=map.exit.z;frame(b);assert.equal(r.won,true);
service.leave(r,b,s2);assert.equal(service.connected(r),1);const resume=service.enter({mode:'join',code:r.code,token:b.token},s3);assert.equal(resume.p,b);assert.equal(r.won,true);assert.equal(service.connected(r),2);
const replacement={};service.enter({mode:'join',code:r.code,token:b.token},replacement);service.leave(r,b,s3);assert.equal(b.socket,replacement,'late close from old page must not drop replacement');assert.equal(service.connected(r),2);
const isolated=service.enter({mode:'create'},{});assert.notEqual(isolated.room.code,r.code);assert.equal(isolated.room.a,false);
// Every traversable cell centre used by the maze route must also be accepted by the server collider map.
for(let cell=0;cell<map.passages.length;cell++)assert(service.valid((cell%map.size+.5)*map.pitch,(Math.floor(cell/map.size)+.5)*map.pitch),'blocked centre '+cell);
console.log('PASS room capacity/isolation, reconnect token, movement validation, shared doors, two-person B, joint victory, all 289 cell centres');
