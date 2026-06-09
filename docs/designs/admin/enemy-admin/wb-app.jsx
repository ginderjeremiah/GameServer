// ─────────────────────────────────────────────────────────────────────────
//  App shell. Production-style chrome: an expand-on-hover admin sidebar
//  (mirroring the live AdminSidebar) is the only navigation. Each entity is a
//  single consolidated page rendered by the one Workbench2 component.
// ─────────────────────────────────────────────────────────────────────────
const NAV_GROUPS = [
	{ key: 'combat', label: 'Combat', entities: ['enemies', 'skills'] },
	{ key: 'items', label: 'Items', entities: ['items', 'itemMods', 'tags'] },
	{ key: 'world', label: 'World', entities: ['zones'] }
];
const groupLabelFor = (entKey) => (NAV_GROUPS.find((g) => g.entities.includes(entKey)) || {}).label || '';
const NAV_COUNT = Object.keys(window.ENTITIES).length;

function Sidebar({ active, onNavigate }) {
	const [hovering, setHovering] = React.useState(false);
	const [pinned, setPinned] = React.useState(false);
	const expanded = pinned || hovering;

	return (
		<div className={'side' + (expanded ? ' expanded' : '')}
			onMouseEnter={() => setHovering(true)} onMouseLeave={() => setHovering(false)}>
			{/* header / wordmark */}
			<div className="side-header">
				<div className="glyph-slot"><div className="game-diamond" /></div>
				<div className={'wordmark-block' + (expanded ? ' show' : '')}>
					<div className="wordmark">Tactic Foundry</div>
					<div className="wordmark-tag">Admin Console</div>
				</div>
				{expanded && (
					<button className={'pin-btn' + (pinned ? ' pinned' : '')} title={pinned ? 'Unpin' : 'Keep open'} onClick={() => setPinned((p) => !p)}>
						<window.Icon k="pin" size={13} sw={1.4} />
					</button>
				)}
			</div>

			{/* nav body */}
			<div className="side-body">
				{NAV_GROUPS.map((g, gi) => (
					<div className="nav-group" key={g.key}>
						<div className={'group-header' + (gi === 0 ? ' first' : '')}>
							{expanded ? <span className="group-label">{g.label}</span> : (gi > 0 ? <div className="glyph-slot"><div className="group-divider" /></div> : null)}
						</div>
						{g.entities.map((ek) => {
							const e = window.ENTITIES[ek];
							const isActive = active === ek;
							return (
								<button key={ek} className={'side-item' + (isActive ? ' active' : '')} title={!expanded ? e.label : undefined} onClick={() => onNavigate(ek)}>
									<div className="glyph-slot"><window.Icon k={e.glyph} size={16} stroke={isActive ? 'var(--accent)' : 'currentColor'} /></div>
									<span className={'item-label' + (expanded ? ' show' : '')}>{e.label}</span>
									{isActive && <span className="active-bar" />}
								</button>
							);
						})}
					</div>
				))}
			</div>

			{/* return to game */}
			<div className="side-return">
				<button className="side-item" title={!expanded ? 'Return to Game' : undefined}>
					<div className="glyph-slot"><window.Icon k="back" size={16} /></div>
					<span className={'item-label' + (expanded ? ' show' : '')}>Return to Game</span>
				</button>
			</div>

			{/* footer */}
			<div className="side-footer">
				<div className="glyph-slot"><div className="pulse-dot" /></div>
				<div className={'footer-status' + (expanded ? ' show' : '')}>{NAV_COUNT} tools</div>
			</div>
		</div>
	);
}

function WBApp() {
	const [ent, setEnt] = React.useState('enemies');
	const entity = window.ENTITIES[ent];

	return (
		<div className="app">
			<Sidebar active={ent} onNavigate={setEnt} />
			<div className="side-spacer" />
			<div className="stage">
				<Workbench2 key={ent} entity={entity} groupLabel={groupLabelFor(ent)} />
			</div>
		</div>
	);
}

ReactDOM.createRoot(document.getElementById('root')).render(<WBApp />);
