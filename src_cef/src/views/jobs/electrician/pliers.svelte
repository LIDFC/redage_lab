<script>
    // Клещи (SVG из мини-игры Farko)
    export let width = 56;
    export let height = 84;
</script>

<svg width={width} height={height} viewBox="0 0 56 84" xmlns="http://www.w3.org/2000/svg">
    <defs>
        <linearGradient id="pg-handle" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stop-color="#b03020" />
            <stop offset="38%" stop-color="#e74c3c" />
            <stop offset="100%" stop-color="#8b1a0e" />
        </linearGradient>
        <linearGradient id="pg-metal" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stop-color="#8a9ba8" />
            <stop offset="32%" stop-color="#d6e4ed" />
            <stop offset="62%" stop-color="#b0c4d0" />
            <stop offset="100%" stop-color="#607888" />
        </linearGradient>
        <linearGradient id="pg-bolt" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#5a6a74" />
            <stop offset="100%" stop-color="#2a3840" />
        </linearGradient>
    </defs>
    <path d="M7,48 L11,9 L19,9 L17,48 Z" fill="url(#pg-metal)" stroke="#4a6070" stroke-width="0.6" />
    <path d="M5,46 Q1,60 3,72 Q5,83 13,83 Q21,82 21,72 Q22,60 19,48 Q18,46 17,46 L7,46 Q5,46 5,46 Z" fill="url(#pg-handle)" stroke="#7b241c" stroke-width="1" />
    {#each [53, 59, 65, 71] as y}
        <line x1="5" y1={y} x2="20" y2={y} stroke="rgba(0,0,0,0.28)" stroke-width="2" stroke-linecap="round" />
        <line x1="51" y1={y} x2="36" y2={y} stroke="rgba(0,0,0,0.28)" stroke-width="2" stroke-linecap="round" />
    {/each}
    <path d="M49,48 L45,9 L37,9 L39,48 Z" fill="url(#pg-metal)" stroke="#4a6070" stroke-width="0.6" />
    <path d="M51,46 Q55,60 53,72 Q51,83 43,83 Q35,82 35,72 Q34,60 37,48 Q38,46 39,46 L49,46 Q51,46 51,46 Z" fill="url(#pg-handle)" stroke="#7b241c" stroke-width="1" />
    <rect x="14" y="46" width="28" height="5" rx="2.5" fill="url(#pg-bolt)" stroke="#1e2a30" stroke-width="0.8" />
    <circle cx="14" cy="48" r="6" fill="url(#pg-bolt)" stroke="#1e2a30" stroke-width="1.2" />
    <circle cx="14" cy="48" r="3" fill="#8a9ba8" />
    <circle cx="42" cy="48" r="6" fill="url(#pg-bolt)" stroke="#1e2a30" stroke-width="1.2" />
    <circle cx="42" cy="48" r="3" fill="#8a9ba8" />
    <path d="M21,58 L23,54 L25,62 L27,54 L29,62 L31,54 L33,62 L35,54 L37,58" fill="none" stroke="#7a8e9a" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
    <path d="M11,9 L19,9 L15,1 Z" fill="url(#pg-metal)" stroke="#4a6070" stroke-width="0.6" />
    <path d="M45,9 L37,9 L41,1 Z" fill="url(#pg-metal)" stroke="#4a6070" stroke-width="0.6" />
</svg>
