<script>
    // Коннектор RJ45 (SVG из мини-игры Farko)
    export let scale = 1;
    export let uid = "c";
</script>

<svg width={44 * scale} height={56 * scale} viewBox="0 0 44 56" xmlns="http://www.w3.org/2000/svg">
    <defs>
        <linearGradient id="bodyGrad-{uid}" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stop-color="#888" />
            <stop offset="40%" stop-color="#666" />
            <stop offset="100%" stop-color="#3a3a3a" />
        </linearGradient>
        <linearGradient id="pinGrad-{uid}" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#d4a000" />
            <stop offset="50%" stop-color="#f0c030" />
            <stop offset="100%" stop-color="#a07000" />
        </linearGradient>
    </defs>
    <rect x="4" y="12" width="36" height="40" rx="3" ry="3" fill="url(#bodyGrad-{uid})" stroke="#222" stroke-width="1" />
    <rect x="6" y="8" width="32" height="18" rx="2" ry="2" fill="#555" stroke="#222" stroke-width="1" />
    {#each [8, 14, 20, 26, 32] as x}
        <rect x={x} y="9" width="4" height="12" rx="1" fill="url(#pinGrad-{uid})" stroke="#8a6000" stroke-width="0.5" />
    {/each}
    {#each [20, 26, 32] as y}
        <rect x="4" y={y} width="4" height="3" rx="1" fill="#777" opacity="0.7" />
        <rect x="36" y={y} width="4" height="3" rx="1" fill="#777" opacity="0.7" />
    {/each}
    <rect x="12" y="8" width="20" height="4" rx="2" fill="#444" stroke="#222" stroke-width="0.5" />
    <rect x="15" y="6" width="14" height="4" rx="2" fill="#555" stroke="#333" stroke-width="0.5" />
    <rect x="14" y="50" width="16" height="6" rx="2" fill="#333" stroke="#222" stroke-width="0.5" />
    <rect x="6" y="12" width="32" height="2" rx="1" fill="rgba(255,255,255,0.12)" />
</svg>
