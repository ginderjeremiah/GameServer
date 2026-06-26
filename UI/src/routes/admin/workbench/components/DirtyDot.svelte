<!-- The amber unsaved-change indicator. Pairs the decorative dot (`.dirty-dot`, styled in workbench.scss)
     with visually-hidden text, so the change state isn't conveyed by colour/shape alone. -->
<span class="dirty-dot" aria-hidden="true"></span>
<span class="sr-only">Unsaved change</span>

<style lang="scss">
.sr-only {
	position: absolute;
	width: 1px;
	height: 1px;
	margin: -1px;
	padding: 0;
	overflow: hidden;
	clip: rect(0, 0, 0, 0);
	white-space: nowrap;
	border: 0;
}
</style>
