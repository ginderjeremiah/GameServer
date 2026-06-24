<!--
	Renders a proficiency "word of power" in the decorative Aetheric conlang.

	The glyphs are flavour, not readable copy, so accessibility is handled by
	keeping the romanization as the real DOM text:
	  • No `label`  — the text content IS the romanization, which a screen reader
	    reads naturally and the browser can still find/select. Only the visual
	    glyphs are alien.
	  • With `label` — pass the human-readable proficiency name; the glyph text is
	    hidden from assistive tech (`aria-hidden`) and the element announces as an
	    image labelled with that name instead.
	Either way the DOM text falls back to legible Latin if the font fails to load.
-->
<span
	class="word-of-power {klass}"
	class:glow
	role={label ? 'img' : undefined}
	aria-label={label}
	title={title ?? label ?? text}
	style:font-size={fontSize}
>
	{#if label}
		<span aria-hidden="true">{text}</span>
	{:else}
		{text}
	{/if}
</span>

<script lang="ts">
type Props = {
	/** The romanized word of power, e.g. "inferno". Rendered as conlang glyphs. */
	text: string;
	/** Human-readable label for assistive tech; when set, the glyphs are aria-hidden. */
	label?: string;
	/** Font size — a number is treated as px, a string is passed through (e.g. "1.5rem"). */
	size?: number | string;
	/** Adds a soft arcane glow derived from the current text colour. */
	glow?: boolean;
	/** Hover title; defaults to the label, then the romanization. */
	title?: string;
	class?: string;
};

let { text, label, size, glow = false, title, class: klass = '' }: Props = $props();

let fontSize = $derived(typeof size === 'number' ? `${size}px` : size);
</script>

<style lang="scss">
.word-of-power {
	// Decorative conlang face; colour is inherited so the theme/consumer controls it.
	// Fallback chain keeps the romanization legible unconditionally: if --conlang is
	// absent (component rendered outside the layout) we still try the Aetheric face,
	// then degrade to --sans rather than the browser default serif.
	font-family: var(--conlang, 'Aetheric', var(--sans));
	letter-spacing: 0.04em;
	line-height: 1.1;
	display: inline-block;

	&.glow {
		text-shadow: 0 0 0.5em color-mix(in srgb, currentColor 55%, transparent);
	}
}
</style>
