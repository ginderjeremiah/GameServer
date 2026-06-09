// ─────────────────────────────────────────────────────────────────────────
//  DIRECTION 2 — RECORD SHEET (single scroll, stacked section cards)
//  A compact enemy switcher at top, then every section open and scannable
//  as a collapsible card. One save-all bar aggregates the whole record.
// ─────────────────────────────────────────────────────────────────────────
function RecordSheet() {
	const store = window.useEnemyStore();
	const { enemies } = store;
	const patch = store.patchEnemy;
	const [selId, setSelId] = React.useState(enemies[0].id);
	const [open, setOpen] = React.useState({ identity: true, attrs: true, skills: true, spawns: true });
	const [pickerOpen, setPickerOpen] = React.useState(false);
	const [q, setQ] = React.useState('');

	const sel = enemies.find((e) => e.id === selId) || enemies[0];
	const idx = enemies.findIndex((e) => e.id === selId);
	const filtered = enemies.filter((e) => e.name.toLowerCase().includes(q.toLowerCase()));
	const toggle = (k) => setOpen((o) => ({ ...o, [k]: !o[k] }));
	const go = (d) => { const n = enemies[idx + d]; if (n) setSelId(n.id); };

	const counts = { attrs: sel.attrs.length, skills: sel.skills.length, spawns: sel.spawns.length };

	return (
		<div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
			{/* ── Switcher bar ── */}
			<div style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '16px 40px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--surface)' }}>
				<div style={{ display: 'flex', gap: 2 }}>
					<button className="btn sm" disabled={idx <= 0} onClick={() => go(-1)} style={{ borderTopRightRadius: 0, borderBottomRightRadius: 0 }}><Icon k="chevR" size={12} className="" stroke="currentColor" /></button>
					<button className="btn sm" disabled={idx >= enemies.length - 1} onClick={() => go(1)} style={{ borderTopLeftRadius: 0, borderBottomLeftRadius: 0, borderLeft: 'none' }}><Icon k="chevR" size={12} /></button>
				</div>
				{/* combo picker */}
				<div style={{ position: 'relative', minWidth: 300 }}>
					<button className="btn" style={{ width: '100%', justifyContent: 'space-between', textTransform: 'none', fontFamily: 'var(--sans)', fontSize: 14, padding: '9px 14px', letterSpacing: 0 }}
						onClick={() => setPickerOpen((v) => !v)}>
						<span style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
							{sel.name || <span style={{ color: 'var(--text-muted)' }}>Unnamed enemy</span>}
							{sel.isBoss && <BossTag />}
						</span>
						<Icon k="chevD" size={12} stroke="var(--text-tertiary)" />
					</button>
					{pickerOpen && (
						<div style={{ position: 'absolute', top: '110%', left: 0, right: 0, zIndex: 20, background: 'var(--panel)', border: '1px solid var(--border-light)', borderRadius: 5, boxShadow: '0 12px 32px rgba(0,0,0,.5)', overflow: 'hidden' }}>
							<div className="search" style={{ padding: 8, borderBottom: '1px solid var(--border-subtle)' }}>
								<Icon k="search" sw={1.4} />
								<input className="inp" autoFocus placeholder="Search…" value={q} onChange={(e) => setQ(e.target.value)} style={{ marginLeft: 0 }} />
							</div>
							<div style={{ maxHeight: 280, overflowY: 'auto' }}>
								{filtered.slice(0, 60).map((e) => (
									<button key={e.id} onClick={() => { setSelId(e.id); setPickerOpen(false); setQ(''); }}
										style={{ width: '100%', textAlign: 'left', background: e.id === selId ? 'rgba(161,194,247,.08)' : 'transparent', border: 'none', padding: '9px 14px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8, color: 'var(--text-secondary)', fontSize: 13 }}>
										<span style={{ flex: 1 }}>{e.name || 'Unnamed'}</span>
										{e.isBoss && <span style={{ fontFamily: 'var(--mono)', fontSize: 9, color: 'var(--enemy)' }}>BOSS</span>}
										{window.enemyWarnings(e).length > 0 && <span className="warn-dot" />}
									</button>
								))}
							</div>
						</div>
					)}
				</div>
				<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--text-muted)' }}>#{sel.id} · {idx + 1} of {enemies.length}</span>
				<div style={{ flex: 1 }} />
				{window.enemyWarnings(sel).length > 0
					? <WarnChip text={`${window.enemyWarnings(sel).length} issue${window.enemyWarnings(sel).length > 1 ? 's' : ''}`} />
					: <span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--success)', display: 'inline-flex', gap: 7, alignItems: 'center' }}><Icon k="check" size={12} sw={1.7} />Complete</span>}
				<button className="btn primary sm" onClick={() => setSelId(store.addEnemy(''))}><Icon k="plus" size={12} />New Enemy</button>
			</div>

			{/* ── Stacked section cards ── */}
			<div style={{ flex: 1, overflowY: 'auto', padding: '22px 40px' }}>
				<div style={{ maxWidth: 880, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 14 }}>
					{window.SECTIONS.map((s) => {
						const empty = s.key !== 'identity' && counts[s.key] === 0;
						return (
							<div className="section-card" key={s.key}>
								<div className={'section-head' + (open[s.key] ? ' open' : '')} onClick={() => toggle(s.key)}>
									<div className="glyph"><Icon k={s.glyph} size={15} /></div>
									<div>
										<div className="tt">{s.label}</div>
										<div className="desc">{s.desc}</div>
									</div>
									<div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 12 }}>
										{s.key !== 'identity' && <span className={'count-badge' + (empty ? ' empty' : '')}>{counts[s.key]}</span>}
										{empty && <span className="warn-dot" />}
										<Icon k="chevR" size={13} className="chev" stroke="var(--text-tertiary)" />
									</div>
								</div>
								{open[s.key] && (
									<div className="section-body">
										<SectionEditor which={s.key} e={sel} patch={patch} />
									</div>
								)}
							</div>
						);
					})}
				</div>
			</div>

			<SaveBar store={store} />
		</div>
	);
}

window.RecordSheet = RecordSheet;
