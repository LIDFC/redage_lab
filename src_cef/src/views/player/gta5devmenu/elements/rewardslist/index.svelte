<script>
    import { translateText } from 'lang'
    import './main.sass';

    import Everyday from './everyday.svelte'
    import Donates from './donates.svelte'
    export let selectView;

    const Views = {
        Everyday,
        Donates
    }

    let isLoad = false;

    let onSelectedView1;
    if (selectView === "Q2") {
        onSelectedView1 = "Everyday";
    } else if (selectView === "Q3") {
        onSelectedView1 = "Donates";
    }
    const onSetLoad = (toggled) => {
        isLoad = toggled;
    }
</script>

<svelte:component this={Views[onSelectedView1]} {onSetLoad} />