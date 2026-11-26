const fs=require('node:fs');
const file='Builds/Blackout-Web/index.html';
let html=fs.readFileSync(file,'utf8');
html=html.replace("if(e.code==='Tab'&&(document.activeElement===gameCanvas||document.pointerLockElement===gameCanvas))e.preventDefault();","if(e.code==='Tab'&&!e.ctrlKey&&!e.altKey&&!e.metaKey){e.preventDefault();gameCanvas.focus();}");
fs.writeFileSync(file,html);
