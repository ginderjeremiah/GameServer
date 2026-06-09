// ─────────────────────────────────────────────────────────────────────────
//  DIRECTION 1 — WORKBENCH (master–detail)
//  Enemy list on the left, tabbed detail on the right. Pick once, edit
//  everything in place, save the whole record at once.
// ─────────────────────────────────────────────────────────────────────────
function Workbench() {
	const store = window.useEnemyStore();
	const { enemies } = store;
	const patch = store.patchEnemy;
	const [selId, setSelId] = React.useState(enemies[0].id);
	const [tab, setTab] = React.useState('identity');
	const [q, setQ] = React.useState('');

	const sel = enemies.find((e) => e.id === selId) || enemies[0];
	const filtered = enemies.filter((e) => e.name.toLowerCase().includes(q.toLowerCase()));

	const newEnemy = () => {
		const id = store.addEnemy('');
		setSelId(id);
		setTab('identity');
	};

	return (
		<div style={{ display: 'flex', height: '100%' }}>
			{/* ── List pane ── */}
			<div style={{ width: 318, flexShrink: 0, borderRight: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column', background: 'var(--surface)' }}>
				<div style={{ padding: '16px 16px 12px', borderBottom: '1px solid var(--border-subtle)' }}>
					<div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
						<div style={{ display: 'flex', alignItems: 'baseline', gap: 9 }}>
							<span style={{ fontSize: 14, fontWeight: 500 }}>Enemies</span>
							<span className="meta">{enemies.length}</span>
						</div>
						<button className="btn primary sm" onClick={newEnemy}><Icon k="plus" size={12} />New</button>
					</div>
					<div className="search">
						<Icon k="search" sw={1.4} />
						<input className="inp" placeholder="Search enemies…" value={q} onChange={(e) => setQ(e.target.value)} />
					</div>
				</div>
				<div style={{ flex: 1, overflowY: 'auto' }}>
					{filtered.map((e) => {
						const warns = window.enemyWarnings(e);
						return (
							<button key={e.id} onClick={() => setSelId(e.id)}
								style={{
									width: '100%', textAlign: 'left', background: e.id === selId ? 'rgba(161,194,247,.08)' : 'transparent',
									border: 'none', borderBottom: '1px solid var(--border-subtle)', padding: '11px 16px 11px 18px',
									cursor: 'pointer', position: 'relative', display: 'block'
								}}>
								{e.id === selId && <span style={{ position: 'absolute', left: 0, top: 7, bottom: 7, width: 2, background: 'var(--accent)', boxShadow: '0 0 10px rgba(161,194,247,.75)' }} />}
								<div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5 }}>
									<span style={{ fontSize: 13.5, color: e.id === selId ? 'var(--text-primary)' : 'var(--text-secondary)', fontWeight: e.id === selId ? 500 : 400, flex: 1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
										{e.name || <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>Unnamed enemy</span>}
									</span>
									{e.isBoss && <BossTag />}
									{warns.length > 0 && <span className="warn-dot" title={warns.join(' · ')} />}
								</div>
								<MetaLine e={e} />
							</button>
						);
					})}
				</div>
			</div>

			{/* ── Detail pane ── */}
			<div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
				<div style={{ padding: '20px 28px 0' }}>
					<div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
						<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--text-muted)' }}>#{sel.id}</span>
						<h2 style={{ margin: 0, fontSize: 22, fontWeight: 500 }}>{sel.name || 'Unnamed enemy'}</h2>
						{sel.isBoss && <BossTag />}
						<div style={{ flex: 1 }} />
						<div style={{ width: 120 }}><CompletenessMeter e={sel} /></div>
					</div>
					{/* tab strip */}
					<div style={{ display: 'flex', gap: 4, marginTop: 18, borderBottom: '1px solid var(--border-subtle)' }}>
						{window.SECTIONS.map((s) => {
							const c = window.completeness(sel)[s.key];
							const counts = { attrs: sel.attrs.length, skills: sel.skills.length, spawns: sel.spawns.length };
							return (
								<button key={s.key} onClick={() => setTab(s.key)}
									style={{
										appearance: 'none', background: 'transparent', border: 'none', borderBottom: '2px solid ' + (tab === s.key ? 'var(--accent)' : 'transparent'),
										color: tab === s.key ? 'var(--text-primary)' : 'var(--text-tertiary)', fontFamily: 'var(--sans)', fontSize: 13,
										padding: '8px 14px 12px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8
									}}>
									{s.label}
									{s.key !== 'identity' && <span className={'count-badge' + (counts[s.key] === 0 ? ' empty' : '')}>{counts[s.key]}</span>}
									{s.key === 'identity' && !c && <span className="warn-dot" />}
								</button>
							);
						})}
					</div>
				</div>

				<div style={{ flex: 1, overflowY: 'auto', padding: '24px 28px' }}>
					<div style={{ display: 'flex', gap: 28, alignItems: 'flex-start' }}>
						<div style={{ flex: 1, minWidth: 0 }}>
							<div className="sec-title">{window.SECTIONS.find((s) => s.key === tab).label}<span className="ln" /></div>
							<SectionEditor which={tab} e={sel} patch={patch} />
						</div>
						{/* persistent summary rail — keeps the page from feeling empty */}
						<div className="card" style={{ width: 252, flexShrink: 0, padding: 18 }}>
							<div className="lbl" style={{ marginBottom: 14 }}>Record Summary</div>
							<Checklist e={sel} />
							<div style={{ height: 1, background: 'var(--border-subtle)', margin: '16px 0' }} />
							<div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
								{window.enemyWarnings(sel).length === 0
									? <span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--success)', display: 'inline-flex', gap: 7, alignItems: 'center' }}><Icon k="check" size={12} sw={1.7} />Ready to deploy</span>
									: window.enemyWarnings(sel).map((w) => <WarnChip key={w} text={w} />)}
							</div>
						</div>
					</div>
				</div>

				<SaveBar store={store} />
			</div>
		</div>
	);
}

window.Workbench = Workbench;
