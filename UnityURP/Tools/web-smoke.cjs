const {chromium}=require('playwright');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage({viewport:{width:1440,height:900}});
 const messages=[],errors=[];
 page.on('console',m=>{messages.push(m.text());if(m.text().includes('PRESENTATION'))console.log(m.text());});
 page.on('pageerror',e=>errors.push(String(e)));
 try{
  const response=await page.goto('http://127.0.0.1:8791/?v=real-rooms-20261124',{waitUntil:'domcontentloaded'});
  if(response.status()!==200)throw Error('Page HTTP '+response.status());
  const html=await page.content();if(!html.includes('real-rooms-20261124'))throw Error('Old release served');
  await page.waitForFunction(()=>!!window.prisonGame,{timeout:240000});
  await page.waitForTimeout(3500);
  await page.screenshot({path:'Logs/RealRooms-Web-Intro.png'});
  const b=await page.locator('#unity-canvas').boundingBox();
  await page.mouse.click(b.x+b.width*.16,b.y+b.height*.92);
  await page.waitForTimeout(2500);
  await page.screenshot({path:'Logs/RealRooms-Web-HUD.png'});
  if(!messages.some(m=>m.includes('PRESENTATION entered game')))throw Error('Start button did not enter game');
  await page.keyboard.press('Tab');await page.waitForTimeout(1200);
  await page.screenshot({path:'Logs/RealRooms-Web-Notebook.png'});
  await page.keyboard.press('Tab');await page.waitForTimeout(1200);
  if(errors.length)throw Error(errors.join('\n'));
  console.log('PASS headless Chromium WebGL load, start button, HUD and Tab notebook smoke test');
 }finally{
  fs.writeFileSync('Logs/RealRooms-Browser.log',messages.join('\n')+'\nPAGE ERRORS:\n'+errors.join('\n'));
  await browser.close();
 }
})().catch(e=>{console.error(e);process.exitCode=1;});
