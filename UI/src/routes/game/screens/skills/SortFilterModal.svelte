<svelte:window onkeydown={onKeydown} />

{#if view.modalOpen}
	<div class="modal-layer">
		<button type="button" class="backdrop" aria-label="Close" onclick={() => (view.modalOpen = false)}></button>
		<div class="modal" role="dialog" aria-label="Sort and filter skills">
			<div class="msub">Library</div>
			<h3>Sort &amp; Filter</h3>

			<div class="mgroup">
				<div class="gl">Sort by</div>
				<div class="opts">
					{#each SKILL_SORTS as option (option.key)}
						<button
							type="button"
							class="opt"
							class:on={view.sort === option.key}
							onclick={() => view.setSort(option.key)}
						>
							{option.label}
						</button>
					{/each}
				</div>
			</div>

			{#if view.usedAttributes.length}
				<div class="mgroup">
					<div class="gl">Filter by attribute</div>
					<div class="opts">
						{#each view.usedAttributes as attr (attr)}
							<button
								type="button"
								class="opt attr"
								class:on={view.filterAttributes.includes(attr)}
								style:--ac={attributeColor(attr)}
								onclick={() => view.toggleAttributeFilter(attr)}
							>
								{attributeName(attr)}
							</button>
						{/each}
					</div>
				</div>
			{/if}

			<div class="mgroup">
				<div class="mrow">
					<span class="ml">Show locked skills</span>
					<button
						type="button"
						class="switch"
						class:on={view.showLocked}
						role="switch"
						aria-checked={view.showLocked}
						aria-label="Show locked skills"
						onclick={() => view.toggleShowLocked()}
					></button>
				</div>
			</div>

			<div class="mfoot">
				<button type="button" class="btn dim" onclick={() => view.resetFilters()}>Reset</button>
				<button type="button" class="btn" onclick={() => (view.modalOpen = false)}>Apply</button>
			</div>
		</div>
	</div>
{/if}

<script lang="ts">
import { EAttribute } from '$lib/api';
import { attributeColor, attributeEnumName } from '$lib/common';
import { staticData } from '$stores';
import { SKILL_SORTS, type SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();

const attributeName = (id: EAttribute) =>
	staticData.attributes?.find((a) => a.id === id)?.name ?? attributeEnumName(id);

/** Escape closes the open filter overlay (the backdrop click / Apply are the other paths). */
const onKeydown = (e: KeyboardEvent) => {
	if (e.key === 'Escape' && view.modalOpen) {
		view.modalOpen = false;
	}
};
</script>

<style lang="scss">
.modal-layer {
	position: absolute;
	inset: 0;
	z-index: 40;
}

.backdrop {
	position: absolute;
	inset: 0;
	border: none;
	background: color-mix(in srgb, var(--black) 55%, transparent);
	backdrop-filter: blur(3px);
	cursor: pointer;
}

.modal {
	position: absolute;
	left: 50%;
	top: 46%;
	transform: translate(-50%, -50%);
	width: 420px;
	max-width: calc(100% - 32px);
	padding: 22px 24px 20px;
	border: 1px solid var(--border-medium);
	border-radius: 6px;
	background: linear-gradient(160deg, color-mix(in srgb, var(--accent) 7%, var(--surface)), var(--surface));
	box-shadow:
		0 30px 80px color-mix(in srgb, var(--black) 60%, transparent),
		0 0 40px color-mix(in srgb, var(--accent) 10%, transparent);
}

.msub {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--text-muted);
}

h3 {
	margin: 2px 0 0;
	font-size: 19px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.mgroup {
	margin-top: 16px;
}

.gl {
	margin-bottom: 9px;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.opts {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}

.opt {
	padding: 6px 13px;
	border: 1px solid var(--border-light);
	border-radius: 3px;
	background: transparent;
	color: var(--text-secondary);
	font-family: var(--sans);
	font-size: 12.5px;
	cursor: pointer;

	&.on {
		background: var(--accent);
		border-color: var(--accent);
		color: var(--text-on-accent);
	}

	&.attr.on {
		background: color-mix(in srgb, var(--ac) 22%, transparent);
		border-color: var(--ac);
		color: var(--text-primary);
	}
}

.mrow {
	display: flex;
	align-items: center;
	justify-content: space-between;
	padding: 9px 0;
	border-bottom: 1px solid var(--border-subtle);

	.ml {
		font-size: 13.5px;
	}
}

.switch {
	position: relative;
	width: 40px;
	height: 21px;
	border: 1px solid var(--border-medium);
	border-radius: 20px;
	background: transparent;
	cursor: pointer;

	&::after {
		content: '';
		position: absolute;
		top: 2px;
		left: 2px;
		width: 15px;
		height: 15px;
		border-radius: 50%;
		background: var(--text-muted);
		transition: 0.12s;
	}

	&.on {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 22%, transparent);

		&::after {
			left: 21px;
			background: var(--accent);
		}
	}
}

.mfoot {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-top: 20px;
}

.btn {
	padding: 8px 16px;
	border: 1px solid var(--accent);
	border-radius: 3px;
	background: var(--accent);
	color: var(--text-on-accent);
	font-family: var(--sans);
	font-size: 13px;
	font-weight: 500;
	cursor: pointer;

	&.dim {
		background: transparent;
		border-color: var(--border-light);
		color: var(--text-secondary);
	}
}
</style>
