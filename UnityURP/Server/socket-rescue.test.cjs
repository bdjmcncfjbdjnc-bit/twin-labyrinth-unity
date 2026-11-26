const {WebSocket}=require('./vendor/package'),assert=require('node:assert/strict'),fs=require('node:fs');const endpoint='ws://127.0.0.1:8793/coop',sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function join(req){return new Promise((resolve,reject)=>{const c={s:new WebSocket(endpoint,{origin:'http://127.0.0.1:8793'}),seq:0};c.s.on('open',()=>c.s.send(JSON.stringify(req)));c.s.on('message',b=>{const m=JSON.parse(b);if(m.type==='hello'){c.hello=m;resolve(c);}if(m.type==='state')c.state=m;});c.s.on('error',reject);});}
function own(c){return c.state.players.find(p=>p.slot===c.hello.slot);}
function send(c,goal,action='',target=-1){c.s.send(JSON.stringify({seq:++c.seq,x:goal.x,z:goal.z,yaw:0,pitch:0,torch:true,weapon:'torch',action,target}));}
(async()=>{let a,b;try{
for(let attempt=0;attempt<8;attempt++){
 a=await join({mode:'create'});b=await join({mode:'join',code:a.hello.code});await sleep(300);
 for(let i=0;i<15;i++){const p=own(a);send(a,{x:2,z:p.z+.18});await sleep(110);}await sleep(250);
 if(a.state.actors[0].armed)break;a.s.close();b.s.close();a=null;b=null;
}assert(a,'could not locate a hostile first encounter');
for(let i=0;i<45;i++){for(const [c,goal] of [[a,{x:2,z:8.1}],[b,{x:2.05,z:6.8}]]){const p=own(c);let next={x:p.x,z:p.z};if(Math.abs(p.x-goal.x)>.03)next.x+=Math.sign(goal.x-p.x)*Math.min(.16,Math.abs(goal.x-p.x));else next.z+=Math.sign(goal.z-p.z)*Math.min(.16,Math.abs(goal.z-p.z));send(c,next);}await sleep(110);}
for(let i=0;i<150&&own(a).hp>0;i++){send(a,own(a));send(b,own(b));await sleep(100);}assert.equal(own(a).hp,0,'NPC did not down the close player');assert(own(b).hp>0,'partner should remain available for rescue');
for(let i=0;i<40&&own(a).hp===0;i++){send(b,own(b),'revive',0);await sleep(100);}assert.equal(own(a).hp,65,'network rescue did not restore player');
const rescued={a:own(a).hp,b:own(b).hp};
for(let i=0;i<300&&!a.state.failed;i++){send(a,own(a));send(b,own(b));await sleep(100);}assert(a.state.failed&&a.state.players.every(p=>p.hp===0),'joint defeat did not trigger');
const result='PASS actual WebSocket NPC warning/damage → player down → held partner rescue (health '+JSON.stringify(rescued)+') → both down → shared defeat';console.log(result);fs.writeFileSync('Logs/Coop-Combat-Socket-Rescue.log',result);
}finally{a?.s.close();b?.s.close();}})().catch(e=>{console.error(e);fs.writeFileSync('Logs/Coop-Combat-Socket-Rescue.log','FAIL '+e.stack);process.exitCode=1;});
