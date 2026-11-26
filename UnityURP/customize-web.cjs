const fs = require('node:fs');
const path = require('node:path');
const file = path.join(__dirname, 'Builds/Blackout-Web/index.html');
let html = fs.readFileSync(file, 'utf8');
if (html.includes('id="last-call-intro"')) throw new Error('Apply this script to a fresh Unity build.');
html = html.replace('<html lang="en-us">', '<html lang="zh-CN">')
  .replace(/<title>.*?<\/title>/, '<title>双生迷城 · 最后一次点名</title>');
html = html.replace('</head>', `<style>
body{background:#080908;color:#d8d5c9;font-family:system-ui,sans-serif}#unity-container{max-width:100%}
#unity-footer{color:#b0bab5}#last-call-intro{position:fixed;inset:0;z-index:30;background:#080908f5;display:grid;place-items:center}
#last-call-intro section{max-width:660px;padding:38px 0;border-top:2px solid #b89e6e;margin:28px}
#last-call-intro h1{font-size:56px;font-weight:400;letter-spacing:6px;margin:32px 0}#last-call-intro p{line-height:1.9;color:#babbb3}
#last-call-intro small{color:#8e9e93}#last-call-intro button{padding:12px 28px;background:#b89e6e;color:#0b0d0b;border:0;font-size:16px;cursor:pointer}
#last-call-intro button:disabled{opacity:.4;cursor:wait}
</style></head>`);
html = html.replace('<body>', `<body><div id="last-call-intro"><section><small>双生迷城 · 监区扩建版 2026.11.24</small>
<h1>最后一次点名</h1><p>昨夜，被叫走的人没有再回来。<br>十八分钟后，这座监区将永久封锁。<br>拿起手电，带上伙伴，找到通往地面的路。</p>
<p><small>第一人称探索 · 监狱协议 · 双门接应</small></p>
<button id="last-call-start" disabled>正在加载监区…</button><p><small>电脑键鼠试玩。首次载入需要下载模型和声音，请稍候。<br>载入后将显示中文前情与操作说明；阅读时游戏暂停。</small></p></section></div>`);
html = html.replace('document.querySelector("#unity-loading-bar").style.display = "none";', `document.querySelector("#unity-loading-bar").style.display = "none";
document.querySelector('#last-call-intro').remove(); canvas.focus(); window.prisonGame=unityInstance;`);
html = html.replace(/(Blackout-Web\.(?:data\.gz|framework\.js\.gz|wasm\.gz|loader\.js))/g, '$1?v=real-rooms-20261124-r2');
html = html.replaceAll('Twin Labyrinth - Partner Protocol', '双生迷城 · 最后一次点名');
html = html.replace('canvas.style.width = "960px";', 'canvas.style.width = Math.min(1280,innerWidth-24,(innerHeight-100)*(16/9))+"px";')
  .replace('canvas.style.height = "600px";', 'canvas.style.height = parseFloat(canvas.style.width)/(16/9)+"px";');
html=html.replace('</head>','<style>#unity-logo-title-footer{display:none}#unity-canvas{outline:none}#unity-fullscreen-button{filter:grayscale(1)}</style></head>');
html=html.replace('</body>',`<script>const gameCanvas=document.querySelector('#unity-canvas');gameCanvas.tabIndex=0;gameCanvas.addEventListener('pointerdown',()=>gameCanvas.focus());window.addEventListener('keydown',e=>{if(e.code==='Tab'&&!e.ctrlKey&&!e.altKey&&!e.metaKey){e.preventDefault();if(!e.repeat&&window.prisonGame)window.prisonGame.SendMessage('First-person camera rig','ToggleNotebook');gameCanvas.focus();}},true);</script></body>`);
html=html.replace('</body>','<footer style="position:fixed;bottom:6px;right:14px;font-size:12px"><a href="Rocketbox-Notices.txt" target="_blank" style="color:#9ba9a1">素材许可</a></footer></body>');
html=html.replace('document.querySelector("#unity-progress-bar-full").style.width = 100 * progress + "%";', 'document.querySelector("#unity-progress-bar-full").style.width = 100 * progress + "%"; document.querySelector("#last-call-start").textContent = "正在加载监区 · " + Math.round(progress * 100) + "%";');
fs.writeFileSync(file, html);
const notice=path.join(__dirname,'Builds/Blackout-Web/Rocketbox-Notices.txt');
fs.appendFileSync(notice,'\n\nValve SteamVR hand pose data (retargeted):\n'+fs.readFileSync(path.join(__dirname,'Assets/ArtReview/Source/HandPoses/LICENSE.txt'),'utf8')+
 '\n\nNoto Sans CJK Chinese font:\n'+fs.readFileSync(path.join(__dirname,'Assets/ArtReview/Resources/NotoSansSC-LICENSE.txt'),'utf8'));
fs.appendFileSync(notice,'\n\n'+fs.readFileSync(path.join(__dirname,'Assets/ArtReview/Source/Environment/ROOM-ASSETS.txt'),'utf8'));
console.log('Prepared Last Roll Call Chinese introduction and asset cache version.');
