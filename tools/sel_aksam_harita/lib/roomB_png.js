const fs=require('fs'),zlib=require('zlib');
const crcT=new Int32Array(256).map((_,n)=>{let c=n;for(let k=0;k<8;k++)c=c&1?0xEDB88320^(c>>>1):c>>>1;return c;});
const crc=buf=>{let c=-1;for(const v of buf)c=crcT[(c^v)&255]^(c>>>8);return (c^-1)>>>0;};
function chunk(type,data){const len=Buffer.alloc(4);len.writeUInt32BE(data.length);const td=Buffer.concat([Buffer.from(type),data]);const c=Buffer.alloc(4);c.writeUInt32BE(crc(td));return Buffer.concat([len,td,c]);}
function encodePng(w,h,rgba){const raw=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++){raw[y*(w*4+1)]=0;Buffer.from(rgba.buffer,rgba.byteOffset+y*w*4,w*4).copy(raw,y*(w*4+1)+1);}
const ihdr=Buffer.alloc(13);ihdr.writeUInt32BE(w,0);ihdr.writeUInt32BE(h,4);ihdr[8]=8;ihdr[9]=6;
return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',ihdr),chunk('IDAT',zlib.deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]);}
function decodePng(buf){let p=8,w,h,bd,ct,idat=[],pal=null,trns=null;
while(p<buf.length){const len=buf.readUInt32BE(p),type=buf.toString('ascii',p+4,p+8),d=buf.subarray(p+8,p+8+len);p+=12+len;
if(type==='IHDR'){w=d.readUInt32BE(0);h=d.readUInt32BE(4);bd=d[8];ct=d[9];}else if(type==='PLTE')pal=d;else if(type==='tRNS')trns=d;else if(type==='IDAT')idat.push(d);else if(type==='IEND')break;}
if(bd!==8)throw new Error('bd '+bd);const ch={6:4,2:3,0:1,4:2,3:1}[ct];const raw=zlib.inflateSync(Buffer.concat(idat));const stride=w*ch;const out=new Uint8Array(w*h*4);let prev=new Uint8Array(stride);
for(let y=0;y<h;y++){const f=raw[y*(stride+1)];const line=new Uint8Array(raw.subarray(y*(stride+1)+1,(y+1)*(stride+1)));
for(let i=0;i<stride;i++){const a=i>=ch?line[i-ch]:0,b=prev[i],c=i>=ch?prev[i-ch]:0;let v=line[i];
if(f===1)v+=a;else if(f===2)v+=b;else if(f===3)v+=(a+b)>>1;else if(f===4){const pp=a+b-c,pa=Math.abs(pp-a),pb=Math.abs(pp-b),pc=Math.abs(pp-c);v+=(pa<=pb&&pa<=pc)?a:(pb<=pc?b:c);}line[i]=v&255;}
for(let x=0;x<w;x++){const o=(y*w+x)*4;if(ct===6){out.set(line.subarray(x*4,x*4+4),o);}else if(ct===2){out[o]=line[x*3];out[o+1]=line[x*3+1];out[o+2]=line[x*3+2];out[o+3]=255;}else if(ct===3){const i=line[x];out[o]=pal[i*3];out[o+1]=pal[i*3+1];out[o+2]=pal[i*3+2];out[o+3]=trns&&i<trns.length?trns[i]:255;}else if(ct===0){out[o]=out[o+1]=out[o+2]=line[x];out[o+3]=255;}else{out[o]=out[o+1]=out[o+2]=line[x*2];out[o+3]=line[x*2+1];}}
prev=line;}return {w,h,rgba:out};}
module.exports={encodePng,decodePng};
