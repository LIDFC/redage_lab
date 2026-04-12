<script>
    import ProgressBar from 'progressbar.js';

    import { setGroup, executeClientToGroup, executeClientAsyncToGroup } from 'api/rage'
    setGroup (".battlepass.");


    let missionsArray = [];


    const getMissions = (index) => {

        executeClientAsyncToGroup("getMissions", index).then((result) => {
            if (result && typeof result === "string")
                missionsArray = JSON.parse(result)
        });
    }

    getMissions(0);

    //

    const statusData = {
        closed: 0,
        selected: 1,
        done: 2,
        active: 3
    }

    const lineData = [
        "battlepassicons-bottom-top-right1",
        "",
        "battlepassicons-top-bottom",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "battlepassicons-bottom-top-right1",
        "battlepassicons-top-bottom-right",
        "",
        "",
        "",
        "",
        "",
        ""
    ];

    let hoverMission = false;
    import { spring } from 'svelte/motion';

    let coords = spring({ x: 0, y: 0 }, {
        stiffness: 1.0,
        damping: 1.0
    });

    let offsetInElementX = 0;
    let offsetInElementY = 0;
    const handleFavoriteSlotMouseEnter = (event, index) => {
        const target = event.target.getBoundingClientRect();

        offsetInElementX = 15 * (target.height / 100);
        offsetInElementY = 125 * (target.height / 100);

        coords.set({ x: event.clientX, y: event.clientY });

        hoverMission = missionsArray [index];
        hoverMission.i = index;
    }

    const handleFavoriteSlotMouseLeave = () => {
        hoverMission = false;
    }

    // Глобальные эвенты
    const handleGlobalMouseMove = (event) => {
        if (hoverMission !== false) {
            coords.set({ x: event.clientX, y: event.clientY });
        }
    }

    //

    const isStatusActive = (status) => status === statusData.done;

    const isActiveLine = (index, missions) => {

        if ((index === 0 || index === 2) && isStatusActive (missions [0].status))
            return true;

        if ((index === 1) && isStatusActive (missions [1].status))
            return true;

        if ((index === 3) && isStatusActive (missions [3].status))
            return true;

        if ((index === 4) && isStatusActive (missions [4].status))
            return true;

        //


        if ((index === 5) && isStatusActive (missions [2].status))
            return true;

        if ((index === 6 || index === 9) && isStatusActive (missions [6].status))
            return true;

        if ((index === 7) && isStatusActive (missions [7].status))
            return true;

        if ((index === 8) && isStatusActive (missions [8].status))
            return true;

        if ((index === 10) && isStatusActive (missions [10].status))
            return true;

        if ((index === 11 || index === 12) && isStatusActive (missions [11].status))
            return true;

        if ((index === 13) && isStatusActive (missions [12].status))
            return true;

        if ((index === 14) && isStatusActive (missions [13].status))
            return true;

        if ((index === 15) && isStatusActive (missions [15].status))
            return true;

        if ((index === 16) && isStatusActive (missions [16].status))
            return true;

        if ((index === 17) && isStatusActive (missions [17].status))
            return true;

        if ((index === 18) && isStatusActive (missions [18].status))
            return true;

        return false;
    }


    const isActiveBox = (index) => {

        if ((index === 1 || index === 2) && isStatusActive (missionsArray [0].status))
            return true;

        if ((index === 3) && isStatusActive (missionsArray [1].status))
            return true;

        if ((index === 4) && isStatusActive (missionsArray [3].status))
            return true;

        if ((index === 5) && isStatusActive (missionsArray [4].status))
            return true;

        //

        if ((index === 6) && isStatusActive (missionsArray [2].status))
            return true;

        if ((index === 7 || index === 10) && isStatusActive (missionsArray [6].status))
            return true;

        if ((index === 8) && isStatusActive (missionsArray [7].status))
            return true;

        if ((index === 9) && isStatusActive (missionsArray [8].status))
            return true;

        if ((index === 11) && isStatusActive (missionsArray [10].status))
            return true;

        if ((index === 12 || index === 13) && isStatusActive (missionsArray [11].status))
            return true;

        if ((index === 14) && isStatusActive (missionsArray [12].status))
            return true;

        if ((index === 15) && isStatusActive (missionsArray [13].status))
            return true;

        if ((index === 16) && isStatusActive (missionsArray [15].status))
            return true;

        if ((index === 17) && isStatusActive (missionsArray [16].status))
            return true;

        if ((index === 18) && isStatusActive (missionsArray [17].status))
            return true;

        if ((index === 19) && isStatusActive (missionsArray [18].status))
            return true

        if ((index === 20) && isStatusActive (missionsArray [19].status))
            return true;

        return false;
    }

    let progressBarId = null;

    const CreateProgressBar = (index, id) => {
        if (!window.loaderData.delay ("battlePass.CreateProgressBar", 1))
            return;

        if (progressBarId !== null)
            progressBarId.destroy();

        progressBarId = new ProgressBar.Circle("#progressBarKey" + index, {
            color: '#6495ED',
            strokeWidth: 6,
            trailWidth: 8,
            easing: 'easeInOut',
            duration: 900,
            trailColor: 'rgba(140, 149, 237, 0)', //цвет фонового кружка
            text: {
                autoStyleContainer: false
            },
            from: { width: 4 },
            to: { width: 6 },
            // Set default step function for all animate calls
            step: function(state, circle) {
                circle.path.setAttribute('stroke-width', state.width);
            }
        });

        StartTimer (id);
    };

    let intervalId = null;

    const StartTimer = (index) => {
        let progress = 0;
        intervalId = setInterval (() => {
            progress += 10;

            if (progress < 1000) {
                progressBarId.set(progress / 1000);
            } else {
                Clear (index);
                //Конец
            }
        }, 8);
    }

    const Clear = (id = -1) => {

        if (typeof id === "number")
            executeClientToGroup ("setMissions", id)

        if (intervalId !== null)
            clearInterval (intervalId);

        intervalId = null;

        if (progressBarId !== null)
            progressBarId.destroy();

        progressBarId = null;
    };

    import { addListernEvent } from 'api/functions'
    addListernEvent ("updateMissions", () => {
        getMissions(0);
    })

    let boxPopup;
    const fixOutToX = (coordsX, offset, element) => {
        if (!element) return coordsX;
        else if (document.querySelector('#battlepass1')) {
            let mainWidth = document.querySelector('#battlepass1').getBoundingClientRect().width;
            let elementWidth = element.getBoundingClientRect().width;
            if ((elementWidth + coordsX + offset) >= mainWidth) return coordsX - offset - elementWidth;
            return coordsX + offset;
        }
        return coordsX + offset;
    }

    const onSkip = () => {
        if (!window.loaderData.delay ("battlePass.onSkip", 1))
            return;

        executeClientToGroup ("skip")

    };
</script>

<svelte:window on:mousemove={handleGlobalMouseMove} on:mouseup={Clear} />

{#if hoverMission !== false}
    <div bind:this={boxPopup} class="hovermisson" style={`top:${$coords.y - offsetInElementY}px;left:${fixOutToX ($coords.x, offsetInElementX, boxPopup)}px;`}>
        <div class="headmis">
            <h1>{hoverMission.name} ({#if hoverMission.title == "Лёгкое задание"}<b style="color: rgb(50, 166, 134);">Легко</b>{/if}{#if hoverMission.title == "Необычное задание"}<b style="color: rgb(248, 201, 65);">Нормально</b>{/if}{#if hoverMission.title == "Сложное задание"}<b style="color: rgb(253, 33, 61);">Сложно</b>{/if})</h1>
            <div class="rewradmis">
                <p>Награда:</p>
                <div class="rewradmislist">
                    <span>2 <svg class="exp" width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><g clip-path="url(#clip0_1130_76)"><path d="M15.9701 14.6773C15.9698 14.6779 15.9695 14.6782 15.9692 14.6788C15.9463 14.7187 15.9122 14.751 15.8712 14.7718C15.8301 14.7925 15.7839 14.8007 15.7382 14.7954C8.08847 13.8948 0.339062 14.7863 0.261874 14.7954C0.216112 14.8007 0.169783 14.7925 0.128661 14.7717C0.0875391 14.751 0.0534435 14.7185 0.0306243 14.6785C0.00763091 14.638 -0.00280469 14.5917 0.000645528 14.5452C0.00409574 14.4988 0.0212758 14.4545 0.0499993 14.4179C0.0618743 14.4026 1.27062 12.8576 2.82159 10.4832C4.25159 8.29413 6.27319 4.93225 7.78412 1.34663C7.80199 1.3041 7.83201 1.2678 7.87043 1.24227C7.90884 1.21674 7.95394 1.20313 8.00006 1.20312C8.04619 1.20313 8.09128 1.21674 8.1297 1.24227C8.16811 1.2678 8.19814 1.3041 8.216 1.34663C9.72694 4.93225 11.7485 8.29413 13.1785 10.4832C14.7295 12.8576 15.9382 14.4026 15.9501 14.4179C15.9787 14.4543 15.9958 14.4984 15.9994 14.5445C16.0029 14.5907 15.9927 14.6369 15.9701 14.6773Z" fill="#D792FF"/><path d="M15.9701 14.6772C15.9697 14.6778 15.9694 14.6781 15.9691 14.6788L12.4644 12.6728L8 7.45534V1.20312C8.09437 1.20312 8.17938 1.25969 8.21594 1.34656C9.72688 4.93219 11.7485 8.29406 13.1785 10.4831C14.7294 12.8575 15.9382 14.4025 15.9501 14.4178C15.9786 14.4542 15.9958 14.4983 15.9993 14.5445C16.0029 14.5906 15.9927 14.6368 15.9701 14.6772Z" fill="#B078FF"/><path d="M15.9698 14.6788C15.9469 14.7187 15.9129 14.751 15.8718 14.7717C15.8308 14.7924 15.7845 14.8006 15.7388 14.7953C8.08909 13.8947 0.339687 14.7863 0.2625 14.7953C0.216738 14.8007 0.170409 14.7925 0.129287 14.7717C0.0881648 14.7509 0.0540692 14.7185 0.03125 14.6784L5.21472 11.7303L10.7297 11.6794L15.9698 14.6788ZM15.9707 14.6772C15.9704 14.6778 15.9701 14.6781 15.9698 14.6788C15.9698 14.6788 9.99944 10.2603 8.00066 1.20312C8.09503 1.20312 8.18003 1.25969 8.21659 1.34656C9.72753 4.93219 11.7491 8.29406 13.1791 10.4831C14.7301 12.8575 15.9388 14.4025 15.9507 14.4178C15.9793 14.4542 15.9964 14.4983 16 14.5445C16.0035 14.5906 15.9934 14.6368 15.9707 14.6772Z" fill="#9057EA"/><path d="M11.6859 11.5062C10.6321 9.8931 9.2652 7.6591 7.99927 5.15723C6.73333 7.6591 5.36645 9.8931 4.31264 11.5062C4.05267 11.9041 3.78894 12.2996 3.52148 12.6926C4.02321 12.664 4.52518 12.6399 5.02733 12.6202C6.9658 12.5441 9.61114 12.5095 12.4635 12.6727C12.2007 12.2863 11.9415 11.8974 11.6859 11.5062Z" fill="#EBBBFF"/><path d="M7.20011 12.3959C7.22538 12.3784 7.24694 12.356 7.26356 12.3301C7.28018 12.3042 7.29154 12.2752 7.29698 12.2449C7.30242 12.2147 7.30184 12.1836 7.29527 12.1535C7.28871 12.1234 7.27628 12.095 7.25871 12.0697C7.25151 12.0593 6.52986 11.0184 5.88218 9.80717C5.86221 9.76981 5.83249 9.73857 5.79618 9.71675C5.75987 9.69494 5.71833 9.68337 5.67597 9.68328C5.63361 9.6832 5.59202 9.69459 5.55562 9.71625C5.51922 9.73791 5.48937 9.76903 5.46925 9.80631C5.45083 9.84037 5.44115 9.87847 5.44108 9.91719C5.44101 9.95592 5.45054 9.99405 5.46883 10.0282C6.12994 11.2645 6.86655 12.3268 6.87392 12.3373C6.9094 12.3884 6.96371 12.4232 7.02488 12.4342C7.08605 12.4452 7.14908 12.4314 7.20011 12.3959ZM5.9126 12.2686C5.96364 12.2331 5.99848 12.1788 6.00948 12.1176C6.02048 12.0564 6.00673 11.9934 5.97125 11.9424C5.96812 11.9378 5.65459 11.4854 5.37348 10.9598C5.35351 10.9224 5.32379 10.8912 5.28748 10.8693C5.25118 10.8475 5.20963 10.836 5.16727 10.8359C5.12492 10.8358 5.08333 10.8472 5.04693 10.8688C5.01053 10.8905 4.98068 10.9216 4.96055 10.9589C4.94214 10.993 4.93246 11.0311 4.93239 11.0698C4.93231 11.1085 4.94185 11.1466 4.96014 11.1808C5.25467 11.7316 5.573 12.1907 5.58645 12.21C5.60401 12.2352 5.62638 12.2568 5.65228 12.2734C5.67817 12.29 5.70709 12.3014 5.73738 12.3068C5.76767 12.3123 5.79873 12.3117 5.8288 12.3051C5.85886 12.2986 5.88734 12.2862 5.9126 12.2686Z" fill="#CBA7FF"/><path d="M7.81211 8.25381C7.78749 8.27229 7.76676 8.29543 7.75109 8.32193C7.73542 8.34842 7.72512 8.37774 7.72078 8.40821C7.71645 8.43868 7.71815 8.46971 7.72581 8.49952C7.73346 8.52933 7.74691 8.55735 7.76539 8.58196C7.77295 8.59206 8.5319 9.60611 9.2231 10.793C9.24441 10.8297 9.27525 10.8598 9.31232 10.8803C9.3494 10.9008 9.39134 10.9108 9.43367 10.9094C9.476 10.9079 9.51715 10.895 9.55274 10.8721C9.58833 10.8491 9.61703 10.8169 9.63579 10.7789C9.65296 10.7442 9.66125 10.7058 9.65992 10.6671C9.65858 10.6284 9.64767 10.5906 9.62816 10.5572C8.92263 9.34566 8.14796 8.31083 8.14022 8.30054C8.1029 8.25084 8.04737 8.21799 7.98584 8.20923C7.92431 8.20047 7.86181 8.2165 7.81211 8.25381ZM9.10339 8.33436C9.05368 8.37167 9.02082 8.4272 9.01205 8.48873C9.00328 8.55027 9.01931 8.61277 9.05662 8.66248C9.05991 8.66689 9.38965 9.10761 9.68964 9.62276C9.71096 9.65937 9.74179 9.68952 9.77887 9.71C9.81594 9.73048 9.85788 9.74053 9.90021 9.73908C9.94254 9.73764 9.98369 9.72474 10.0193 9.70177C10.0549 9.6788 10.0836 9.64662 10.1023 9.60864C10.1195 9.57393 10.1278 9.53551 10.1265 9.49681C10.1251 9.45811 10.1142 9.42034 10.0947 9.38689C9.78038 8.84716 9.4456 8.39984 9.43146 8.3811C9.41299 8.35649 9.38985 8.33575 9.36337 8.32008C9.33689 8.30441 9.30757 8.29411 9.27711 8.28977C9.24664 8.28543 9.21562 8.28713 9.18581 8.29478C9.15601 8.30243 9.128 8.31588 9.10339 8.33436Z" fill="#CBA7FF"/></g><defs><clipPath id="clip0_1130_76"><rect width="16" height="16" fill="white"/></clipPath></defs></svg></span>
                    <span>{hoverMission.money} <svg class="money" width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><g clip-path="url(#clip0_1_4840)"><path d="M6.85405 15.4348L1.80848 11.496C1.56935 11.3093 1.5274 10.9641 1.71476 10.7252L9.9622 0.210653C10.1493 -0.0278984 10.4944 -0.0701358 10.7331 0.116263L15.7787 4.05513C16.0179 4.24178 16.0598 4.58699 15.8724 4.8259L7.62501 15.3404C7.43787 15.579 7.09286 15.6212 6.85405 15.4348Z" fill="#57AD7B"></path><path d="M3.66841 15.7569L0.103824 10.4452C-0.0651903 10.1934 0.00235097 9.85213 0.254617 9.68321L11.3656 2.24437C11.6175 2.07568 11.9585 2.1428 12.1273 2.39436L15.6919 7.70601C15.8609 7.95785 15.7934 8.2991 15.5411 8.46802L4.43013 15.9069C4.17816 16.0756 3.83723 16.0084 3.66841 15.7569Z" fill="#81CDA1"></path><path d="M15.6918 7.70601L12.1272 2.39436C11.9584 2.1428 11.6175 2.07568 11.3655 2.24437L10.9331 2.53385L14.4041 7.70601C14.5731 7.95785 14.5056 8.2991 14.2533 8.46802L3.57471 15.6174L3.66832 15.7569C3.83715 16.0084 4.17807 16.0756 4.43005 15.9069L15.541 8.46802C15.7933 8.2991 15.8608 7.95785 15.6918 7.70601Z" fill="#60BF88"></path><path d="M10.8248 9.25356C10.9187 7.63959 9.685 6.255 8.0692 6.16098C6.4534 6.06697 5.06741 7.29913 4.9735 8.9131C4.87959 10.5271 6.11333 11.9117 7.72913 12.0057C9.34492 12.0997 10.7309 10.8675 10.8248 9.25356Z" fill="#29795D"></path><path d="M1.77511 8.66541C2.50722 9.75634 2.21542 11.2335 1.12336 11.9646L0.103356 10.4447C-0.0654323 10.1932 0.00226992 9.85234 0.2546 9.68342L1.77511 8.66541ZM2.65119 14.2413C3.74556 13.5086 5.225 13.7971 5.95556 14.8857L4.43083 15.9066C4.17853 16.0755 3.83715 16.0085 3.66836 15.757L2.65119 14.2413ZM14.0213 9.48721C13.2892 8.39628 13.581 6.91916 14.673 6.18802L15.6931 7.70792C15.8618 7.95945 15.7941 8.30028 15.5418 8.46919L14.0213 9.48721ZM13.1452 3.91135C12.0508 4.64407 10.5714 4.35552 9.84085 3.26688L11.3656 2.24606C11.6179 2.07714 11.9593 2.14411 12.128 2.3956L13.1452 3.91135Z" fill="#29795D"></path><path d="M11.575 6.85551C11.5232 6.85557 11.4727 6.83897 11.4311 6.80814C11.3894 6.77732 11.3588 6.73392 11.3437 6.68435C11.3286 6.63478 11.3299 6.58167 11.3473 6.53287C11.3647 6.48407 11.3974 6.44217 11.4405 6.41337L12.5771 5.65239C12.6035 5.63475 12.633 5.62247 12.6641 5.61625C12.6952 5.61004 12.7272 5.61001 12.7583 5.61617C12.7894 5.62233 12.819 5.63455 12.8454 5.65215C12.8718 5.66974 12.8944 5.69235 12.9121 5.7187C12.9297 5.74505 12.942 5.77462 12.9482 5.80571C12.9544 5.8368 12.9544 5.86882 12.9483 5.89992C12.9421 5.93103 12.9299 5.96062 12.9123 5.987C12.8947 6.01338 12.8721 6.03603 12.8458 6.05367L11.7091 6.81465C11.6695 6.8413 11.6228 6.85552 11.575 6.85551ZM3.08447 12.54C3.03266 12.54 2.98222 12.5234 2.94058 12.4926C2.89894 12.4618 2.86831 12.4184 2.85323 12.3688C2.83815 12.3193 2.83941 12.2662 2.85682 12.2174C2.87424 12.1686 2.90688 12.1267 2.94994 12.0979L4.08655 11.3369C4.13976 11.3018 4.20471 11.2891 4.26722 11.3017C4.32972 11.3143 4.38472 11.3511 4.42019 11.4041C4.45566 11.4571 4.46874 11.5219 4.45656 11.5845C4.44439 11.6471 4.40795 11.7023 4.3552 11.7381L3.21859 12.4991C3.17893 12.5258 3.13224 12.54 3.08447 12.54Z" fill="#29795D"></path><path d="M9.0262 10.3591C9.37434 10.0003 9.38567 9.50388 9.22728 9.15278C9.02041 8.69435 8.56446 8.48078 8.06553 8.60878C8.00552 8.62417 7.944 8.63949 7.88206 8.65407L7.33429 7.83785C7.47189 7.79316 7.57912 7.80482 7.58971 7.8061C7.65267 7.81523 7.71669 7.79911 7.76782 7.76126C7.81896 7.7234 7.85306 7.66688 7.86271 7.604C7.86756 7.57266 7.86618 7.54067 7.85866 7.50987C7.85115 7.47906 7.83763 7.45004 7.8189 7.42445C7.80016 7.39887 7.77657 7.37723 7.74947 7.36076C7.72237 7.34429 7.69229 7.33333 7.66096 7.32849C7.64425 7.32594 7.37048 7.28705 7.05901 7.42767L6.95277 7.26938C6.93511 7.24303 6.91243 7.22042 6.88603 7.20284C6.85963 7.18525 6.83002 7.17305 6.7989 7.16691C6.76778 7.16078 6.73575 7.16084 6.70465 7.16708C6.67355 7.17333 6.64399 7.18564 6.61765 7.20332C6.59131 7.221 6.56871 7.24369 6.55115 7.2701C6.53358 7.29652 6.5214 7.32613 6.51528 7.35726C6.50917 7.38838 6.50925 7.42041 6.51551 7.4515C6.52178 7.4826 6.53411 7.51216 6.55181 7.53848L6.66767 7.71117C6.64829 7.7318 6.62956 7.75303 6.61149 7.77481C6.39869 8.03329 6.35202 8.39389 6.48977 8.71585C6.61568 9.01026 6.86926 9.20606 7.1516 9.22685C7.29482 9.23738 7.45624 9.22698 7.6615 9.19208L8.36637 10.2425C8.22669 10.3183 8.1143 10.342 7.90337 10.3445C7.87167 10.3449 7.84035 10.3515 7.8112 10.364C7.78205 10.3765 7.75565 10.3946 7.73351 10.4173C7.71136 10.44 7.6939 10.4668 7.68213 10.4962C7.67035 10.5257 7.66449 10.5571 7.66488 10.5888C7.66565 10.6524 7.69141 10.713 7.73658 10.7577C7.78176 10.8023 7.84272 10.8274 7.90624 10.8274H7.90923C8.23824 10.8234 8.42825 10.7648 8.63625 10.6446L8.75527 10.822C8.77734 10.8549 8.80721 10.8819 8.84221 10.9006C8.87722 10.9192 8.91629 10.929 8.95596 10.9289C8.99971 10.9289 9.04263 10.9169 9.08013 10.8944C9.11763 10.8719 9.1483 10.8396 9.16887 10.801C9.18943 10.7623 9.19912 10.7189 9.1969 10.6752C9.19467 10.6315 9.18062 10.5892 9.15623 10.5529L9.0262 10.3591ZM7.18714 8.74531C7.08708 8.73794 6.98764 8.65185 6.93381 8.52598C6.88761 8.418 6.87152 8.26383 6.94978 8.13142L7.36066 8.74367C7.29875 8.74821 7.24026 8.74924 7.18714 8.74531ZM8.18551 9.07654C8.5829 8.97468 8.74641 9.26117 8.78714 9.35141C8.86353 9.52068 8.86923 9.75041 8.74467 9.93948L8.16848 9.08086L8.18551 9.07654Z" fill="#81CDA1"></path></g><defs><clipPath id="clip0_1_4840"><rect width="16" height="16" fill="white"></rect></clipPath></defs></svg></span>
                </div>
            </div>
        </div>
        <div class="descmis">
            <p>{hoverMission.descr}</p>
        </div>
        <div class="bottommis">
            <p>Прогресс:  <b>{hoverMission.count}</b> /{hoverMission.maxCount}</p>
            {#if hoverMission.status == statusData.selected && hoverMission.isDone}
            <span>Зажмите <b>ЛКМ</b>, чтобы выбрать награды</span>
            {:else if hoverMission.status == statusData.selected && !hoverMission.isDone}
            <span>В процессе выполнения</span>
            {:else if hoverMission.status == statusData.closed && isActiveBox (hoverMission.i)}
            <span>Зажмите <b>ЛКМ</b>, чтобы выбрать задание</span>
            {:else if hoverMission.status == statusData.closed && !isActiveBox (hoverMission.i)}
            <span>Пройдите прошлые задания</span>
            {:else if hoverMission.status == statusData.done}
            <span>Вы выполнили это занаие</span>
            {/if}
        </div>
        <div class="bar">
            <div class="barprog" style="width: {hoverMission.count / hoverMission.maxCount * 100}%"></div>
        </div>
    </div>
{/if}


<div id="battlepass1" class="battlepass3">
    <div class="questline1"></div>
    <div class="questline2"></div>
    <div class="questline3"></div>
    <div class="questline4"></div>
    <div class="questline5"></div>
    <div class="questline6"></div>
    <div class="questline7"></div>
    <div class="questline8"></div>
    <div class="questline9"></div>
    <div class="questline10"></div>
    <div class="questline11"></div>
    <div class="questline12"></div>
    <div class="questline13"></div>
    <div class="questline14"></div>
    <div class="questline15"></div>
    <div class="questline16"></div>
    <div class="questline17"></div>
    {#each missionsArray as mission, index}
        <div class="questblock pos{index+1}" class:closed={mission.status === statusData.closed && !isActiveBox (index)} class:active={mission.status === statusData.closed && isActiveBox (index)} class:done={mission.status === statusData.done} class:selected={mission.status === statusData.selected}>
            <svg class="done" width="17" height="15" viewBox="0 0 17 15" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M5.07483 15C4.62438 14.9999 4.19241 14.8247 3.87394 14.513L0.476808 11.1887C0.1674 10.8752 -0.00380584 10.4553 6.42103e-05 10.0195C0.00393426 9.58372 0.182572 9.16683 0.4975 8.85866C0.812429 8.55048 1.23845 8.37568 1.68381 8.37189C2.12917 8.3681 2.55823 8.53564 2.87859 8.83841L4.97886 10.8936L13.9762 0.622458C14.1186 0.448636 14.2953 0.304602 14.4957 0.198918C14.6961 0.0932342 14.9162 0.028057 15.1429 0.00725867C15.3695 -0.0135396 15.5982 0.0104682 15.8152 0.0778513C16.0322 0.145234 16.2331 0.254618 16.4061 0.399505C16.579 0.544391 16.7204 0.721821 16.8219 0.92125C16.9234 1.12068 16.9829 1.33804 16.9968 1.5604C17.0107 1.78277 16.9789 2.00561 16.903 2.21568C16.8272 2.42575 16.709 2.61876 16.5555 2.78324L6.36405 14.4182C6.20604 14.6019 6.0086 14.7493 5.78577 14.8498C5.56294 14.9504 5.32021 15.0016 5.07483 15Z" fill="white"/>
            </svg>
            <svg class="active" width="3" height="15" viewBox="0 0 3 15" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M0.434938 8.48396L0.12364 3.74156C0.0627849 2.78736 -0.256705 1.24513 0.458344 0.469837C1.00253 -0.12654 2.29102 -0.229116 2.6819 0.597463C3.01428 1.56876 3.08778 2.61257 2.89489 3.62229L2.47827 8.50423C2.46036 8.96372 2.36166 9.41619 2.18687 9.84012C2.11806 9.9773 2.01375 10.0927 1.88522 10.1738C1.75669 10.2549 1.60885 10.2986 1.45769 10.3002C1.30654 10.3018 1.15784 10.2612 1.02769 10.1828C0.897537 10.1045 0.790898 9.99132 0.719319 9.85563C0.554615 9.41641 0.458651 8.95354 0.434938 8.48396ZM1.51278 15C1.14373 14.9979 0.78934 14.8525 0.521713 14.5935C0.254085 14.3345 0.0933298 13.9814 0.0721566 13.6058C0.0509834 13.2303 0.170983 12.8607 0.407739 12.5722C0.644495 12.2836 0.980217 12.0979 1.3466 12.0527C1.54343 12.0284 1.74305 12.0455 1.93315 12.1029C2.12325 12.1603 2.2998 12.2568 2.45189 12.3864C2.60398 12.5161 2.72839 12.6761 2.81745 12.8566C2.90651 13.0372 2.95833 13.2344 2.9697 13.4361C2.98108 13.6379 2.95177 13.8398 2.88359 14.0296C2.81541 14.2193 2.7098 14.3928 2.57328 14.5394C2.43676 14.6859 2.27223 14.8024 2.08983 14.8816C1.90743 14.9608 1.71104 15.0011 1.51278 15Z" fill="white"/>
            </svg>
            <svg class="selected" width="3" height="15" viewBox="0 0 3 15" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M0.434938 8.48396L0.12364 3.74156C0.0627849 2.78736 -0.256705 1.24513 0.458344 0.469837C1.00253 -0.12654 2.29102 -0.229116 2.6819 0.597463C3.01428 1.56876 3.08778 2.61257 2.89489 3.62229L2.47827 8.50423C2.46036 8.96372 2.36166 9.41619 2.18687 9.84012C2.11806 9.9773 2.01375 10.0927 1.88522 10.1738C1.75669 10.2549 1.60885 10.2986 1.45769 10.3002C1.30654 10.3018 1.15784 10.2612 1.02769 10.1828C0.897537 10.1045 0.790898 9.99132 0.719319 9.85563C0.554615 9.41641 0.458651 8.95354 0.434938 8.48396ZM1.51278 15C1.14373 14.9979 0.78934 14.8525 0.521713 14.5935C0.254085 14.3345 0.0933298 13.9814 0.0721566 13.6058C0.0509834 13.2303 0.170983 12.8607 0.407739 12.5722C0.644495 12.2836 0.980217 12.0979 1.3466 12.0527C1.54343 12.0284 1.74305 12.0455 1.93315 12.1029C2.12325 12.1603 2.2998 12.2568 2.45189 12.3864C2.60398 12.5161 2.72839 12.6761 2.81745 12.8566C2.90651 13.0372 2.95833 13.2344 2.9697 13.4361C2.98108 13.6379 2.95177 13.8398 2.88359 14.0296C2.81541 14.2193 2.7098 14.3928 2.57328 14.5394C2.43676 14.6859 2.27223 14.8024 2.08983 14.8816C1.90743 14.9608 1.71104 15.0011 1.51278 15Z" fill="white"/>
            </svg>
            <svg class="closed" width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M1 1L8.5 8.5M16 16L8.5 8.5M8.5 8.5L16 1L1 16" stroke="white" stroke-opacity="0.25" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>      
            {#if (mission.status === statusData.closed && isActiveBox (index)) || (hoverMission.status == statusData.selected && hoverMission.isDone)}
                <div class="battlepass__missions_element_progress" id="progressBarKey{index}" />
                <div class="battlepass__missions_element_info"
                     on:mousedown={() => CreateProgressBar (index, mission.id)}
                     on:mouseenter={(e) => handleFavoriteSlotMouseEnter (e, index)}
                     on:mouseleave={handleFavoriteSlotMouseLeave} />
            {:else}
                <div class="battlepass__missions_element_info"
                     on:mouseenter={(e) => handleFavoriteSlotMouseEnter (e, index)}
                     on:mouseleave={handleFavoriteSlotMouseLeave} />
            {/if}
        </div>
    {/each}
</div>