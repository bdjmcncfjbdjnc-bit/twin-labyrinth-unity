const {chromium}=require('playwright');const fs=require('node:fs'),assert=require('node:assert/strict');
const base=process.env.COOP_BROWSER_URL||'http://127.0.0.1:8792';fs.mkdirSync('Logs',{recursive:true});
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--enable-gpu','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required','--disable-background-timer-throttling','--disable-renderer-backgrounding','--disable-backgrounding-occluded-windows']});
const logs=[],errors=[];const a=await browser.newPage({viewport:{width:1000,height:700}}),b=await browser.newPage({viewport:{width:1000,height:700}});
for(const [tag,p] of [['A',a],['B',b]]){p.on('console',m=>{logs.push(tag+' '+m.text());if(/COOP|Exception/.test(m.text()))console.log(tag,m.text());});p.on('pageerror',e=>errors.push(tag+' '+e));}
const click=async(p,x,y)=>{const c=await p.locator('#unity-canvas').boundingBox();await p.mouse.click(c.x+c.width*x/1280,c.y+c.height*y/720);};
const snapshot=p=>p.evaluate(()=>window.twinNet.latest);
try{
await a.goto(base);await a.waitForFunction(()=>!!window.prisonGame,null,{timeout:360000});await a.waitForTimeout(5000);await click(a,230,350);await a.waitForFunction(()=>window.twinNet?.request?.code?.length===6);const code=await a.evaluate(()=>window.twinNet.request.code);console.log('COMBAT ROOM',code);
await b.goto(base+'/?room='+code);await b.waitForFunction(()=>window.twinNet?.latest?.players?.filter(p=>p.online).length===2,null,{timeout:360000});await b.waitForTimeout(5000);
await a.bringToFront();await a.waitForTimeout(1000);await click(a,230,486);await a.waitForTimeout(400);await a.keyboard.press('f');await b.waitForFunction(()=>window.twinNet.latest.players.find(p=>p.slot===0).hasKnife,null,{timeout:10000});
await b.bringToFront();await b.waitForTimeout(1000);await click(b,230,486);await b.waitForTimeout(400);await b.keyboard.press('f');await a.waitForFunction(()=>window.twinNet.latest.players.find(p=>p.slot===1).hasKnife,null,{timeout:10000});
await a.bringToFront();await a.waitForTimeout(500);await a.mouse.down();await a.mouse.up();await a.waitForTimeout(350);await a.keyboard.press('2');await b.waitForFunction(()=>window.twinNet.latest.players.find(p=>p.slot===0).weapon==='knife');await a.screenshot({path:'Logs/Combat-Web-Knife.png'});
// Walk the actual first-person controller along the initial straight corridor; no position injection.
await a.keyboard.down('w');await a.waitForTimeout(2700);await a.keyboard.up('w');await a.waitForTimeout(300);
console.log('approach',JSON.stringify((await snapshot(a)).players[0]),JSON.stringify((await snapshot(a)).actors[0]));
for(let attempt=0;attempt<10;attempt++){
const s=await snapshot(a),p=s.players[0],n=s.actors[0];if(n.hp<=0)break;assert(p.hp>0,'test player down before attacking');
const d=Math.hypot(n.x-p.x,n.z-p.z);if(d>1.35){await a.keyboard.down('w');await a.waitForTimeout(Math.min(700,Math.max(120,(d-1.3)/2.1*1000)));await a.keyboard.up('w');await a.waitForTimeout(150);}
await a.mouse.down();await a.waitForTimeout(60);await a.mouse.up();await a.waitForTimeout(350);await a.screenshot({path:'Logs/Combat-Web-Strike-'+attempt+'.png'});await a.waitForTimeout(650);
}
await b.waitForFunction(()=>window.twinNet.latest.actors[0].hp===0,null,{timeout:10000});await a.waitForTimeout(1500);const dead=await snapshot(b);assert(dead.players[0].swing>0);assert.equal(dead.actors[0].mode,'dead');console.log('NPC death synchronized',JSON.stringify({actors:dead.actors,players:dead.players}));await a.screenshot({path:'Logs/Combat-Web-Corpse-A.png'});await b.screenshot({path:'Logs/Combat-Web-Corpse-B.png'});
const canvas=await a.locator('#unity-canvas').boundingBox(),cursorX=canvas.x+canvas.width*230/1280,cursorY=canvas.y+canvas.height*486/720;await a.mouse.move(cursorX,cursorY+180);await a.waitForTimeout(400);await a.screenshot({path:'Logs/Combat-Web-Corpse-Lookdown.png'});await a.mouse.move(cursorX,cursorY);await a.waitForTimeout(300);
await a.keyboard.press('1');await a.keyboard.press('l');await b.waitForFunction(()=>window.twinNet.latest.players[0].torch===false);await a.keyboard.press('l');await b.waitForFunction(()=>window.twinNet.latest.players[0].torch===true);await a.screenshot({path:'Logs/Combat-Web-Torch.png'});
await b.bringToFront();await b.waitForTimeout(300);await b.mouse.down();await b.mouse.up();await b.waitForTimeout(350);const before=(await snapshot(b)).players[1];await b.keyboard.down('w');await b.waitForTimeout(1100);await b.keyboard.up('w');await b.waitForTimeout(350);const moved=(await snapshot(a)).players[1];assert(Math.hypot(moved.x-before.x,moved.z-before.z)>.2,'second player cannot move');
await b.reload();await b.waitForFunction(()=>window.twinNet?.latest?.players?.filter(p=>p.online).length===2,null,{timeout:360000});await b.waitForTimeout(1000);const resumed=await snapshot(b);assert(resumed.players[1].hasKnife);assert.equal(resumed.actors[0].hp,0);await b.screenshot({path:'Logs/Combat-Web-Reconnected.png'});
assert.equal(errors.length,0,errors.join('\n'));assert(!logs.some(s=>/NullReferenceException|EntryPointNotFoundException|TypeError|ReferenceError/.test(s)),'runtime exception');console.log('PASS actual dual WebGL: room/invite, both knife pickups, equip, real walking, knife attacks, synchronized NPC death, torch switch, second-player movement, inventory/corpse persistence after refresh');
}catch(e){await a.screenshot({path:'Logs/Combat-Web-Failure.png'}).catch(()=>{});throw e;}finally{fs.writeFileSync(base.startsWith('https:')?'Logs/Coop-Combat-Public-Browser.log':'Logs/Coop-Combat-Browser.log',logs.join('\n')+'\nPAGE ERRORS:\n'+errors.join('\n'));await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
