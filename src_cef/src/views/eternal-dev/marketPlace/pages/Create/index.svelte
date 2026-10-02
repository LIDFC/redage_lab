<script>
    import { createLot, setModalState } from "../../modules/functions";

    import UserInfo from "../../components/UserInfo/index.svelte";
    import StorageCard from "../Storage/StorageCard/index.svelte";
    import CreateCard from "./CreateCard/index.svelte";
    import CreateModal from "../../modals/CreateModal/index.svelte";

    import { calculatedColumnCount } from "../../modules/formats";

    let columnCount = calculatedColumnCount();

    export let items;

    let selectedItem = null;

    // Вещи из инвентаря и со склада маркетплейса — отдельными разделами, имущество — сверху
    $: estateItems = (items || []).filter(x => x.type != "item" && x.type != "clothes");
    $: inventoryItems = (items || []).filter(x => (x.type == "item" || x.type == "clothes") && x.source == "inv");
    $: storageItems = (items || []).filter(x => (x.type == "item" || x.type == "clothes") && x.source != "inv");

    const setModal = (modalName) => {
        selectedItem = null
        setModalState(modalName)
    };

    const callbackCreateLot = (hours, paymentType, count, price) => {
        createLot(selectedItem, price, {
            hours,
            count,
            paymentType
        });

        setModal(null);
    };
</script>

<svelte:window on:resize={(e) => { columnCount = calculatedColumnCount(); }} />

<style>
    .create-hint {
        grid-column: 1 / -1;
        padding: 1.4vh 1.8vh;
        border-radius: 0.8vh;
        background: rgba(61, 130, 213, 0.08);
        border: 1px solid rgba(61, 130, 213, 0.35);
        color: rgba(15, 15, 15, 0.8);
        font-size: 1.3vh;
        line-height: 1.5;
    }
    .create-hint b {
        display: block;
        color: #3d82d5;
        font-size: 1.45vh;
        margin-bottom: 0.3vh;
    }
    .create-section {
        grid-column: 1 / -1;
        font-weight: 900;
        font-size: 1.6vh;
        color: rgba(15, 15, 15, 0.85);
        margin-top: 0.6vh;
    }
    .create-section span {
        font-weight: 600;
        font-size: 1.3vh;
        color: rgba(15, 15, 15, 0.4);
        margin-left: 0.6vh;
    }
    .create-empty {
        grid-column: 1 / -1;
        font-size: 1.3vh;
        color: rgba(15, 15, 15, 0.45);
    }
</style>

{#if selectedItem != null && selectedItem.type != "item" && selectedItem.type != "clothes"}
    <CreateCard data={selectedItem} backFunction={() => selectedItem = null} isEditing={false} />
{:else}
    <div class="page-data create">
        {#if selectedItem != null && (selectedItem.type == "item" || selectedItem.type == "clothes")}
            <CreateModal setModal={setModal} type="item" data={selectedItem} callback={callbackCreateLot} />
        {/if}

        <div class="header">
            <div class="header__content">
                <div class="header__title">
                    Выберите товар для продажи
                </div>
            </div>

            <UserInfo />
        </div>

        <div class="content">
            <div class="list" style={"--column-count: " + columnCount}>
                <StorageCard data={{
                    id: -1,
                    type: "service",

                    params: {
                        name: "Создать объявление"
                    },
                }} type="create" onClick={() => selectedItem = {type: "service"}} />
                {#each estateItems as item}
                    <StorageCard data={item} type="create" onClick={() => selectedItem = item} />
                {/each}

                <div class="create-hint">
                    <b>Одежду, аксессуары и предметы можно продавать прямо из инвентаря.</b>
                    Склад хранения маркетплейса (точка «Склад хранения» на карте) — для крупных партий и хранения: туда же приходят купленные вещи.
                </div>

                <div class="create-section">Из инвентаря <span>{inventoryItems.length}</span></div>
                {#each inventoryItems as item}
                    <StorageCard data={item} type="create" onClick={() => selectedItem = item} />
                {:else}
                    <div class="create-empty">В инвентаре нет вещей, которые можно продать</div>
                {/each}

                <div class="create-section">Со склада маркетплейса <span>{storageItems.length}</span></div>
                {#each storageItems as item}
                    <StorageCard data={item} type="create" onClick={() => selectedItem = item} />
                {:else}
                    <div class="create-empty">Склад пуст — отнесите вещи на «Склад хранения», чтобы продавать партиями</div>
                {/each}
            </div>
        </div>
    </div>
{/if}