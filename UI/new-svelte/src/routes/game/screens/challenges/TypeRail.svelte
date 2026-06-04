<div class="rail">
	<button class="overview-button" class:active={selected === 'all'} onclick={() => onSelect('all')}>
		<div class="overview-mark">
			<span class="overview-diamond"></span>
		</div>
		<span class="overview-label" class:active={selected === 'all'}>Overview</span>
	</button>
	{#each groups as group (group.typeId)}
		<RailButton {group} active={selected === group.typeId} onClick={() => onSelect(group.typeId)} />
	{/each}
</div>

<script lang="ts">
import type { EChallengeType } from '$lib/api';
import RailButton from './RailButton.svelte';
import type { TypeGroup } from './challenges-view.svelte';

interface Props {
	groups: TypeGroup[];
	selected: EChallengeType | 'all';
	onSelect: (type: EChallengeType | 'all') => void;
}

const { groups, selected, onSelect }: Props = $props();
</script>

<style lang="scss">
.rail {
	width: 232px;
	flex-shrink: 0;
	border-right: 1px solid var(--border-subtle);
	padding-right: 18px;
	overflow-y: auto;
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.overview-button {
	display: flex;
	align-items: center;
	gap: 11px;
	width: 100%;
	text-align: left;
	cursor: pointer;
	padding: 8px 10px;
	border-radius: 3px;
	border: 1px solid transparent;
	background: transparent;
	margin-bottom: 5px;
	transition: all 120ms;

	&.active {
		border-color: var(--border-light);
		background: rgba(255, 255, 255, 0.06);
	}
}

.overview-mark {
	width: 30px;
	height: 30px;
	flex-shrink: 0;
	border-radius: 50%;
	display: flex;
	align-items: center;
	justify-content: center;
	background: rgba(255, 255, 255, 0.05);
	border: 1px solid var(--border-light);
}

.overview-diamond {
	width: 7px;
	height: 7px;
	transform: rotate(45deg);
	background: var(--accent);
}

.overview-label {
	flex: 1;
	font-size: 13px;
	color: var(--text-tertiary);

	&.active {
		color: var(--text-primary);
	}
}
</style>
