<div class="tt-description">{text}</div>

<script lang="ts">
interface Props {
	/** The flavour/description text rendered as the shared italic tooltip body. */
	text: string;
}

const { text }: Props = $props();
</script>

<style lang="scss">
.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}
</style>
