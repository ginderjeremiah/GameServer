// ─────────────────────────────────────────────────────────────────────────
//  App shell: icon rail (admin chrome) + direction switcher + active mock.
// ─────────────────────────────────────────────────────────────────────────
const DIRECTIONS = [
	{ key: 'workbench', label: 'Workbench', comp: 'Workbench', sub: 'Master–detail: an enemy list on the left, a tabbed detail panel on the right. Select once, edit Identity / Attributes / Skills / Spawns in place, and save the whole record together.' },
	{ key: 'recordsheet', label: 'Record Sheet', comp: 'RecordSheet', sub: 'A single scrolling sheet per enemy: every section is a collapsible card you can scan at once, with one save-all bar aggregating the whole record.' },
	{ key: 'wizard', label: 'Wizard', comp: 'Wizard', sub: 'A guided New-Enemy flow — Identity → Attributes → Skills → Spawns → Review — that commits once at the end. Best for fast first-time setup.' },
	{ key: 'powertable', label: 'Power Table', comp: 'PowerTable', sub: 'The familiar enemy grid, but rows expand inline to a detail drawer and a quick-add bar creates enemies without leaving the table. Built for power users editing many at once.' }
];

const RAIL_ICONS = [
	{ k: 'skull', active: true }, { k: 'box' }, { k: 'bolt' }, { k: 'map' }
];

function App() {
	const [dir, setDir] = React.useState('workbench');
	const active = DIRECTIONS.find((d) => d.key === dir);
	const Comp = window[active.comp];

	return (
		<div className="app">
			{/* icon rail — context chrome mirroring the live admin console */}
			<div className="rail">
				<div className="rail-diamond" />
				{RAIL_ICONS.map((r, i) => (
					<React.Fragment key={r.k}>
						{i === 1 && <div className="rail-div" />}
						<div className={'rail-item' + (r.active ? ' active' : '')}><Icon k={r.k} size={16} /></div>
					</React.Fragment>
				))}
				<div className="rail-spacer" />
				<div className="rail-dot" />
			</div>

			<div className="stage">
				<div className="stage-head">
					<div className="eyebrow">Admin · Enemies — Redesign Explorations</div>
					<h1 className="stage-title">One-stop enemy editor</h1>
					<div className="stage-sub">{active.sub}</div>
					<div className="switcher">
						{DIRECTIONS.map((d, i) => (
							<button key={d.key} className={'switch-tab' + (dir === d.key ? ' active' : '')} onClick={() => setDir(d.key)}>
								<span className="num">{i + 1}</span>{d.label}
							</button>
						))}
					</div>
				</div>
				<div className="stage-body">
					<Comp key={dir} />
				</div>
			</div>
		</div>
	);
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
