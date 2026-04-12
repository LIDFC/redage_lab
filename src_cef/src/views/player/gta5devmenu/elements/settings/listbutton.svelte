<script>

    export let active = false;
    export let name = "";
    export let onChange;

    const OnChangUse = (change) => {
        if (onChange)
            onChange(change);
    }

    const handleKeyUp = event => {
        if (!active) return;
        const { keyCode } = event;
        switch (keyCode) {
            case 37: // left
                OnChangUse(-1);
                break;
            case 39: // right
                OnChangUse(1);
                break;
        }
    }
</script>

<svelte:window on:keyup={handleKeyUp} />
<div class="leftselcet">
    <svg width="7" height="11" viewBox="0 0 7 11" fill="none" xmlns="http://www.w3.org/2000/svg" on:keypress={() => {}} on:click={() => OnChangUse(-1)}>
        <path d="M5.5 1L1 5.5L5.5 10" stroke-linecap="round"/>
    </svg>                                            
</div>
<b>{name}</b>
<div class="rightselcet">
    <svg width="7" height="11" viewBox="0 0 7 11" fill="none" xmlns="http://www.w3.org/2000/svg" on:keypress={() => {}} on:click={() => OnChangUse(1)}>
        <path d="M1 10L5.5 5.5L1 1" stroke-linecap="round"/>
    </svg>
</div>