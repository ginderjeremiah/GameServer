<span
	class="mask-bar"
	style:width="{width}px"
	style:height="{height}px"
	style:background="repeating-linear-gradient(45deg, {tintColor(accent, 0.22)}, {tintColor(accent, 0.22)} 3px, {tintColor(
		accent,
		0.07
	)} 3px, {tintColor(accent, 0.07)} 6px)"
></span>

<script lang="ts">
import { tintColor } from '$lib/common';

interface Props {
	accent: string;
	width?: number;
	height?: number;
}

const { accent, width = 60, height = 9 }: Props = $props();
</script>

<style lang="scss">
.mask-bar {
	display: inline-block;
	border-radius: 2px;
	vertical-align: middle;
}
</style>
