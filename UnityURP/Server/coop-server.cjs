const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const {WebSocketServer,WebSocket}=require('./vendor/package');const {Rooms}=require('./rooms.cjs');
const root=path.resolve(__dirname,'../Builds/'+(process.env.TWIN_COOP_BUILD||'Coop-Web')),map=JSON.parse(fs.readFileSync(path.join(__dirname,'map.json'),'utf8'));
const rooms=new Rooms(map),port=Number(process.env.TWIN_COOP_PORT||8792);
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript','.css':'text/css','.wasm':'application/wasm','.png':'image/png','.txt':'text/plain; charset=utf-8'};
const server=http.createServer((req,res)=>{
 if(!['GET','HEAD'].includes(req.method)){res.writeHead(405).end();return;}
 let resource;try{resource=decodeURIComponent(new URL(req.url,'http://local').pathname);}catch{res.writeHead(400).end();return;}
 if(resource==='/health'){res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'}).end(JSON.stringify({ok:true,mode:'two-player',version:'coop-combat-20261124-r1'}));return;}
 if(resource==='/')resource='/index.html';
 if(!/^\/(?:index\.html|Rocketbox-Notices\.txt|(?:Build|TemplateData)\/[a-zA-Z0-9_.-]+)$/.test(resource)){res.writeHead(404).end();return;}
 const target=path.resolve(root,'.'+resource);if(!target.startsWith(root+path.sep)){res.writeHead(404).end();return;}
 fs.stat(target,(err,stat)=>{if(err||!stat.isFile()){res.writeHead(404).end();return;}const zip=target.endsWith('.gz');
  res.setHeader('Content-Type',types[path.extname(zip?target.slice(0,-3):target)]||'application/octet-stream');res.setHeader('Content-Length',stat.size);res.setHeader('Cache-Control','no-store');if(zip)res.setHeader('Content-Encoding','gzip');
  if(req.method==='HEAD'){res.end();return;}const stream=fs.createReadStream(target);stream.on('error',()=>res.destroy());stream.pipe(res);
 });
});
const wss=new WebSocketServer({server,path:'/coop',maxPayload:4096});
const send=(s,m)=>{if(s.readyState===WebSocket.OPEN)s.send(JSON.stringify(m));};
wss.on('connection',(socket,request)=>{
 try{if(request.headers.origin&&new URL(request.headers.origin).host!==request.headers.host){socket.close(1008,'origin');return;}}catch{socket.close(1008,'origin');return;}
 let session=null,count=0,period=Date.now();const timeout=setTimeout(()=>{if(!session)socket.close();},10000);
 socket.on('message',raw=>{try{if(Date.now()-period>1000){period=Date.now();count=0;}if(++count>50){socket.close(1008,'rate');return;}
  const m=JSON.parse(raw.toString());if(!session){session=rooms.enter(m,socket);clearTimeout(timeout);send(socket,session.hello);}
  else {const notice=rooms.frame(session.room,session.p,m);if(notice)send(socket,{type:'notice',message:notice});}
 }catch(e){send(socket,{type:'error',message:session?'无法处理联机请求':e.message});if(!session)socket.close();}});
 socket.on('error',()=>{});socket.on('close',()=>{clearTimeout(timeout);if(session)rooms.leave(session.room,session.p,socket);});
 socket.isAlive=true;socket.on('pong',()=>socket.isAlive=true);
});
setInterval(()=>{rooms.clean();rooms.tick();for(const r of rooms.rooms.values()){const state=rooms.snapshot(r);for(const p of r.players)if(p?.socket)send(p.socket,state);}},80).unref();
setInterval(()=>{for(const s of wss.clients){if(!s.isAlive){s.terminate();continue;}s.isAlive=false;s.ping();}},10000).unref();
server.listen(port,'127.0.0.1',()=>console.log('Twin Labyrinth co-op http://127.0.0.1:'+port));
