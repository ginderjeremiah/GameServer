{#if visible}
	<svg width="16" height="16" viewBox="0 0 20 20" fill="none">
		<path d="M2 10s3-5 8-5 8 5 8 5-3 5-8 5-8-5-8-5z" stroke="var(--text-secondary)" stroke-width="1.4" />
		<circle cx="10" cy="10" r="2.5" stroke="var(--text-secondary)" stroke-width="1.4" />
	</svg>
{:else}
	<svg width="16" height="16" viewBox="0 0 20 20" fill="none">
		<path
			d="M2 10s3-5 8-5c2 0 3.7.8 5 1.8M18 10s-3 5-8 5c-2 0-3.7-.8-5-1.8"
			stroke="var(--text-secondary)"
			stroke-width="1.4"
		/>
		<path d="M3 3l14 14" stroke="var(--text-secondary)" stroke-width="1.4" stroke-linecap="round" />
	</svg>
{/if}

<script lang="ts">
/** Shows the open-eye glyph when the password is visible, otherwise the struck-through eye. */
let { visible = false }: { visible?: boolean } = $props();
</script>
