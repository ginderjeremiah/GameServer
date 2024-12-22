<div class="admin-container">
	<NavMenu {navMenuItems} />
	<div class="tool-container round-border">
		<CurrentTool bind:this={toolInstance}>
			<div class="save-button-container">
				<Button text="Save" onClick={saveChanges} />
			</div>
		</CurrentTool>
	</div>
</div>

<script lang="ts">
import { Button, NavMenu, type INavMenuItem } from '$components';
import { normalizeText, routeTo } from '$lib/common';
import { toolMap } from './tools';

let CurrentTool = $state(toolMap['Items']['Add/Edit Items']);
let toolInstance = $state<{ saveChanges: () => Promise<void> }>();

const navMenuItems: INavMenuItem[] = Object.entries(toolMap).map(([text, item]) => {
	const baseItem: INavMenuItem = {
		text: normalizeText(text)
	};

	if (typeof item === 'object') {
		baseItem.children = Object.entries(item).map(([itemText, component]) => ({
			text: itemText,
			onClick: () => (CurrentTool = component)
		}));
	} else {
		baseItem.onClick = () => (CurrentTool = item);
	}

	return baseItem;
});

navMenuItems.push({
	text: 'Game',
	onClick: () => routeTo('/game')
});

const saveChanges = () => {
	toolInstance?.saveChanges();
};
</script>

<style lang="scss">
.admin-container {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;

	.tool-container {
		height: 100%;
		margin: 2rem;
		padding: 0.5rem;
		border: var(--default-border);
		border-width: 3px;
		border-radius: 1vw;
		background-color: var(--container-background-color);
		user-select: text;
		position: relative;

		.save-button-container {
			width: 6rem;
			margin-top: 1rem;
		}
	}
}
</style>
