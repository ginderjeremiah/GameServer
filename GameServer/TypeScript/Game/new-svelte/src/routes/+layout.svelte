<div class="page-base {colorScheme}">
	{@render children()}
	<TooltipBase />
</div>

<script lang="ts">
import { player } from '$stores';
import { page } from '$app/stores';
import { routeTo } from '$lib/common';
import TooltipBase from '$components/TooltipBase.svelte';
import '$styles/common.scss';

let { children } = $props();

let colorScheme = $state('default');

$effect(() => {
	if (!player.data && $page.url.pathname !== '/') {
		routeTo('/');
	}
});
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
		--title-color: #{$off-white};
		--default-shadow: 0 0 0.25rem #{$black};
		--default-border: 1px solid #{$black};
		--error-color: #{$red};
		--nav-bar-color: #{$gray-blue};
		--health-missing-color: #{$red};
		--health-remaining-color: #{$green};
		--health-disappearing-color: #{$red-orange};
		--container-background-color: #{$off-white};
		--slot-background-color: #{$white};
	}
}
</style>
