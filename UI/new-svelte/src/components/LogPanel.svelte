<div class="log-panel" data-testid="log-panel">
	<div class="log-header">
		<span class="log-title">Combat Log</span>
		<div class="log-divider"></div>
	</div>
	<div class="log-entries">
		{#each visibleLogs as log, i (log.id)}
			<div class="log-entry" style:opacity={1 - i * 0.13}>
				<span class="log-time">{formatTime(log.id)}</span>
				<span class="log-text" class:hit={log.logType === ELogType.Damage}
					class:enemy={log.logType === ELogType.EnemyDefeated}
					class:loot={log.logType === ELogType.ItemFound}
					class:exp={log.logType === ELogType.Exp || log.logType === ELogType.LevelUp}
				>{log.message}</span>
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import { ELogType } from '$lib/api';
import { logs } from '$stores';

const allLogs = $derived(logs());
const visibleLogs = $derived(allLogs.slice(-8).reverse());

const formatTime = (id: number) => {
	const s = Math.floor(id / 60);
	const m = Math.floor(s / 60);
	return `${String(m % 100).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
};
</script>

<style lang="scss">
.log-panel {
	border-top: 1px solid var(--border-subtle);
	background: rgba(0, 0, 0, 0.25);
	padding: 10px 24px;
	height: 130px;
	overflow: hidden;
	position: relative;
}

.log-header {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 6px;
}

.log-title {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: rgba(192, 216, 255, 0.7);
}

.log-divider {
	flex: 1;
	height: 1px;
	background: rgba(240, 240, 240, 0.07);
}

.log-entries {
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	line-height: 1.55;
}

.log-entry {
	display: flex;
	gap: 10px;
}

.log-time {
	color: rgba(240, 240, 240, 0.4);
	min-width: 36px;
}

.log-text {
	color: rgba(240, 240, 240, 0.65);

	&.hit {
		color: #c0d8ff;
	}

	&.enemy {
		color: #e8b6a6;
	}

	&.loot {
		color: #bde0b4;
	}

	&.exp {
		color: #f0d28a;
	}
}
</style>
