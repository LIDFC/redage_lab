<script>
    import BattlePass from './components/battlepass/index.svelte'
    import Missions from './components/missions.svelte'
    import Tasks from './components/battlepass/tasks.svelte'
    import { isPopupInfoOpened, isPopupLvlOpened, isPopupBuyOpened } from './stores.js'

    import BattlePassPopupBuyPremium from './components/battlepass/popupBuyPremium.svelte'
    import BattlePassPopupBuyLvl from './components/battlepass/popupBuyLvl.svelte'

    import './main.sass'
    import './main.css'  
    import './assets/fonts/style.css' 

    import { setGroup, executeClientToGroup } from 'api/rage'
    setGroup (".battlepass.");
    export let selectView;

    let SelectViews2;
    if (selectView === "BP1") {
        SelectViews2 = "BattlePass";
    } else if (selectView === "BP2") {
        SelectViews2 = "Buylvl";
    } else if (selectView === "BP3") {
        SelectViews2 = "Tasks";
    } else if (selectView === "BP4") {
        SelectViews2 = "Missions";
    }

    const handleKeyUp = (event) => {
        const { keyCode } = event;

        if (keyCode === 27)
            onClose ();
    }

    const onClose = () => {
        if ($isPopupBuyOpened || $isPopupLvlOpened || $isPopupInfoOpened) {
            isPopupBuyOpened.set (false);
            isPopupLvlOpened.set (false);
            isPopupInfoOpened.set (false);
            return;
        }

        executeClientToGroup ("close")
    }
</script>

<svelte:window on:keyup={handleKeyUp} />

{#if $isPopupBuyOpened}
        <BattlePassPopupBuyPremium />
    {:else if SelectViews2 === "Buylvl"}
        <BattlePassPopupBuyLvl />
    {:else if SelectViews2 === "Tasks"}
        <Tasks />
    {:else if SelectViews2 === "Missions"}
        <Missions />
    {:else if SelectViews2 === "BattlePass"}
        <BattlePass />
{/if}