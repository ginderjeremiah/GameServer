<div class="page-base {colorScheme}">
	<slot></slot>
	<TooltipBase />
</div>

<script lang="ts">
import { player } from '$stores';
import { onDestroy } from 'svelte';
import { page } from '$app/stores';
import { routeTo } from '$lib/common';
import TooltipBase from '$components/TooltipBase.svelte';
import '$styles/common.scss';

let colorScheme = 'default';

const unsubscribe = player.subscribe((val) => {
	if (!val && $page.url.pathname !== '/') {
		routeTo('/');
	}
});

onDestroy(unsubscribe);
</script>

<style lang="scss">
@import '../styles/colors.scss';

.page-base {
	height: 100vh;

	&.default {
		background: $dark-light-gray-grad;
		--border-radius: 5px;
		--default-glow-color: #{$blue};
		--btn-background: #{$light-blue};
		--spinner-color: gray;
		--overlay-color: #{$light-gray};
		--default-title-color: #{$off-white};
		--default-shadow: 0 0 0.25rem #{$black};
		--default-border: 1px solid #{$black};
		--error-color: #{$red};
		--nav-bar-color: #{$gray-blue};
		--health-missing-color: #{$red};
		--health-remaining-color: #{$green};
		--health-disappearing-color: #{$red-orange};
	}
}
</style>
