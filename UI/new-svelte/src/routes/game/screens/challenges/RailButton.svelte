<button
	class="rail-button"
	class:active
	style:border="1px solid {active ? tintColor(group.accent, 0.5) : 'transparent'}"
	style:background={active ? tintColor(group.accent, 0.12) : 'transparent'}
	style:box-shadow={active ? `inset 2px 0 0 ${group.accent}` : 'none'}
	onclick={onClick}
>
	<RingMeter {pct} accent={group.accent} size={30} stroke={3} done={complete}>
		<TypeGlyph typeId={group.typeId} color={complete ? 'var(--success)' : group.accent} size={13} />
	</RingMeter>
	<div class="rail-text">
		<div class="rail-label" class:active>{group.label}</div>
		<div class="rail-unit">{unit}</div>
	</div>
	<span class="rail-count" class:complete>{stats.done}/{stats.total}</span>
</button>

<script lang="ts">
import { tintColor } from '$lib/common';
import { challengeTypeUnit } from './challenge-meta';
import { typeStats, type TypeGroup } from './challenges-view.svelte';
import RingMeter from './RingMeter.svelte';
import TypeGlyph from './TypeGlyph.svelte';

interface Props {
	group: TypeGroup;
	active: boolean;
	onClick: () => void;
}

const { group, active, onClick }: Props = $props();

const stats = $derived(typeStats(group.items));
const pct = $derived((stats.done / Math.max(1, stats.total)) * 100);
const complete = $derived(stats.done === stats.total);
const unit = $derived(challengeTypeUnit(group.typeId));
</script>

<style lang="scss">
.rail-button {
	display: flex;
	align-items: center;
	gap: 11px;
	width: 100%;
	text-align: left;
	cursor: pointer;
	padding: 8px 10px;
	border-radius: 3px;
	transition: all 120ms;
}

.rail-text {
	flex: 1;
	min-width: 0;
}

.rail-label {
	font-size: 13px;
	color: var(--text-tertiary);
	letter-spacing: -0.1px;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.active {
		color: var(--text-primary);
	}
}

.rail-unit {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.rail-count {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-tertiary);
	flex-shrink: 0;

	&.complete {
		color: var(--success);
	}
}
</style>
