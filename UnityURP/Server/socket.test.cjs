const assert=require('node:assert/strict');const {WebSocket}=require('./vendor/package');
const endpoint=process.env.COOP_WS_URL||'ws://127.0.0.1:8792/coop';
function connect(message){return new Promise((resolve,reject)=>{const s=new WebSocket(endpoint,{origin:new URL(endpoint).origin.replace(/^ws/,'http')});const timer=setTimeout(()=>{s.close();reject(Error('Connection timed out'));},15000);s.events=[];s.on('open',()=>s.send(JSON.stringify(message)));s.on('message',raw=>{const m=JSON.parse(raw);s.events.push(m);if(m.type==='hello'){clearTimeout(timer);resolve({s,hello:m});}if(m.type==='error'){clearTimeout(timer);s.close();reject(Error(m.message));}});s.on('error',e=>{clearTimeout(timer);reject(e);});});}
const delay=ms=>new Promise(r=>setTimeout(r,ms));
async function until(s,predicate){for(let i=0;i<75;i++){const state=s.events.findLast(m=>m.type==='state');if(state&&predicate(state))return state;await delay(100);}throw Error('State replication timed out');}
(async()=>{let a,b,c;try{
 a=await connect({mode:'create'});b=await connect({mode:'join',code:a.hello.code});await until(b.s,s=>s.players.filter(p=>p.online).length===2);
 assert.equal(b.hello.slot,1);assert.equal(b.s.events.findLast(m=>m.type==='state').players.filter(p=>p.online).length,2);
 a.s.send(JSON.stringify({seq:1,x:2,z:2,yaw:75,pitch:20,torch:false,action:'',target:-1}));await until(b.s,s=>s.players.find(p=>p.slot===0)?.torch===false);
 const observed=b.s.events.findLast(m=>m.type==='state').players.find(p=>p.slot===0);assert.equal(observed.yaw,75);assert.equal(observed.torch,false);
 b.s.close();await until(a.s,s=>s.players.filter(p=>p.online).length===1);assert.equal(a.s.events.findLast(m=>m.type==='state').players.filter(p=>p.online).length,1);
 c=await connect({mode:'join',code:a.hello.code,token:b.hello.token});assert.equal(c.hello.slot,1);
 console.log('PASS two real WebSocket clients: room join, remote rotation/torch, peer disconnect and authenticated resume');
}finally{a?.s.close();b?.s.close();c?.s.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
