<div class="page-base {colorScheme}">
	{@render children()}
	<TooltipBase />
</div>

<script lang="ts">
import { page } from '$app/stores';
import { routeTo } from '$lib/common';
import { TooltipBase } from '$components';
import { playerManager } from '$lib/engine';
import '$styles/common.scss';

let { children } = $props();

let colorScheme = $state('default');

$effect(() => {
	if (!playerManager.name && $page.url.pathname !== '/') {
		routeTo('/');
	}
});
</script>

<style lang="scss">
@use '../styles/colors.scss';

.page-base {
	height: 100vh;
	user-select: none;

	&.default {
		background: colors.$dark-light-gray-grad;
		--border-radius: 5px;
		--default-glow-color: #{colors.$blue};
		--btn-background: #{colors.$light-blue};
		--spinner-color: gray;
		--overlay-color: #{colors.$light-gray};
		--title-color: #{colors.$off-white};
		--default-shadow: 0 0 0.25rem #{colors.$black};
		--default-border: 1px solid #{colors.$black};
		--error-color: #{colors.$red};
		--nav-bar-color: #{colors.$gray-blue};
		--nav-bar-hover-color: #{colors.$gray-blue-light};
		--nav-bar-active-color: #{colors.$gray-blue-dark};
		--health-missing-color: #{colors.$red};
		--health-remaining-color: #{colors.$green};
		--health-disappearing-color: #{colors.$red-orange};
		--container-background-color: #{colors.$off-white};
		--slot-background-color: #{colors.$white};
		--slot-highlight-color: #{colors.$red};
	}
}
</style>
