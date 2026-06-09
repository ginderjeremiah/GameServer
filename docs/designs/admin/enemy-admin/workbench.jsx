// ─────────────────────────────────────────────────────────────────────────
//  Entity-driven WORKBENCH — one component renders any entity from its config.
//  List ←→ tabbed detail, full diffing, delete / restore / reset.
//  Validation surfaces only as a ! triangle (per tab + per list row); the
//  amber dot is reserved for dirty/unsaved state. No summary rail.
// ─────────────────────────────────────────────────────────────────────────
function WarnTri({ size = 13, title }) {
	return <span title={title} style={{ display: 'inline-flex' }}><window.Icon k="warn" size={size} sw={1.6} stroke="var(--warning)" /></span>;
}

function Workbench2({ entity, groupLabel }) {
	const store = window.useEntityStore(entity);
	const { items, status, baselineOf, counts, saved, patch } = store;

	const [selId, setSelId] = React.useState(() => entity.seed[0].id);
	const [tab, setTab] = React.useState(entity.sections[0].key);
	const [q, setQ] = React.useState('');

	React.useEffect(() => {
		if (!items.find((it) => it.id === selId)) setSelId(items[0] ? items[0].id : null);
	}, [items, selId]);

	const sel = items.find((it) => it.id === selId) || items[0];
	const baseline = sel ? baselineOf(sel.id) : null;
	const filtered = items.filter((it) => (it.name || '').toLowerCase().includes(q.toLowerCase()));

	const newItem = () => { const id = store.addItem(); setSelId(id); setTab(entity.sections[0].key); };

	const sectionDirty = (section, item) => {
		const b = baselineOf(item.id);
		if (!b) return false;
		if (section.kind === 'fields') return section.fields.some((f) => !window.eq(item[f.key], b[f.key]));
		return !window.eq(item[section.itemsKey], b[section.itemsKey]);
	};

	if (!sel) {
		return <div className="empty-state" style={{ height: '100%' }}><div className="glyph"><window.Icon k={entity.glyph} size={22} /></div><div className="et">No {entity.label.toLowerCase()} left</div><button className="btn primary sm" onClick={newItem}><window.Icon k="plus" size={12} />New {entity.singular}</button></div>;
	}

	const selStatus = status(sel);
	const curSection = entity.sections.find((s) => s.key === tab);

	const liveItems = items.filter((it) => status(it) !== 'deleted');
	const flagged = liveItems.filter((it) => window.entityWarnings(entity, it).length > 0).length;

	return (
		<div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
			{/* ── page header (entity title + live summary) ── */}
			<div className="page-head">
				<div className="eyebrow">Admin Console{groupLabel ? ' · ' + groupLabel : ''}</div>
				<div className="page-title-row">
					<h1 className="page-title">{entity.label}</h1>
					<div className="page-summary">
						<span>{liveItems.length} {entity.label.toLowerCase()}</span>
						{flagged > 0 && <span className="flag"><window.Icon k="warn" size={12} sw={1.6} stroke="var(--warning)" />{flagged} need attention</span>}
						{counts.total > 0 && <span style={{ color: 'var(--text-secondary)' }}>{counts.total} unsaved</span>}
					</div>
				</div>
			</div>

			<div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
			{/* ── List pane ── */}
			<div style={{ width: 322, flexShrink: 0, borderRight: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column', background: 'var(--surface)' }}>
				<div style={{ padding: '16px 16px 12px', borderBottom: '1px solid var(--border-subtle)' }}>
					<div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
						<div style={{ display: 'flex', alignItems: 'baseline', gap: 9 }}>
							<span style={{ fontSize: 14, fontWeight: 500 }}>{entity.label}</span>
							<span className="meta">{items.filter((it) => status(it) !== 'deleted').length}</span>
						</div>
						<button className="btn primary sm" onClick={newItem}><window.Icon k="plus" size={12} />New</button>
					</div>
					<div className="search">
						<window.Icon k="search" sw={1.4} />
						<input className="inp" placeholder={`Search ${entity.label.toLowerCase()}…`} value={q} onChange={(e) => setQ(e.target.value)} />
					</div>
				</div>
				<div style={{ flex: 1, overflowY: 'auto', overflowX: 'hidden' }}>
					{filtered.map((it) => {
						const st = status(it);
						const warns = window.entityWarnings(entity, it);
						const badge = entity.listBadge(it);
						const edge = st === 'added' ? 'var(--accent)' : st === 'modified' ? 'var(--warning)' : st === 'deleted' ? 'var(--enemy)' : 'transparent';
						return (
							<button key={it.id} onClick={() => setSelId(it.id)}
								style={{ width: '100%', textAlign: 'left', background: it.id === selId ? 'rgba(161,194,247,.08)' : 'transparent', border: 'none', borderBottom: '1px solid var(--border-subtle)', padding: '10px 14px 10px 0', cursor: 'pointer', position: 'relative', display: 'flex', alignItems: 'center', gap: 10, opacity: st === 'deleted' ? 0.45 : 1 }}>
								<div className="row-edge" style={{ background: edge, boxShadow: edge === 'transparent' ? 'none' : `0 0 7px ${edge}`, width: 3, height: 34 }} />
								<div style={{ flex: 1, minWidth: 0 }}>
									<div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
										<span style={{ fontSize: 13.5, color: it.id === selId ? 'var(--text-primary)' : 'var(--text-secondary)', fontWeight: it.id === selId ? 500 : 400, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', textDecoration: st === 'deleted' ? 'line-through' : 'none' }}>
											{it.name || <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>{entity.blankName}</span>}
										</span>
										{badge && <span className="rare-tag" style={{ color: entity.badgeColor ? entity.badgeColor(it) : 'var(--text-secondary)', borderColor: 'currentColor' }}>{badge}</span>}
										{st === 'added' && <span className="spill added">new</span>}
										{st === 'modified' && <span className="spill modified">edited</span>}
										<div style={{ flex: 1 }} />
										{st !== 'deleted' && warns.length > 0 && <WarnTri title={warns.join(' · ')} />}
									</div>
									<span className="meta">
										{entity.meta(it).map(([label, val], i) => <span key={i}>{label ? <><b>{val}</b> {label}</> : <b style={{ color: 'var(--text-tertiary)' }}>{val}</b>}</span>)}
									</span>
								</div>
							</button>
						);
					})}
				</div>
			</div>

			{/* ── Detail pane (full width, no summary rail) ── */}
			<div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
				<div style={{ padding: '18px 32px 0' }}>
					<div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
						<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: sel.id < 0 ? 'var(--accent)' : 'var(--text-muted)' }}>{sel.id < 0 ? 'new' : '#' + sel.id}</span>
						<h2 style={{ margin: 0, fontSize: 22, fontWeight: 500, color: sel.name ? 'var(--text-primary)' : 'var(--text-muted)' }}>{sel.name || entity.blankName}</h2>
						{entity.listBadge(sel) && <span className="rare-tag" style={{ color: entity.badgeColor ? entity.badgeColor(sel) : 'var(--text-secondary)', borderColor: 'currentColor' }}>{entity.listBadge(sel)}</span>}
						{selStatus === 'added' && <span className="spill added">new</span>}
						{selStatus === 'modified' && <span className="spill modified">edited</span>}
						{selStatus === 'deleted' && <span className="spill deleted">removed</span>}
						<div style={{ flex: 1 }} />
						{selStatus === 'deleted' ? (
							<button className="hdr-action accent" onClick={() => store.restoreItem(sel.id)}><window.Icon k="check" size={11} sw={1.7} />Restore</button>
						) : (
							<>
								{selStatus === 'modified' && <button className="hdr-action reset" onClick={() => store.resetItem(sel.id)}>Reset</button>}
								<button className="hdr-action danger" onClick={() => store.removeItem(sel.id)}><window.Icon k="x" size={11} />Delete</button>
							</>
						)}
					</div>
					{/* tabs */}
					<div style={{ display: 'flex', gap: 4, marginTop: 16, borderBottom: '1px solid var(--border-subtle)', flexWrap: 'wrap' }}>
						{entity.sections.map((s) => {
							const cnt = s.count ? s.count(sel) : null;
							const incomplete = window.sectionWarnings(s, sel).length > 0;
							const dirty = sectionDirty(s, sel);
							return (
								<button key={s.key} onClick={() => setTab(s.key)}
									style={{ appearance: 'none', background: 'transparent', border: 'none', borderBottom: '2px solid ' + (tab === s.key ? 'var(--accent)' : 'transparent'), color: tab === s.key ? 'var(--text-primary)' : 'var(--text-tertiary)', fontFamily: 'var(--sans)', fontSize: 13, padding: '8px 14px 12px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
									{s.label}
									{cnt !== null && <span className="count-badge">{cnt}</span>}
									{incomplete && <WarnTri size={12} />}
									{dirty && <span style={{ width: 5, height: 5, borderRadius: '50%', background: 'var(--warning)', boxShadow: '0 0 5px var(--warning)' }} title="Unsaved changes" />}
								</button>
							);
						})}
					</div>
				</div>

				<div style={{ flex: 1, overflowY: 'auto', padding: '24px 32px' }}>
					<div style={{ maxWidth: 1020, opacity: selStatus === 'deleted' ? 0.5 : 1, pointerEvents: selStatus === 'deleted' ? 'none' : 'auto' }}>
						<div className="sec-title">{curSection.label}{curSection.desc ? <span style={{ textTransform: 'none', letterSpacing: 0, fontFamily: 'var(--sans)', fontSize: 12, color: 'var(--text-muted)', marginLeft: 4, whiteSpace: 'nowrap', flexShrink: 0 }}>— {curSection.desc}</span> : null}<span className="ln" /></div>
						<window.GenericSection section={curSection} item={sel} baseline={baseline} patch={patch} />
					</div>
				</div>

				{/* save bar with real pips */}
				<div className="save-bar">
					<div className="save-summary">
						{saved ? (
							<span className="saved"><window.Icon k="check" size={13} sw={1.7} />Changes saved</span>
						) : counts.total === 0 ? (
							<span>No unsaved changes</span>
						) : (
							<>
								<span className="pending">{counts.total} unsaved {counts.total === 1 ? 'change' : 'changes'}</span>
								<span style={{ display: 'inline-flex', gap: 12 }}>
									{counts.added > 0 && <span className="pip added"><span className="dot" />{counts.added} added</span>}
									{counts.modified > 0 && <span className="pip modified"><span className="dot" />{counts.modified} edited</span>}
									{counts.deleted > 0 && <span className="pip deleted"><span className="dot" />{counts.deleted} removed</span>}
								</span>
							</>
						)}
					</div>
					<div className="save-actions">
						<button className="btn" disabled={counts.total === 0} onClick={store.discard}>Discard</button>
						<button className="btn primary" disabled={counts.total === 0} onClick={store.save}>Save Changes</button>
					</div>
				</div>
			</div>
			</div>
		</div>
	);
}

window.Workbench2 = Workbench2;
