// Isometric SDF pixel renderer. World: u (grid X, 32 units/cell), v (grid Y), z (pixels up).
// screen dx = u - v, dy_up = (u+v)/2 + z. Ray for pixel: P(t) = (t+sx/2, t-sx/2, sy-t).
const hex = h => { h = h.replace('#', ''); return [parseInt(h.slice(0,2),16), parseInt(h.slice(2,4),16), parseInt(h.slice(4,6),16), h.length>6?parseInt(h.slice(6,8),16):255]; };
const V = (u,v,z)=>({u,v,z});
// SDF primitives (p = {u,v,z})
const len3=(a,b,c)=>Math.sqrt(a*a+b*b+c*c);
function sdBox(p,c,h){ // c centre, h half extents
  const qx=Math.abs(p.u-c.u)-h.u, qy=Math.abs(p.v-c.v)-h.v, qz=Math.abs(p.z-c.z)-h.z;
  return len3(Math.max(qx,0),Math.max(qy,0),Math.max(qz,0))+Math.min(Math.max(qx,qy,qz),0);
}
function box(u0,u1,v0,v1,z0,z1){const c=V((u0+u1)/2,(v0+v1)/2,(z0+z1)/2),h=V((u1-u0)/2,(v1-v0)/2,(z1-z0)/2);return p=>sdBox(p,c,h);}
function rbox(u0,u1,v0,v1,z0,z1,r){const c=V((u0+u1)/2,(v0+v1)/2,(z0+z1)/2),h=V((u1-u0)/2-r,(v1-v0)/2-r,(z1-z0)/2-r);return p=>sdBox(p,c,h)-r;}
function sphere(cu,cv,cz,r){return p=>len3(p.u-cu,p.v-cv,p.z-cz)-r;}
function ellipsoid(cu,cv,cz,ru,rv,rz){return p=>{const a=len3((p.u-cu)/ru,(p.v-cv)/rv,(p.z-cz)/rz);const b=len3((p.u-cu)/ru/ru,(p.v-cv)/rv/rv,(p.z-cz)/rz/rz);return a*(a-1)/Math.max(b,1e-6);};}
function cylZ(cu,cv,r,z0,z1){return p=>{const d=Math.hypot(p.u-cu,p.v-cv)-r, dz=Math.abs(p.z-(z0+z1)/2)-(z1-z0)/2;return Math.min(Math.max(d,dz),0)+Math.hypot(Math.max(d,0),Math.max(dz,0));};}
function coneZ(cu,cv,r0,r1,z0,z1){ // frustum radius r0 at z0, r1 at z1 (approx sdf)
  return p=>{const k=(p.z-z0)/(z1-z0);const r=r0+(r1-r0)*Math.min(Math.max(k,0),1);const d=(Math.hypot(p.u-cu,p.v-cv)-r)/Math.sqrt(1+((r1-r0)/(z1-z0))**2);const dz=Math.abs(p.z-(z0+z1)/2)-(z1-z0)/2;return Math.min(Math.max(d,dz),0)+Math.hypot(Math.max(d,0),Math.max(dz,0));};}
function capsule(a,b,r){return p=>{const pa=V(p.u-a.u,p.v-a.v,p.z-a.z),ba=V(b.u-a.u,b.v-a.v,b.z-a.z);const h=Math.min(Math.max((pa.u*ba.u+pa.v*ba.v+pa.z*ba.z)/(ba.u*ba.u+ba.v*ba.v+ba.z*ba.z),0),1);return len3(pa.u-ba.u*h,pa.v-ba.v*h,pa.z-ba.z*h)-r;};}
const U=(...fs)=>p=>Math.min(...fs.map(f=>f(p)));
const SUB=(a,b)=>p=>Math.max(a(p),-b(p));
const INT=(a,b)=>p=>Math.max(a(p),b(p));
function smin(a,b,k){const h=Math.max(k-Math.abs(a-b),0)/k;return Math.min(a,b)-h*h*k*0.25;}
const SU=(k,...fs)=>p=>fs.map(f=>f(p)).reduce((a,b)=>smin(a,b,k));
const L=(()=>{const l=[-0.55,-0.25,1];const n=Math.hypot(...l);return l.map(x=>x/n);})();
function faceOf(n){ if(n.z>0.7) return 'top'; if(n.z<-0.7) return 'bottom'; return (-n.u >= -n.v) ? 'left':'right'; }
function lambert(n){return n.u*L[0]+n.v*L[1]+n.z*L[2];}
// ramp pick: ramp dark->light, by lambert
function shade(ramp,n,bias=0){const d=lambert(n)+bias;const k=Math.max(0,Math.min(ramp.length-1,Math.floor((d+0.35)/1.25*ramp.length)));return ramp[k];}

// Render a scene: objs = [{sdf, mat(p,n,ctx)->hex, inner?:hex|false, id}]
function render(objs, W, H, ox, oy, opt={}){
  const zmax=opt.zmax||140;
  const hits=new Array(W*H).fill(null);
  objs.forEach((o,i)=>o.id=i);
  const scene=p=>{let best=1e9,bo=null;for(const o of objs){const d=o.sdf(p);if(d<best){best=d;bo=o;}}return [best,bo];};
  for(let py=0;py<H;py++)for(let px=0;px<W;px++){
    const sx=px+0.5-ox, sy=oy-(py+0.5);
    let t=sy-zmax; const tEnd=sy+2; let hit=null;
    for(let it=0;it<600&&t<tEnd;it++){
      const p=V(t+sx/2,t-sx/2,sy-t); const [d,o]=scene(p);
      if(d<0.002){hit={p,o,t};break;}
      t+=Math.max(d/1.7320508,0.004);
    }
    if(hit && hit.p.z>=-0.01){
      const e=0.002,f=hit.o.sdf,p=hit.p;
      const n=V(f(V(p.u+e,p.v,p.z))-f(V(p.u-e,p.v,p.z)),f(V(p.u,p.v+e,p.z))-f(V(p.u,p.v-e,p.z)),f(V(p.u,p.v,p.z+e))-f(V(p.u,p.v,p.z-e)));
      const nl=len3(n.u,n.v,n.z)||1;n.u/=nl;n.v/=nl;n.z/=nl;
      hit.n=n;hit.face=faceOf(n);hits[py*W+px]=hit;
    }
  }
  const out=new Uint8Array(W*H*4);
  const at=(x,y)=>(x<0||y<0||x>=W||y>=H)?null:hits[y*W+x];
  for(let py=0;py<H;py++)for(let px=0;px<W;px++){
    const h=hits[py*W+px]; if(!h) continue;
    const below=at(px,py+1), above=at(px,py-1);
    const ctx={face:h.face,px,py,
      rim: h.face==='top' && below && below.o===h.o && below.face!=='top',
      edgeTop: (h.face!=='top') && (!above || above.o!==h.o || above.face==='top') ,
    };
    let col=h.o.mat(h.p,h.n,ctx);
    // inner contour: neighbour belongs to other object and is much closer
    if(h.o.inner!==false){
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){const nb=at(px+dx,py+dy);
        if(nb && nb.o!==h.o && nb.t < h.t-(opt.innerThr||3) && nb.o.cast!==false){col=h.o.inner||opt.innerColor||darken(col);break;}}
    }
    if(!col) continue;
    const c=hex(col); out.set(c,(py*W+px)*4);
  }
  // overlays (screen-space pixel art) before outline
  if(opt.overlay) opt.overlay((x,y,col)=>{if(x<0||y<0||x>=W||y>=H)return;const c=hex(col);out.set(c,(y*W+x)*4);},{ox,oy,hits,W,H});
  // outer outline
  if(opt.outline!==false){
    const oc=hex(opt.outlineColor||'#14141F');const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ if(out[(y*W+x)*4+3]) continue;
      let n=false; for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){const X=x+dx,Y=y+dy;if(X>=0&&Y>=0&&X<W&&Y<H&&out[(Y*W+X)*4+3]===255){n=true;break;}}
      if(n) add.push((y*W+x)*4);}
    for(const o of add) out.set(oc,o);
  }
  // floor shadow under everything (alpha), shape fn(u,v)->alpha 0..255
  if(opt.shadow){
    for(let py=0;py<H;py++)for(let px=0;px<W;px++){const o=(py*W+px)*4; if(out[o+3]) continue;
      const sx=px+0.5-ox, sy=oy-(py+0.5); const t=sy; const u=t+sx/2, v=t-sx/2; const a=opt.shadow(u,v); if(a>0){out[o]=20;out[o+1]=20;out[o+2]=31;out[o+3]=a;}}
  }
  return {W,H,ox,oy,rgba:out,hits};
}
function darken(col){const c=hex(col);const t=[20,20,31];const m=x=>x.toString(16).padStart(2,"0");return "#"+[0,1,2].map(i=>m(Math.round(c[i]*0.5+t[i]*0.5))).join("");}
function crop(r, bounds){ // bounds {x0,y0,x1,y1} inclusive or auto
  let b=bounds; if(!b){b={x0:1e9,y0:1e9,x1:-1,y1:-1};for(let y=0;y<r.H;y++)for(let x=0;x<r.W;x++)if(r.rgba[(y*r.W+x)*4+3]){b.x0=Math.min(b.x0,x);b.x1=Math.max(b.x1,x);b.y0=Math.min(b.y0,y);b.y1=Math.max(b.y1,y);}}
  const W=b.x1-b.x0+1,H=b.y1-b.y0+1,o=new Uint8Array(W*H*4);
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)o.set(r.rgba.subarray(((y+b.y0)*r.W+x+b.x0)*4,((y+b.y0)*r.W+x+b.x0)*4+4),(y*W+x)*4);
  return {W,H,rgba:o,ox:r.ox-b.x0,oy:r.oy-b.y0,bounds:b};
}
function bboxOf(r){let b={x0:1e9,y0:1e9,x1:-1,y1:-1};for(let y=0;y<r.H;y++)for(let x=0;x<r.W;x++)if(r.rgba[(y*r.W+x)*4+3]){b.x0=Math.min(b.x0,x);b.x1=Math.max(b.x1,x);b.y0=Math.min(b.y0,y);b.y1=Math.max(b.y1,y);}return b;}
module.exports={hex,V,box,rbox,sphere,ellipsoid,cylZ,coneZ,capsule,U,SUB,INT,SU,faceOf,lambert,shade,render,crop,bboxOf,sdBox};
