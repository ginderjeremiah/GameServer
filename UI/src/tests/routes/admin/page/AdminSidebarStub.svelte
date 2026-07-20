<!-- Test stub for AdminSidebar: renders a nav button per tool (invoking onNavigate) so tests can drive
     tool switches, plus the bindable `pinned` prop the real page wires up. Renders no buttons when
     `tools` is empty (the reference-load suite's setup), so it's inert there. -->
<div data-testid="admin-sidebar-stub" data-pinned={pinned} data-active={active}>
	{#each tools as tool (tool.key)}
		<button type="button" data-testid={`nav-${tool.key}`} onclick={() => onNavigate(tool.key)}>
			{tool.label}
		</button>
	{/each}
</div>

<script lang="ts">
let { tools = [], active = '', onNavigate = () => {}, pinned = $bindable(false) } = $props();
</script>
