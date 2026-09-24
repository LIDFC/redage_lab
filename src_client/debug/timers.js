// Восстановлено из собранного client_packages/main.js (модуль 3360).
// Исходник не попал в git из-за правила [Dd]ebug/ в .gitignore.
const __req = () => { throw new Error('unexpected require'); };
__req.g = global;
(function () {
const e=t=>t?(t^16*Math.random()>>t/4).toString(16):([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g,e);let t=new Map;const a=e=>{const a=t.get(e);if(a){try{a.callback()}catch(e){}t.delete(e)}};gm.createTimeout=(n,o,i)=>{let s;return(...r)=>{i?(s=null,n.apply(this,r)):(s&&((e=>{const a=t.get(e.id);a&&(clearTimeout(a.timerId),t.delete(e.id))})(s),s=null),s=((n,o=0)=>{const i=e()+"-"+Date.now().toString(36),s={};return s.id=i,s.delay=o,s.start=Date.now(),s.callback=n,s.timerId=setTimeout(()=>{a(i)},o),t.set(i,s),s})(()=>{s=null,n.apply(this,r)},o))}},gm.timers=new class{setTimeout(e,t,...a){return setTimeout((...a)=>{try{e(...a)}catch(e){crushLog("setTimeout",t.toString(),e.stack)}},t,...a)}setInterval(e,t,...a){return setInterval((...a)=>{try{e(...a)}catch(e){crushLog("setInterval",t.toString(),e.stack)}},t,...a)}setImmediate(e,...t){return setTimeout((...t)=>{try{e(...t)}catch(e){crushLog("setImmediate",delay.toString(),e.stack)}},0,...t)}}
})(module, module.exports, __req);
