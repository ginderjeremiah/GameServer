<!-- Test stub for NavSidebar: renders one button per visible screen (driving `handleNavigate`) plus a
     pinned-toggle button that exercises the real `bind:pinned` two-way binding, and surfaces `active`
     as a data attribute — all without the real sidebar's chrome/animation. -->
<div data-testid="nav-sidebar" data-active={active}>
	<button data-testid="nav-toggle-pinned" onclick={() => (pinned = !pinned)}>Toggle pinned</button>
	{#each screens as screenDef (screenDef.key)}
		<button data-testid={`nav-${screenDef.key}`} onclick={() => onNavigate(screenDef.key)}>
			{screenDef.label}
		</button>
	{/each}
</div>

<script lang="ts">
interface ScreenDef {
	key: string;
	label: string;
}

let {
	screens,
	active,
	onNavigate,
	pinned = $bindable(false)
}: { screens: ScreenDef[]; active: string; onNavigate: (key: string) => void; pinned?: boolean } = $props();
</script>
