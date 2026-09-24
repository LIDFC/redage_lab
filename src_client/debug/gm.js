// Восстановлено из собранного client_packages/main.js (модуль 6516).
// Исходник не попал в git из-за правила [Dd]ebug/ в .gitignore.
const __req = () => { throw new Error('unexpected require'); };
__req.g = global;
(function (e,t,a) {
const n=mp._events.add;a.g.gm={},gm.events={},gm.events.add=(e,t)=>{n(e,function(){try{const a=t.apply(null,arguments);a instanceof Promise&&a.catch(t=>crushLog("eventAdd.1",e,t.stack))}catch(t){crushLog("eventAdd.2",e,t.stack)}})}
})(module, module.exports, __req);
