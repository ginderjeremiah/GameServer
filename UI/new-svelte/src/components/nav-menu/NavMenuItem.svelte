<button class="hover-glow" onclick={(ev) => onClick(ev, props)}>{text}</button>

<script lang="ts">
import type { INavMenuItem } from './types';

const props: INavMenuItem = $props();
const { text, onClick } = props;
</script>

<style lang="scss">
button {
	font-size: 1.5rem;
	background-color: transparent;
	outline: none;
	border: none;
	color: var(--title-color);
	text-shadow: var(--default-shadow);
	margin: 0.1rem 0.5rem;
}
</style>
