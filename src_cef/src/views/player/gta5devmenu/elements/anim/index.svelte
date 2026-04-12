<script>
    import { translateText } from 'lang'
    import './main.sass';
    import './fonts/style.css';
    import Animations from 'json/animations.js'
    import AnimElement from './element.svelte'
    import { storeAnimFavorites, storeAnimBind } from 'store/animation'
    import { spring } from 'svelte/motion';
    import { executeClient } from 'api/rage'
    import KeyAnimation from '@/components/keyAnimation/index.svelte';
    export let viewData;
    import keys from 'store/keys'
    import keysName from 'json/keys.js'
    export let selectView;
    import { selectedView } from "./../../index.svelte"
    export let searchText;
    
    let animMenuList = [
        {
            id: 0,
            title: translateText('player', 'Избранное'),
            count: 0
        },
        {
            id: 1,
            title: translateText('player', 'Сесть/Лечь'),
            count: 0
        },
        {
            id: 2,
            title: translateText('player', 'Социальные'),
            count: 0
        },
        {
            id: 3,
            title: translateText('player', 'Позы'),
            count: 0
        },
        {
            id: 4,
            title: translateText('player', 'Неприличное'),
            count: 0
        },
        {
            id: 5,
            title: translateText('player', 'Физ. упражнения'),
            count: 0
        },
        {
            id: 6,
            title: translateText('player', 'Танцы'),
            count: 0
        },
        {
            id: 7,
            title: translateText('player', 'Прочее'),
            count: 0
        }
    ];

    let onSelectedView1;
    if (selectView === "Anim0") {
        onSelectedView1 = animMenuList[0];
    } else if (selectView === "Anim1") {
        onSelectedView1 = animMenuList[1];
    } else if (selectView === "Anim2") {
        onSelectedView1 = animMenuList[2];
    } else if (selectView === "Anim3") {
        onSelectedView1 = animMenuList[3];
    } else if (selectView === "Anim4") {
        onSelectedView1 = animMenuList[4];
    } else if (selectView === "Anim5") {
        onSelectedView1 = animMenuList[5];
    } else if (selectView === "Anim6") {
        onSelectedView1 = animMenuList[6];
    } else if (selectView === "Anim7") {
        onSelectedView1 = animMenuList[7];
    }

    Object.values(Animations).forEach(animation => {
        animMenuList.forEach((item, index) => {
            if (animation[0] === item.title) {
                animMenuList[index].count++;
            }
        });
    });
    
    let favoritesAnim = [];
    storeAnimFavorites.subscribe((value) => {
        favoritesAnim = value;
        animMenuList[0].count = favoritesAnim.length;
    });

    const onSelectMenu = (index) => {
        onSelectedView1 = animMenuList [index];
    }
    let enterAnim = "";




    function handleSlotMouseEnter (index) {
        if (DragonDropData != "")
            return;
        enterAnim = `${onSelectedView1.id}_${index}`;
    }
	
	// Когда выходим из зоны ячейки
	function handleSlotMouseLeave() {
        enterAnim = "";
    }

    let dubleClickData = ''
    let dubleClickTime = 0;
    const onPlayAnimation = (item) => {
        if (dubleClickData === item && dubleClickTime > new Date().getTime()) {
            executeClient ("client.animation.play", item);
            //window.events.callEvent("hud.enter", 'SPACE', 'Нажмите чтобы отменить анимацию');
        } else {
            dubleClickTime = new Date().getTime() + 1000;
            dubleClickData = item
        }
    }

    let DragonDropData = "";
    let offsetInElementX = 0;
    let offsetInElementY = 0;
    let clientX = 0;
    let clientY = 0;

    /* Functions */
    let coords = spring({ x: 0, y: 0 }, {
        stiffness: 1.0,
        damping: 1.0
    });

    const handleMouseDown = (event, item) => {
        const target = event.target.getBoundingClientRect();

        offsetInElementX = (target.width - (target.right - event.clientX)) * 0.7222;
        offsetInElementY = (target.height - (target.bottom - event.clientY)) * 0.7222;
        DragonDropData = item;
        coords.set({ x: event.clientX, y: event.clientY });
        clientX = event.clientX;
        clientY = event.clientY;
    }
    
    let favoriteIndex = -1;
    function handleFavoriteSlotMouseEnter (index) {
        favoriteIndex = index;
    }
	
	// Когда выходим из зоны ячейки
	function handleFavoriteSlotMouseLeave() {
        favoriteIndex = -1;
    }

    let fastSlotIndex = -1;
    let fastSlotAnim = true;

    function handleFastSlotMouseEnter (index) {
        fastSlotIndex = index;
    }
	
	// Когда выходим из зоны ячейки
	function handleFastSlotMouseLeave() {
        fastSlotIndex = -1;
    }

    const handleGlobalMouseUp = () => {
        if (fastSlotIndex !== -1 && DragonDropData != "" && DragonDropData.split("_") && DragonDropData.split("_").length) {
            window.animationStore.addAnimBind(fastSlotIndex, DragonDropData);
        }
        DragonDropData = "";
        fastSlotAnim = false;
    }


    const onDell = (item) => {
        window.animationStore.dellAnimBind(item);
    }
    // Глобальные эвенты    
    const handleGlobalMouseMove = (event) => {
        if (DragonDropData != "" && DragonDropData.split("_") && DragonDropData.split("_").length) {
            if (clientX !== event.clientX || clientY !== event.clientY) {
                dubleClickData = ''
                coords.set({ x: event.clientX, y: event.clientY });
                fastSlotAnim = true;
            }
        }
    }

    const IsFavorite = (index, AnimListFavorites) => {
        let success = false;
        if (AnimListFavorites) {
            if (AnimListFavorites.findIndex(a => a == `${onSelectedView1.id}_${index}`) !== -1) success = true;
        }
        return success;
    }

    const AddFavorite = (event, item) => {
        event.stopPropagation();
        window.animationStore.addAnimFavorite(item);
    }

    const DellFavorite = (event, item) => {
        event.stopPropagation();
        window.animationStore.dellAnimFavorite(item);
    }

    const StopAnim = () => {
        viewData = false;
        executeClient ("client.animation.stop");
    }

    const OnClose = () => {
        executeClient ("escape");
    }
</script>

<svelte:window on:mouseup={handleGlobalMouseUp} on:mousemove={handleGlobalMouseMove} />

<div id="animations1">
    {#if DragonDropData != "" && ($coords.x !== clientX || $coords.y !== clientY) && DragonDropData.split("_") && DragonDropData.split("_").length}
                <div class="blockbg1" style={`top:${$coords.y - offsetInElementY}px;left:${$coords.x - offsetInElementX}px;`}>
					<img src="./images/preview.png" alt=""/>                                
                </div>
    {/if}
</div>
    <div class="list2">
        {#if onSelectedView1.id === 0}
            {#each favoritesAnim as animation, index}
                {#if (!searchText || !searchText.length) || (searchText && Animations [animation][2].toLowerCase().trim().includes(searchText.toLowerCase().trim()))}
                    <AnimElement
                        title={Animations [animation][2]}
                        isEnterAnim={favoriteIndex === index}
                        use={animation}
                        isFavorite={true}
                        dellFavorite={DellFavorite}
                        {onPlayAnimation}
                        on:mousedown={(event) => handleMouseDown (event, animation)}
                        on:mouseenter={() => handleFavoriteSlotMouseEnter (index)}
                        on:mouseleave={handleFavoriteSlotMouseLeave} />
                {/if}
            {/each}
            {:else}
            {#each Object.values(Animations).filter(el => el[0] === onSelectedView1.title) as animation, index}
                {#if (!searchText || !searchText.length) || (searchText && animation[2].toLowerCase().trim().includes(searchText.toLowerCase().trim()))}
                    <AnimElement
                        title={animation[2]}
                        isEnterAnim={enterAnim === `${onSelectedView1.id}_${animation[1]}`}
                        use={`${onSelectedView1.id}_${animation[1]}`}
                        isFavorite={IsFavorite(animation[1], favoritesAnim)}
                        addFavorite={AddFavorite}
                        dellFavorite={DellFavorite}
                        {onPlayAnimation}
                        on:mousedown={(event) => handleMouseDown (event, `${onSelectedView1.id}_${animation[1]}`)}
                        on:mouseenter={() => handleSlotMouseEnter (animation[1])}
                        on:mouseleave={handleSlotMouseLeave} />
                {/if}
            {/each}
        {/if}
    </div>
    <div class="fastslotanim">
        {#each $storeAnimBind as item, index}
            <div class="fastblock">
                <div class="blockbg">
                    <span>{index + 1 === 10 ? 0 : index + 1}</span>
                    {#if $storeAnimBind && Animations [item]}
                    <img src="./images/preview.png" alt=""/> 
                    <AnimElement
                        isEnterAnim={fastSlotIndex === index}
                        use={item}
                        isBind={true}
                        {onDell}
                        {onPlayAnimation}
                        on:mouseenter={() => handleFastSlotMouseEnter (index)}
                        on:mouseleave={handleFastSlotMouseLeave} />
                    {:else}
                    <AnimElement
                        animations__anim={fastSlotAnim}
                        isBind={true}
                        use={""}
                        on:mouseenter={() => handleFastSlotMouseEnter (index)}
                        on:mouseleave={handleFastSlotMouseLeave} />
                    {/if}
                </div>
                {#if $storeAnimBind && Animations [item]}
                <h1>{Animations [item][2]}</h1>
                {:else}
                <h1>{translateText('player', 'Пусто')}</h1>
                {/if}
            </div>
        {/each}
    </div>