<span class="warn-tri" {title}>
	<WorkbenchIcon kind="warn" {size} sw={1.6} stroke="var(--warning)" />
</span>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';

interface Props {
	size?: number;
	title?: string;
}

const { size = 13, title }: Props = $props();
</script>

<style lang="scss">
.warn-tri {
	display: inline-flex;
	align-items: center;
}
</style>
