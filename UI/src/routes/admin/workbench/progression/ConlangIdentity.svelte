<div class="row gap16">
	<ProgInput
		label="Name"
		value={tier.name}
		grow
		warn={!tier.name.trim()}
		maxLength={50}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.name = v))}
	/>
	<ProgInput
		label="Icon path"
		value={tier.iconPath}
		mono
		warn={!tier.iconPath.trim()}
		maxLength={50}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.iconPath = v))}
	/>
	<div class="ro">
		<span class="lbl">Path</span>
		<div class="ro-box">{store.selectedPath?.name ?? '—'}</div>
	</div>
	<div class="ro narrow">
		<span class="lbl">Tier</span>
		<div class="ro-box mono center">{tier.pathOrdinal}</div>
	</div>
</div>

<div class="wop">
	<div class="wop-head">
		<span class="wop-title">Words of Power</span>
		<span class="wop-sub">— the conlang glyphs deciphered as the tier levels</span>
	</div>
	<div class="row gap12">
		<ProgInput
			label="Romanized word"
			value={tier.word}
			mono
			fullWidth
			warn={!tier.word.trim()}
			maxLength={50}
			onChange={(v) => store.patchProf(tier.id, (d) => (d.word = v))}
		/>
		<ProgInput
			label="Pronunciation"
			value={tier.pronunciation}
			fullWidth
			warn={!tier.pronunciation.trim()}
			maxLength={50}
			onChange={(v) => store.patchProf(tier.id, (d) => (d.pronunciation = v))}
		/>
		<ProgInput
			label="Translation"
			value={tier.translation}
			fullWidth
			warn={!tier.translation.trim()}
			maxLength={100}
			onChange={(v) => store.patchProf(tier.id, (d) => (d.translation = v))}
		/>
	</div>

	<div class="preview-label">Decipher preview <span class="muted">— how the player sees it as they level</span></div>
	<div class="preview">
		<div class="pv-step">
			<div class="pv-glyph">
				{#if tier.word}<WordOfPower text={tier.word} label={tier.name} />{:else}▯▯▯{/if}
			</div>
			<div class="pv-cap">lv 0 · undeciphered</div>
		</div>
		<span class="arrow">→</span>
		<div class="pv-step">
			<div class="pv-text">{tier.pronunciation || '—'}</div>
			<div class="pv-cap">lv {thresholds.pronunciation} · pronunciation</div>
		</div>
		<span class="arrow">→</span>
		<div class="pv-step final">
			<div class="pv-text strong">{tier.translation || '—'}</div>
			<div class="pv-cap">lv {thresholds.translation} · translated</div>
		</div>
	</div>
</div>

<div class="notes">
	<ProgInput
		label="Description"
		value={tier.description}
		textarea
		fullWidth
		maxLength={500}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.description = v))}
	/>
</div>

<div class="notes">
	<ProgInput
		label="Designer notes"
		value={tier.designerNotes}
		textarea
		fullWidth
		maxLength={2000}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.designerNotes = v))}
	/>
</div>

<script lang="ts">
import WordOfPower from '$components/WordOfPower.svelte';
import type { ProgressionStore } from './progression-store.svelte';
import { decipherThresholds } from './progression-helpers';
import type { WorkbenchProficiency } from './types';
import ProgInput from './ProgInput.svelte';

interface Props {
	store: ProgressionStore;
	tier: WorkbenchProficiency;
}

const { store, tier }: Props = $props();

const thresholds = $derived(decipherThresholds(tier.maxLevel));
</script>

<style lang="scss">
.row {
	display: flex;
}
.gap16 {
	gap: 16px;
}
.gap12 {
	gap: 12px;
}
.notes {
	margin-top: 18px;
}
.ro {
	display: flex;
	flex-direction: column;
	gap: 7px;
	width: 160px;

	&.narrow {
		width: 70px;
	}
}
.lbl {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
}
.ro-box {
	background: color-mix(in srgb, var(--white) 2%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 3px;
	color: var(--text-tertiary);
	font-size: 13.5px;
	padding: 8px 11px;

	&.mono {
		font-family: var(--mono);
	}
	&.center {
		text-align: center;
	}
}
.wop {
	border: 1px solid var(--border-light);
	border-radius: 6px;
	padding: 16px 18px;
	background: var(--panel);
	margin-top: 18px;
}
.wop-head {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 14px;
}
.wop-title {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-primary);
}
.wop-sub {
	font-size: 12px;
	color: var(--text-muted);
}
.preview-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin: 16px 0 9px;

	.muted {
		text-transform: none;
		letter-spacing: 0;
	}
}
.preview {
	display: flex;
	align-items: center;
	gap: 8px;
}
.pv-step {
	flex: 1;
	border: 1px solid var(--border-light);
	border-radius: 4px;
	padding: 10px;
	text-align: center;
	background: var(--page);

	&.final {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 6%, transparent);
	}
}
.pv-glyph {
	font-family: var(--mono);
	color: var(--text-muted);
	letter-spacing: 3px;
}
.pv-text {
	font-size: 13px;
	color: var(--text-secondary);

	&.strong {
		font-weight: 500;
		color: var(--text-primary);
	}
}
.pv-cap {
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-tertiary);
	margin-top: 4px;
}
.arrow {
	color: var(--accent);
}
</style>
