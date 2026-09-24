// Восстановлено из собранного client_packages/main.js (модуль 6537).
// Исходник не попал в git из-за правила [Dd]ebug/ в .gitignore.
const __req = () => { throw new Error('unexpected require'); };
__req.g = global;
(function (e,t,a) {
gm.discord=e=>{let t="на RedAge";a.g.localplayer&&void 0!==a.g.localplayer.remoteId&&(t=translateText("на RedAge под ID {0}",a.g.localplayer.remoteId)),mp.discord.update(e,t)},a.g.discordDefault=()=>{gm.discord(translateText("Наслаждается жизнью"))},discordDefault()
})(module, module.exports, __req);
