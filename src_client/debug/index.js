// Восстановлено из собранного client_packages/main.js (модуль 3337).
// Исходник не попал в git из-за правила [Dd]ebug/ в .gitignore.
const __req = () => { throw new Error('unexpected require'); };
__req.g = global;
(function (e,t,a) {
require("./gm"),require("./natives"),require("./streaming"),require("./timers"),require("./discord"),a.g.isDebug=!1,a.g.debugLog=(e,t=!1)=>{(a.g.isDebug||t)&&mp.console.logError(`[RedAge] DebugLog: ${e}`,!0)};let n=[];a.g.crushLog=(e,t,a)=>{const o=a&&a.stack?a.stack:JSON.stringify(a),i=`${e}_${t}_${o}`;n.includes(i)||(n.push(i),mp.events.callRemote("client_trycatch",e,t,o))}
})(module, module.exports, __req);
