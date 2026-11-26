const {chromium}=require('playwright');const fs=require('node:fs'),assert=require('node:assert/strict');
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--enable-gpu','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required','--disable-background-timer-throttling','--disable-renderer-backgrounding','--disable-backgrounding-occluded-windows']});
 const logs=[],errors=[];const a=await browser.newPage({viewport:{width:1000,height:700}}),b=await browser.newPage({viewport:{width:1000,height:700}});
 for(const [tag,page] of [['A',a],['B',b]]){page.on('console',m=>{logs.push(tag+' '+m.text());if(/COOP|Exception|error/i.test(m.text()))console.log(tag,m.text());});page.on('pageerror',e=>errors.push(tag+' '+e));}
 const click=async(page,x,y)=>{const c=await page.locator('#unity-canvas').boundingBox();await page.mouse.click(c.x+c.width*x/1280,c.y+c.height*y/720);};
 try{
  await a.goto('http://127.0.0.1:8792/',{waitUntil:'domcontentloaded'});await a.waitForFunction(()=>!!window.prisonGame,{timeout:360000});await a.waitForTimeout(5000);
  await a.screenshot({path:'Logs/Coop-Lobby.png'});await click(a,230,350);
  await a.waitForFunction(()=>window.twinNet?.request?.code?.length===6,{timeout:20000});const code=await a.evaluate(()=>window.twinNet.request.code);console.log('Room created',code);
  await b.goto('http://127.0.0.1:8792/?room='+code,{waitUntil:'domcontentloaded'});await b.waitForFunction(()=>window.twinNet?.latest?.players?.filter(p=>p.online).length===2,{timeout:360000});
  await a.waitForFunction(()=>window.twinNet?.latest?.players?.filter(p=>p.online).length===2);await a.waitForTimeout(5000);
  await a.bringToFront();await a.waitForTimeout(1200);await click(a,230,486);await a.waitForTimeout(1200);await a.screenshot({path:'Logs/Coop-A-Entered.png'});await b.bringToFront();await b.waitForTimeout(1200);await click(b,230,486);await b.waitForTimeout(1200);await b.screenshot({path:'Logs/Coop-B-Entered.png'});
  const start=await b.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===0));
  await a.bringToFront();await a.waitForTimeout(1000);await click(a,640,360);await a.waitForTimeout(300);const yawA=await b.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===0).yaw);const radA=yawA*Math.PI/180;const keyA=[['w',Math.cos(radA)],['s',-Math.cos(radA)],['a',Math.sin(radA)],['d',-Math.sin(radA)]].sort((x,y)=>y[1]-x[1])[0][0];await a.keyboard.down(keyA);await a.waitForTimeout(1600);await a.keyboard.up(keyA);await a.waitForTimeout(500);
  const moved=await b.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===0));console.log('movement',JSON.stringify({start,moved}));await a.screenshot({path:'Logs/Coop-A-Moved.png'});assert(Math.hypot(moved.x-start.x,moved.z-start.z)>.15,'real browser movement was not replicated');
  await a.keyboard.press('l');await b.waitForFunction(()=>window.twinNet.latest.players.find(p=>p.slot===0).torch===false,{timeout:10000});
  const bStart=await a.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===1));
  await b.bringToFront();await b.waitForTimeout(1000);await click(b,640,360);await b.waitForTimeout(300);const yawB=await a.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===1).yaw);const radB=yawB*Math.PI/180;const keyB=[['w',Math.cos(radB)],['s',-Math.cos(radB)],['a',Math.sin(radB)],['d',-Math.sin(radB)]].sort((x,y)=>y[1]-x[1])[0][0];await b.keyboard.down(keyB);await b.waitForTimeout(1400);await b.keyboard.up(keyB);await b.waitForTimeout(500);
  const bMoved=await a.evaluate(()=>window.twinNet.latest.players.find(p=>p.slot===1));console.log('movement B',JSON.stringify({start:bStart,moved:bMoved}));assert(Math.hypot(bMoved.x-bStart.x,bMoved.z-bStart.z)>.15,'player B movement was not replicated');
  await a.screenshot({path:'Logs/Coop-Player-A.png'});await b.screenshot({path:'Logs/Coop-Player-B.png'});
  await b.reload({waitUntil:'domcontentloaded'});await b.waitForFunction(()=>window.twinNet?.latest?.players?.filter(p=>p.online).length===2,{timeout:360000});
  const resumed=await b.evaluate(()=>window.twinNet.latest.players);assert.equal(resumed.length,2);await b.screenshot({path:'Logs/Coop-Reconnected.png'});
  if(errors.length)throw Error(errors.join('\n'));
  assert(!logs.some(x=>/NullReferenceException|EntryPointNotFoundException|TypeError|ReferenceError/.test(x)),'Unity runtime exception');
  console.log('PASS two actual WebGL clients: create room, invite URL, movement replication, flashlight replication, refresh/reconnect');
 }finally{fs.writeFileSync('Logs/Coop-Browser.log',logs.join('\n')+'\nPAGE ERRORS:\n'+errors.join('\n'));await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
