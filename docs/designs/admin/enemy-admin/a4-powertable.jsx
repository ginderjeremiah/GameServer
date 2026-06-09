// ─────────────────────────────────────────────────────────────────────────
//  DIRECTION 4 — POWER TABLE (expandable rows + quick-add)
//  The familiar enemy table, but each row expands inline to a tabbed detail
//  drawer. A quick-add command bar creates rows without leaving the grid.
// ─────────────────────────────────────────────────────────────────────────
function PowerTable() {
	const store = window.useEnemyStore();
	const { enemies } = store;
	const patch = store.patchEnemy;
	const [expanded, setExpanded] = React.useState(enemies[0].id);
	const [drawerTab, setDrawerTab] = React.useState('attrs');
	const [q, setQ] = React.useState('');

	const filtered = enemies.filter((e) => e.name.toLowerCase().includes(q.toLowerCase()));
	const toggle = (id) => { setExpanded((cur) => (cur === id ? null : id)); setDrawerTab('attrs'); };

	const quickAdd = (e) => {
		if (e.key === 'Enter' && e.target.value.trim()) {
			const id = store.addEnemy(e.target.value.trim());
			e.target.value = '';
			setExpanded(id);
			setDrawerTab('identity');
		}
	};

	const DRAWER_TABS = [
		{ key: 'identity', label: 'Identity' }, { key: 'attrs', label: 'Attributes' },
		{ key: 'skills', label: 'Skills' }, { key: 'spawns', label: 'Spawns' }
	];

	return (
		<div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
			{/* ── Quick-add command bar ── */}
			<div style={{ padding: '16px 40px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--surface)', display: 'flex', gap: 14, alignItems: 'center' }}>
				<div style={{ position: 'relative', flex: 1, maxWidth: 460 }}>
					<span style={{ position: 'absolute', left: 13, top: '50%', transform: 'translateY(-50%)', color: 'var(--accent)', fontFamily: 'var(--mono)', fontSize: 15 }}>›</span>
					<input className="inp" placeholder="Add enemy — type a name and press ⏎" onKeyDown={quickAdd}
						style={{ paddingLeft: 32, fontSize: 13.5 }} />
				</div>
				<div className="search" style={{ width: 220 }}>
					<Icon k="search" sw={1.4} />
					<input className="inp" placeholder="Filter…" value={q} onChange={(e) => setQ(e.target.value)} />
				</div>
				<div style={{ flex: 1 }} />
				<span className="meta">{filtered.length} of {enemies.length}</span>
			</div>

			{/* ── Table ── */}
			<div style={{ flex: 1, overflowY: 'auto', padding: '0 40px' }}>
				<table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 720 }}>
					<thead>
						<tr style={{ position: 'sticky', top: 0, zIndex: 2, background: 'var(--page)' }}>
							{['', 'Enemy', 'Class', 'Attr', 'Skills', 'Zones', 'Status'].map((h, i) => (
								<th key={i} style={{ fontFamily: 'var(--mono)', fontSize: 9, fontWeight: 400, letterSpacing: '1.3px', textTransform: 'uppercase', color: 'var(--text-muted)', textAlign: i > 2 && i < 6 ? 'center' : 'left', padding: '13px 12px', borderBottom: '1px solid var(--border-light)', whiteSpace: 'nowrap' }}>{h}</th>
							))}
						</tr>
					</thead>
					<tbody>
						{filtered.slice(0, 80).map((e) => {
							const isOpen = expanded === e.id;
							const warns = window.enemyWarnings(e);
							return (
								<React.Fragment key={e.id}>
									<tr onClick={() => toggle(e.id)} style={{ cursor: 'pointer', background: isOpen ? 'rgba(161,194,247,.05)' : 'transparent' }}>
										<td style={{ width: 36, padding: '10px 0 10px 12px', borderBottom: '1px solid var(--border-subtle)' }}>
											<Icon k={isOpen ? 'chevD' : 'chevR'} size={13} stroke={isOpen ? 'var(--accent)' : 'var(--text-tertiary)'} />
										</td>
										<td style={{ padding: '10px 12px', borderBottom: '1px solid var(--border-subtle)', fontSize: 13.5, color: 'var(--text-primary)' }}>
											{e.name || <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>Unnamed</span>}
										</td>
										<td style={{ padding: '10px 12px', borderBottom: '1px solid var(--border-subtle)' }}>{e.isBoss ? <BossTag /> : <span style={{ fontFamily: 'var(--mono)', fontSize: 10, color: 'var(--text-muted)' }}>—</span>}</td>
										{['attrs', 'skills', 'spawns'].map((k) => (
											<td key={k} style={{ padding: '10px 12px', borderBottom: '1px solid var(--border-subtle)', textAlign: 'center' }}>
												<span className={'count-badge' + (e[k].length === 0 ? ' empty' : '')}>{e[k].length}</span>
											</td>
										))}
										<td style={{ padding: '10px 12px', borderBottom: '1px solid var(--border-subtle)' }}>
											{warns.length === 0
												? <span style={{ fontFamily: 'var(--mono)', fontSize: 10.5, color: 'var(--success)', display: 'inline-flex', gap: 6, alignItems: 'center' }}><Icon k="check" size={11} sw={1.8} />Ready</span>
												: <span style={{ fontFamily: 'var(--mono)', fontSize: 10.5, color: 'var(--warning)', display: 'inline-flex', gap: 6, alignItems: 'center' }} title={warns.join(' · ')}><Icon k="warn" size={11} sw={1.5} />{warns.length} issue{warns.length > 1 ? 's' : ''}</span>}
										</td>
									</tr>
									{isOpen && (
										<tr>
											<td colSpan={7} style={{ padding: 0, background: 'var(--panel)', borderBottom: '1px solid var(--border-light)' }}>
												<div style={{ padding: '4px 20px 22px 48px' }}>
													<div style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--border-subtle)', marginBottom: 18 }}>
														{DRAWER_TABS.map((t) => (
															<button key={t.key} onClick={() => setDrawerTab(t.key)}
																style={{ appearance: 'none', background: 'transparent', border: 'none', borderBottom: '2px solid ' + (drawerTab === t.key ? 'var(--accent)' : 'transparent'), color: drawerTab === t.key ? 'var(--text-primary)' : 'var(--text-tertiary)', fontFamily: 'var(--sans)', fontSize: 12.5, padding: '8px 13px 11px', cursor: 'pointer' }}>
																{t.label}
															</button>
														))}
														<div style={{ flex: 1 }} />
														<div style={{ alignSelf: 'center', display: 'flex', gap: 8 }}>
															{warns.map((w) => <WarnChip key={w} text={w} />)}
														</div>
													</div>
													<div style={{ maxWidth: 720, paddingBottom: 6 }}>
														<SectionEditor which={drawerTab} e={e} patch={patch} />
													</div>
												</div>
											</td>
										</tr>
									)}
								</React.Fragment>
							);
						})}
					</tbody>
				</table>
			</div>

			<SaveBar store={store} />
		</div>
	);
}

window.PowerTable = PowerTable;
