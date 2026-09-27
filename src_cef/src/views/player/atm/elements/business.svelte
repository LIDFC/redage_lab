<script>
    import { sound } from 'api/uiSound'
    import { executeClient } from 'api/rage'
    export let type;
    export let subdata;

    // Раньше здесь автоматически «нажимались» все бизнесы подряд и открывался последний — теперь игрок выбирает сам
    $: list = Array.isArray(subdata) ? subdata : [];

    const onSelect = (index) => executeClient ("atmCB", type, index);
    const onBack = () => executeClient ("atmCB", 2, 0);
</script>
<h1>Налог за бизнес</h1>
<div class="atm__sub">Выберите бизнес, на счёт которого внести деньги</div>
<div class="atm__list">
    {#each list as name, index}
        <div class="atm__list_item" use:sound={"atm"} on:click={() => onSelect (index)}>
            <i class="atm__icon store"></i>
            <span>{name}</span>
            <b>›</b>
        </div>
    {/each}
</div>
<div class="atm__buttons single">
    <div class="atm__btn" use:sound={"atm"} on:click={onBack}>Назад</div>
</div>
