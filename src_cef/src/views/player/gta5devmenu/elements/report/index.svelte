<script>
    import { executeClient } from 'api/rage'
    import { selected, reportsData, text } from '../../../reports/index'
    import { onDestroy } from 'svelte'
    
    let
        reports = [],
        answer = $text;

    onDestroy(() => {
        text.set(answer) 
    });

    reportsData.subscribe(value => {
		reports = value;
    });

    let antiSpam = new Date().getTime();
    const onSelectReport = (report, index) => {
        if (report.blocked) return;
        else if ($selected) return;
        else if (new Date().getTime() - antiSpam < 500) return;
        antiSpam = new Date().getTime();

        selected.set (report);
        //if (index != undefined && reports[index]) {
        //    reports[index].blocked = true;
        //    reports[index].blockedBy = $charName;
        //}
        executeClient ("takereport", report.id);
    }

    const onSendAnswer = (report) => {
        if (!report) return;
        else if (!answer) return;
        
        executeClient ("sendreport", report.id, answer);
        selected.set(false);
        answer = "";
    }

    const onReturnReport = (report) => {
        if (!report) return;

        //const index = reports.findIndex(r => r.id == report.id);
        //if (index != undefined && reports[index]) {
        //    reports[index].blocked = false;
        //    reports[index].blockedBy = "";
        //}
        executeClient ("takereport", report.id);
        selected.set(false);
        answer = "";
    }

    const func = (funcName) => {
        const report = $selected;
        if (!report) return;
        executeClient ("funcreport", report.id, funcName);
    }

    const onKeyBan = (e) => {
        if (event.key === 'Enter') {
            event.preventDefault();
        }
    }

    const sendsms = (event) => {
        const { keyCode } = event;
        if (keyCode !== 13) return;

        setTimeout(() => {
            onSendAnswer($selected);
        }, 50);

    }
</script>

<svelte:window on:keyup={sendsms} />

<div class="reportmenu">
    <div class="reportleft">
        <div class="reporthead">ИСТОРИЯ ОБРАЩЕНИЙ</div>
        <div class="reportlist">
            {#each reports as report, index}
                <div class="reportblock" on:keypress={() => {}} on:click={e => onSelectReport(report, index)}>
                    <div class="headblock">
                        <p>Обращение:<b>#{report.id}</b></p>
                        {#if report.blocked}
                        <span>{report.blockedBy}</span>
                        {/if}
                        <div class="timedata">{report.author}</div>
                    </div>
                    <div class="textreport">
                        <p>{report.text}</p>
                    </div>
                </div>
            {/each}
        </div>
    </div>
    <div class="reportright">
        <div class="reporthead">ОБРАЩЕНИЕ: {#if $selected}#{$selected.id}{:else}не выбрано{/if}</div>
        <div class="bgreportmessage">
            {#if $selected}
                <div class="messageuser">
                    <div class="messleft">
                        <div class="headmess">
                            <p>#{$selected.id}<b>{$selected.author}</b></p>
                        </div>
                        <div class="messus">
                            <p>{$selected.text}</p>
                        </div>
                    </div>
                    <div class="messright">
						<img src="{document.cloud}img/report_icon.png" alt=""/>
                    </div>
                </div>
                <div class="btns">
                    <div class="btnreport" on:keypress={() => {}} on:click={e => onReturnReport($selected)}>
                        <p>Закрыть обращение</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("tp")}>
                        <p>Телепортироваться</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("sp")}>
                        <p>Следить</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("metp")}>
                        <p>Телепортировать к себе</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("stats")}>
                        <p>Статистика</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("kill")}>
                        <p>Убить</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("ptime")}>
                        <p>Личное время</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("checkdim")}>
                        <p>Проверить мир</p>
                    </div>
                    <div class="btnreport" on:keypress={() => {}} on:click={e => func("nhistory")}>
                        <p>История</p>
                    </div>
                </div>
            {/if}
        </div>
        <div class="inputbg">
            <input onKeyPress={e => onKeyBan(e)} ref="answerBox" bind:value={answer} placeholder="Напишите сообщение...">
        </div>
    </div>
</div>