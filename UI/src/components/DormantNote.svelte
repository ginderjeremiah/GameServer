<!-- Shared "⊘ dormant" signal: a read-only, muted note that a build committed points into
     something inert (an off-weapon skill, an unfielded amplifier attribute). Presentational
     only — callers own the hint text and any layout spacing around it. -->
<span
	class="dormant-note"
	class:block
	{title}
	style:font-size="{fontSize}px"
	style:letter-spacing="{letterSpacing}px"
	style:margin-top="{marginTop}px"
	style:margin-left="{marginLeft}px">⊘ {text}</span
>

<script lang="ts">
type Props = {
	text: string;
	title?: string;
	fontSize?: number;
	letterSpacing?: number;
	/** Renders as a block below the preceding content instead of inline. */
	block?: boolean;
	marginTop?: number;
	marginLeft?: number;
};

const {
	text,
	title,
	fontSize = 8.5,
	letterSpacing = 0.5,
	block = false,
	marginTop = 0,
	marginLeft = 0
}: Props = $props();
</script>

<style lang="scss">
// Neutral muted tone — --warning is reserved for validation, and this is a read-only signal, not a problem to fix.
.dormant-note {
	font-family: var(--mono);
	text-transform: uppercase;
	color: var(--text-tertiary);

	&.block {
		display: block;
	}
}
</style>
