<script>
    import { getActors } from 'json/quests/quests.js';
    import imgCityhall from '../../assets/quest/cityhall.png';
    import imgPolice from '../../assets/quest/police.png';
    import imgEms from '../../assets/quest/ems.png';
    import imgNews from '../../assets/quest/news.png';
    import imgOrg from '../../assets/quest/org.png';

    export let actor;

    // Портретов NPC нет ни в архиве, ни на CDN: фракционным NPC ставим логотип фракции,
    // остальным — инициалы.
    const logos = {
        npc_cityhall: imgCityhall,
        npc_fracpolic: imgPolice,
        npc_fracems: imgEms,
        npc_fracnews: imgNews,
        npc_org: imgOrg,
    };

    $: logo = logos[actor];
    $: name = (getActors(actor) || {}).name || "";
    $: initials = name.split(" ").filter((part) => part.length).slice(0, 2).map((part) => part[0].toUpperCase()).join("") || "?";
</script>

{#if logo}
    <img class="questavatar" src={logo} alt=""/>
{:else}
    <div class="questavatar initials">{initials}</div>
{/if}

<style>
    .questavatar {
        flex-shrink: 0;
        object-fit: contain;
    }
    .initials {
        display: flex;
        align-items: center;
        justify-content: center;
        border-radius: 50%;
        background: linear-gradient(135deg, rgba(255, 255, 255, 0.22), rgba(255, 255, 255, 0.06));
        border: 0.0926vh solid rgba(255, 255, 255, 0.25);
        color: #fff;
        font-family: SF Pro Display;
        font-weight: 700;
        font-size: 1.6667vh;
        box-sizing: border-box;
    }
</style>
