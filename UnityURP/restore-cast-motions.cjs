const fs = require('node:fs'), path = require('node:path');
function read(file) {const b=fs.readFileSync(file),n=b.readUInt32LE(12);return {json:JSON.parse(b.subarray(20,20+n)),bin:b.subarray(28+n)};}
for(const name of ['marcus_formal','hannah_casual','kenji_formal']) {
 const from=read(path.join(__dirname,'../ReferenceCharacters/Rocketbox',name+'.glb'));
 const file=path.join(__dirname,'Assets/ArtReview/Source/MazeCast',name+'.glb');const to=read(file);
 to.json.animations=from.json.animations;
 let json=Buffer.from(JSON.stringify(to.json));json=Buffer.concat([json,Buffer.alloc((4-json.length%4)%4,32)]);
 const head=Buffer.alloc(20); head.write('glTF');head.writeUInt32LE(2,4);head.writeUInt32LE(28+json.length+to.bin.length,8);head.writeUInt32LE(json.length,12);head.write('JSON',16);
 const bin=Buffer.alloc(8);bin.writeUInt32LE(to.bin.length);bin.write('BIN\0',4);
 fs.writeFileSync(file,Buffer.concat([head,json,bin,to.bin]));console.log(name, to.json.animations.length+' original licensed motions restored');
}
