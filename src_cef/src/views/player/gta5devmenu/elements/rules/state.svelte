<script>
    import Page1 from "./state/page1.svelte";
    import Page2 from "./state/page2.svelte";
    import Page3 from "./state/page3.svelte";
    import Page4 from "./state/page4.svelte";
    import Page5 from "./state/page5.svelte";
    import Page6 from "./state/page6.svelte";
    import Page7 from "./state/page7.svelte";
    import Page8 from "./state/page8.svelte";
    import Page9 from "./state/page9.svelte";
    import Page10 from "./state/page10.svelte";
    import Page11 from "./state/page11.svelte";

    const Views = {
        Page1,
        Page2,
        Page3,
        Page4,
        Page5,
        Page6,
        Page7,
        Page8,
        Page9,
        Page10,
        Page11,
    }

    let onSelectedView2 = "Page1";

</script>

<div class="menurules">
    <div class="rulesleft">
        <div class="headrules">
            <h1>ВЫБОР КАТЕГОРИИ</h1>
        </div>
        <div class="ruleslist">
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page1"} class:active={onSelectedView2 == "Page1"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page2"} class:active={onSelectedView2 == "Page2"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page3"} class:active={onSelectedView2 == "Page3"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page4"} class:active={onSelectedView2 == "Page4"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page5"} class:active={onSelectedView2 == "Page5"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page6"} class:active={onSelectedView2 == "Page6"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page7"} class:active={onSelectedView2 == "Page7"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page8"} class:active={onSelectedView2 == "Page8"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page9"} class:active={onSelectedView2 == "Page9"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page10"} class:active={onSelectedView2 == "Page10"}>
                <p>Основные правила проекта</p>
            </div>
            <div class="rulesblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page11"} class:active={onSelectedView2 == "Page11"}>
                <p>Основные правила проекта</p>
            </div>
        </div>
    </div>
    <div class="rulesright">
        <svelte:component this={Views[onSelectedView2]}/>
    </div>
</div>