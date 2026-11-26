mergeInto(LibraryManager.library, {
 TL_Connect: function(ptr){
  var request=JSON.parse(UTF8ToString(ptr));
  if(window.twinNet&&window.twinNet.socket){window.twinNet.closed=true;window.twinNet.socket.close();}
  var net=window.twinNet={request:request,closed:false,attempt:0,latest:null};
  var deliver=function(value){SendMessage('Online session','Receive',JSON.stringify(value));};
  var connect=function(){
   if(net.closed)return;
   var socket=net.socket=new WebSocket((location.protocol==='https:'?'wss://':'ws://')+location.host+'/coop');
   socket.onopen=function(){var token=sessionStorage.getItem('twin-room-'+net.request.code);socket.send(JSON.stringify(Object.assign({},net.request,{token:token||''})));};
   socket.onmessage=function(e){var value;try{value=JSON.parse(e.data);}catch{return;}
    if(value.type==='hello'){net.attempt=0;net.request={mode:'join',code:value.code};sessionStorage.setItem('twin-room-'+value.code,value.token);history.replaceState(null,'','?room='+value.code);}
    if(value.type==='state')net.latest=value;
    if(value.type==='error')net.closed=true;
    deliver(value);
   };
   socket.onclose=function(e){if(net.closed)return;if(e.code===4001){net.closed=true;deliver({type:'error',message:'这个玩家已在另一页面连接，请勿重复打开自己的邀请链接'});return;}deliver({type:'disconnected',message:'连接中断，正在重新连接…'});if(++net.attempt<=12)setTimeout(connect,1500);else deliver({type:'error',message:'重连失败，请重新进入房间'});};
   socket.onerror=function(){};
  };connect();
 },
 TL_Send:function(ptr){var net=window.twinNet;if(net&&net.socket&&net.socket.readyState===1)net.socket.send(UTF8ToString(ptr));},
 TL_Close:function(){if(window.twinNet){window.twinNet.closed=true;if(window.twinNet.socket)window.twinNet.socket.close();}},
 TL_InitialRoom:function(){var room=new URLSearchParams(location.search).get('room');if(room&&/^\d{6}$/.test(room))SendMessage('Online session','JoinRoom',room);}
});
