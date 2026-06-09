// ─────────────────────────────────────────────────────────────────────────
//  Generic, dirty-aware section renderers used by the entity-driven Workbench.
//  A section is 'fields' | 'table' | 'chips'; columns are 'select' | 'number'
//  | 'share'. Dirty state is computed against the saved baseline.
// ─────────────────────────────────────────────────────────────────────────

// ── Decimal-safe numeric input. Keeps the in-progress text locally so typing
//    "1." / "0.5" isn't clobbered by re-coercion on every keystroke. ──
function NumInput({ value, onChange, className, allowNegative }) {
	const [text, setText] = React.useState(() => String(value ?? 0));
	React.useEffect(() => {
		if (text.trim() === '' || text === '-' || text === '.') return; // mid-entry, don't clobber
		if (parseFloat(text) !== value) setText(String(value));
	}, [value]); // eslint-disable-line
	const pattern = allowNegative ? /^-?\d*\.?\d*$/ : /^\d*\.?\d*$/;
	const handle = (e) => {
		const raw = e.target.value;
		if (raw !== '' && !pattern.test(raw)) return;
		setText(raw);
		if (raw === '' || raw === '-' || raw === '.') { onChange(0); return; }
		const n = parseFloat(raw);
		if (!isNaN(n)) onChange(n);
	};
	return <input className={className} value={text} inputMode="decimal" onChange={handle} />;
}

// ── A field label with optional validation triangle. ──
function FieldLabel({ field, warn }) {
	return (
		<label className="lbl" style={warn ? { color: 'var(--warning)', display: 'flex', alignItems: 'center', gap: 5 } : null}>
			{field.label}
			{warn && <window.Icon k="warn" size={11} sw={1.6} stroke="var(--warning)" />}
		</label>
	);
}

// ── A single scalar field (text / number / toggle / textarea) with dirty dot
//    and a per-field validation triangle when a required value is missing. ──
function Field({ field, item, baseline, patch }) {
	const v = item[field.key];
	const dirty = baseline ? !window.eq(v, baseline[field.key]) : false;
	const warn = window.fieldWarn(field, item);
	const set = (val) => patch(item.id, (x) => { x[field.key] = val; });

	if (field.type === 'toggle') {
		return (
			<div className="fld">
				<FieldLabel field={field} warn={warn} />
				<div className={'toggle' + (v ? ' on' : '') + (dirty ? ' dirty' : '')} style={{ height: 38 }} onClick={() => set(!v)}>
					<span className="track"><span className="knob" /></span>
					<span className="tg-label">{v ? field.onLabel : field.offLabel}</span>
				</div>
			</div>
		);
	}
	if (field.type === 'textarea') {
		return (
			<div className="fld" style={{ flex: field.grow ? '1 1 100%' : 'none', width: '100%' }}>
				<FieldLabel field={field} warn={warn} />
				<textarea className={'txtarea' + (dirty ? ' dirty' : '') + (warn ? ' invalid' : '')} placeholder={field.placeholder} value={v}
					onChange={(e) => set(e.target.value)} />
				{dirty && <span className="dirty-dot" />}
			</div>
		);
	}
	if (field.type === 'number') {
		return (
			<div className="fld" style={{ width: field.width || 150 }}>
				<FieldLabel field={field} warn={warn} />
				<div className="num-unit">
					<NumInput className={'inp num' + (dirty ? ' dirty' : '') + (warn ? ' invalid' : '')} value={v} onChange={set} allowNegative={field.allowNegative} />
					{field.suffix && <span className="suffix">{field.suffix}</span>}
				</div>
				{dirty && <span className="dirty-dot" />}
			</div>
		);
	}
	if (field.type === 'select') {
		return (
			<div className="fld" style={{ width: field.width || 170 }}>
				<FieldLabel field={field} warn={warn} />
				<div style={{ position: 'relative' }}>
					<select className={'sel' + (dirty ? ' dirty' : '') + (warn ? ' invalid' : '')} value={v} onChange={(e) => set(+e.target.value)}>
						{field.options().map((o) => <option key={o.value} value={o.value}>{o.text}</option>)}
					</select>
					<window.Caret />
					{dirty && <span className="dirty-dot" />}
				</div>
			</div>
		);
	}
	// text
	return (
		<div className="fld" style={{ flex: field.grow ? '1 1 280px' : 'none', minWidth: field.grow ? 240 : 'auto' }}>
			<FieldLabel field={field} warn={warn} />
			<input className={'inp' + (dirty ? ' dirty' : '') + (warn ? ' invalid' : '')} placeholder={field.placeholder} value={v}
				onChange={(e) => set(e.target.value)} />
			{dirty && <span className="dirty-dot" />}
		</div>
	);
}

function FieldsSection({ section, item, baseline, patch }) {
	return (
		<div style={{ display: 'flex', gap: 18, flexWrap: 'wrap', alignItems: 'flex-end' }}>
			{section.fields.map((f) => <Field key={f.key} field={f} item={item} baseline={baseline} patch={patch} />)}
		</div>
	);
}

// ── A row-based collection table (attrs / spawns / multipliers). ──
function rowStatus(baseRows, i) {
	if (!baseRows || i >= baseRows.length) return 'added';
	return null; // 'modified' handled per-cell
}

function TableSection({ section, item, baseline, patch }) {
	const rows = item[section.itemsKey] || [];
	const baseRows = baseline ? baseline[section.itemsKey] : null;

	const add = () => patch(item.id, (x) => x[section.itemsKey].push(section.newRow(x)));
	const removeRow = (i) => patch(item.id, (x) => x[section.itemsKey].splice(i, 1));

	// When a select column is `unique`, disable Add once every option is taken.
	const uniqueCol = section.columns.find((c) => c.type === 'select' && c.unique);
	const noFree = uniqueCol ? uniqueCol.options().every((o) => rows.some((r) => r[uniqueCol.key] === o.value)) : false;

	if (rows.length === 0) {
		return <window.EmptySection icon={section.emptyIcon} title={section.emptyTitle} sub={section.emptySub} onAdd={add} addLabel={section.addLabel} />;
	}

	return (
		<div>
			<table className="mtable">
				<thead>
					<tr>
						<th style={{ width: 18, padding: 0 }}></th>
						{section.columns.map((c) => <th key={c.key} className={c.align === 'r' ? 'r' : ''}>{c.label}</th>)}
						<th className="c" style={{ width: 40 }}></th>
					</tr>
				</thead>
				<tbody>
					{rows.map((row, i) => {
						const isNewRow = !baseRows || i >= baseRows.length;
						const baseRow = baseRows && baseRows[i];
						const edge = isNewRow ? 'var(--accent)' : (baseRow && !window.eq(row, baseRow)) ? 'var(--warning)' : 'transparent';
						return (
							<tr key={i}>
								<td style={{ padding: 0, width: 18 }}>
									<div className="row-edge" style={{ background: edge, boxShadow: edge === 'transparent' ? 'none' : `0 0 7px ${edge}` }} />
								</td>
								{section.columns.map((c) => {
									const cellDirty = !isNewRow && baseRow ? !window.eq(row[c.key], baseRow[c.key]) : false;
									return <Cell key={c.key} col={c} row={row} idx={i} rows={rows} record={item} dirty={cellDirty}
										onChange={(val) => patch(item.id, (x) => { x[section.itemsKey][i][c.key] = val; })} />;
								})}
								<td className="c">
									<button className="row-x" title="Remove" onClick={() => removeRow(i)}><window.Icon k="x" size={11} /></button>
								</td>
							</tr>
						);
					})}
				</tbody>
			</table>
			<button className="btn sm" style={{ marginTop: 12 }} onClick={add} disabled={noFree}
				title={noFree ? 'Every option is already assigned' : undefined}>
				<window.Icon k="plus" size={12} />{section.addLabel}
			</button>
		</div>
	);
}

function Cell({ col, row, idx, rows, record, dirty, onChange }) {
	if (col.type === 'select') {
		// For `unique` columns, disable options already taken by other rows so a
		// relationship can't be selected twice.
		const taken = col.unique ? new Set(rows.filter((_, ri) => ri !== idx).map((r) => r[col.key])) : null;
		return (
			<td style={{ minWidth: col.min || 160 }}>
				<div className="fld">
					<select className={'sel' + (dirty ? ' dirty' : '')} value={row[col.key]} onChange={(e) => onChange(+e.target.value)}>
						{col.options().map((o) => (
							<option key={o.value} value={o.value} disabled={taken ? taken.has(o.value) && o.value !== row[col.key] : false}>{o.text}</option>
						))}
					</select>
					<window.Caret />
					{dirty && <span className="dirty-dot" />}
				</div>
			</td>
		);
	}
	if (col.type === 'number') {
		return (
			<td className={col.align === 'r' ? 'r' : ''} style={{ width: col.width || 110 }}>
				<div className="fld">
					<NumInput className={'inp num' + (dirty ? ' dirty' : '')} value={row[col.key]} onChange={onChange} allowNegative={col.allowNegative} />
					{dirty && <span className="dirty-dot" />}
				</div>
			</td>
		);
	}
	if (col.type === 'share') {
		const wk = col.weightKey || 'weight';
		// Default denominator is the sibling rows (e.g. a zone's own enemies).
		// A column can override `shareTotal` for cross-record shares (e.g. an
		// enemy's weight relative to all enemies in the zone it's assigned to).
		const total = col.shareTotal ? col.shareTotal(row, rows, record) : (rows.reduce((s, r) => s + (r[wk] || 0), 0) || 1);
		const pct = total > 0 ? Math.round(((row[wk] || 0) / total) * 100) : 0;
		return (
			<td style={{ width: col.width || 150 }}>
				<div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
					<div className="share-bar" style={{ flex: 1 }}><span style={{ width: pct + '%' }} /></div>
					<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--text-tertiary)', minWidth: 30, textAlign: 'right' }}>{pct}%</span>
				</div>
			</td>
		);
	}
	return <td>{String(row[col.key])}</td>;
}

// ── Reference-pool chips (skill pool). ──
function ChipsSection({ section, item, baseline, patch }) {
	const ids = item[section.itemsKey] || [];
	const baseIds = baseline ? (baseline[section.itemsKey] || []) : null;
	const cat = section.catalogue();
	const avail = cat.filter((c) => !ids.includes(c.id));
	const add = (id) => { if (id) patch(item.id, (x) => x[section.itemsKey].push(+id)); };
	const remove = (id) => patch(item.id, (x) => { x[section.itemsKey] = x[section.itemsKey].filter((k) => k !== id); });

	return (
		<div>
			{ids.length === 0 ? (
				<window.EmptySection icon={section.emptyIcon} title={section.emptyTitle} sub={section.emptySub} />
			) : (
				<div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
					{ids.map((id) => {
						const c = cat.find((x) => x.id === id);
						const isNew = baseIds && !baseIds.includes(id);
						return (
							<div className={'skill-chip' + (isNew ? ' added' : '')} key={id}>
								<span className="nm">{c ? section.labelOf(c) : '#' + id}</span>
								<span className="dm">{c ? section.metaOf(c) : ''}</span>
								<span className="x" title="Remove" onClick={() => remove(id)}><window.Icon k="x" size={11} /></span>
							</div>
						);
					})}
				</div>
			)}
			{avail.length > 0 && (
				<div className="fld" style={{ marginTop: 14, maxWidth: 280 }}>
					<select className="sel" value="" onChange={(e) => add(e.target.value)}>
						<option value="">＋ {section.addLabel}</option>
						{avail.map((c) => <option key={c.id} value={c.id}>{section.labelOf(c)} · {section.metaOf(c)}</option>)}
					</select>
					<window.Caret />
				</div>
			)}
		</div>
	);
}

// ════════════════════════════════════════════════════════════════════════
//  TAGS — the showcase. Two integration modes (Search / Browse) over the same
//  applied-tag list. Shared by Items and Item Mods. Scales to hundreds of tags
//  while a single record holds only a couple dozen.
// ════════════════════════════════════════════════════════════════════════
function TagPill({ tag, onRemove, isNew }) {
	const c = window.tagColor(tag.tagCategoryId);
	return (
		<span className="tag-pill" style={{ color: c.fg, borderColor: c.bd, background: c.bg, boxShadow: isNew ? `inset 0 0 0 1px ${c.bd}` : 'none' }}>
			<span className="tdot" style={{ background: c.fg }} />
			{tag.name}
			{onRemove && <span className="x" onClick={(e) => { e.stopPropagation(); onRemove(); }}><window.Icon k="x" size={10} /></span>}
		</span>
	);
}

function TagsSection({ section, item, baseline, patch }) {
	const ids = item[section.itemsKey] || [];
	const baseIds = baseline ? (baseline[section.itemsKey] || []) : null;
	const [mode, setMode] = React.useState('browse');
	const [q, setQ] = React.useState('');
	const [cat, setCat] = React.useState(window.TAG_CATEGORIES[0].id);

	const add = (id) => { if (!ids.includes(id)) patch(item.id, (x) => x[section.itemsKey].push(id)); };
	const remove = (id) => patch(item.id, (x) => { x[section.itemsKey] = x[section.itemsKey].filter((k) => k !== id); });
	const toggle = (id) => (ids.includes(id) ? remove(id) : add(id));

	const applied = ids.map(window.tagById).filter(Boolean);
	const ql = q.trim().toLowerCase();

	return (
		<div>
			{/* applied tags + mode toggle */}
			<div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, gap: 16 }}>
				<span className="lbl" style={{ margin: 0 }}>Applied · {applied.length}</span>
				<div className="eswitch" style={{ borderRadius: 6 }}>
					<button className={mode === 'browse' ? 'active' : ''} onClick={() => setMode('browse')}><window.Icon k="box" size={13} />Browse</button>
					<button className={mode === 'search' ? 'active' : ''} onClick={() => setMode('search')}><window.Icon k="search" size={13} sw={1.5} />Search</button>
				</div>
			</div>
			<div className="applied-box">
				{applied.length === 0
					? <span style={{ fontFamily: 'var(--mono)', fontSize: 11.5, color: 'var(--text-muted)' }}>No tags yet — use {mode === 'search' ? 'search' : 'browse'} below to add some.</span>
					: <div style={{ display: 'flex', flexWrap: 'wrap', gap: 7 }}>
						{applied.map((t) => <TagPill key={t.id} tag={t} isNew={baseIds && !baseIds.includes(t.id)} onRemove={() => remove(t.id)} />)}
					</div>}
			</div>

			{mode === 'search' ? (
				<TagSearch ql={ql} q={q} setQ={setQ} ids={ids} add={add} />
			) : (
				<TagBrowse q={q} setQ={setQ} cat={cat} setCat={setCat} ids={ids} toggle={toggle} ql={ql} />
			)}
		</div>
	);
}

// ── Search mode: typeahead, results grouped by category ──
function TagSearch({ ql, q, setQ, ids, add }) {
	const matches = window.TAGS.filter((t) => !ids.includes(t.id) && (ql === '' || t.name.toLowerCase().includes(ql)));
	const groups = window.TAG_CATEGORIES.map((c) => ({ cat: c, tags: matches.filter((t) => t.tagCategoryId === c.id) })).filter((g) => g.tags.length);
	const shownTotal = Math.min(matches.length, 60);
	let budget = shownTotal;
	return (
		<div style={{ marginTop: 14 }}>
			<div className="search" style={{ maxWidth: 420 }}>
				<window.Icon k="search" sw={1.4} />
				<input className="inp" autoFocus placeholder="Search all tags…" value={q} onChange={(e) => setQ(e.target.value)} />
			</div>
			<div style={{ fontFamily: 'var(--mono)', fontSize: 10.5, color: 'var(--text-muted)', margin: '12px 0 8px' }}>
				{matches.length} match{matches.length === 1 ? '' : 'es'} across {groups.length} categor{groups.length === 1 ? 'y' : 'ies'} · click to add
			</div>
			<div className="tag-results">
				{groups.map((g) => {
					if (budget <= 0) return null;
					const take = g.tags.slice(0, budget);
					budget -= take.length;
					const c = window.tagColor(g.cat.id);
					return (
						<div key={g.cat.id} style={{ marginBottom: 12 }}>
							<div className="tag-group-head"><span className="tdot" style={{ background: c.fg }} />{g.cat.name}<span style={{ color: 'var(--text-muted)' }}>· {g.tags.length}</span></div>
							<div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
								{take.map((t) => (
									<button key={t.id} className="tag-add" style={{ color: c.fg, borderColor: c.bd }} onClick={() => add(t.id)}>
										<window.Icon k="plus" size={10} sw={1.8} />{t.name}
									</button>
								))}
							</div>
						</div>
					);
				})}
				{matches.length === 0 && <span style={{ fontFamily: 'var(--mono)', fontSize: 11.5, color: 'var(--text-muted)' }}>No matching tags.</span>}
			</div>
		</div>
	);
}

// ── Browse mode: category sidebar + toggleable chip grid ──
function TagBrowse({ q, setQ, cat, setCat, ids, toggle, ql }) {
	const searching = ql !== '';
	const shown = searching
		? window.TAGS.filter((t) => t.name.toLowerCase().includes(ql))
		: window.tagsByCategory(cat);
	const appliedInCat = (catId) => window.tagsByCategory(catId).filter((t) => ids.includes(t.id)).length;

	return (
		<div style={{ marginTop: 14, display: 'flex', gap: 16, border: '1px solid var(--border-subtle)', borderRadius: 6, overflow: 'hidden', minHeight: 280 }}>
			<div style={{ width: 196, flexShrink: 0, borderRight: '1px solid var(--border-subtle)', background: 'var(--surface)', overflowY: 'auto', maxHeight: 360 }}>
				{window.TAG_CATEGORIES.map((c) => {
					const cc = window.tagColor(c.id);
					const n = appliedInCat(c.id);
					const active = !searching && cat === c.id;
					return (
						<button key={c.id} onClick={() => { setCat(c.id); setQ(''); }}
							style={{ width: '100%', textAlign: 'left', background: active ? 'rgba(161,194,247,.08)' : 'transparent', border: 'none', borderBottom: '1px solid var(--border-subtle)', padding: '10px 14px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 9 }}>
							<span className="tdot" style={{ background: cc.fg }} />
							<span style={{ flex: 1, fontSize: 12.5, color: active ? 'var(--text-primary)' : 'var(--text-secondary)' }}>{c.name}</span>
							<span style={{ fontFamily: 'var(--mono)', fontSize: 10, color: 'var(--text-muted)' }}>{window.tagsByCategory(c.id).length}</span>
							{n > 0 && <span className="cat-count">{n}</span>}
						</button>
					);
				})}
			</div>
			<div style={{ flex: 1, minWidth: 0, padding: 14, overflowY: 'auto', maxHeight: 360 }}>
				<div className="search" style={{ maxWidth: 360, marginBottom: 14 }}>
					<window.Icon k="search" sw={1.4} />
					<input className="inp" placeholder="Filter tags…" value={q} onChange={(e) => setQ(e.target.value)} />
				</div>
				<div style={{ display: 'flex', flexWrap: 'wrap', gap: 7 }}>
					{shown.map((t) => {
						const c = window.tagColor(t.tagCategoryId);
						const on = ids.includes(t.id);
						return (
							<button key={t.id} className={'tag-toggle' + (on ? ' on' : '')} onClick={() => toggle(t.id)}
								style={on ? { color: c.fg, borderColor: c.bd, background: c.bg } : {}}>
								<span className="tdot" style={{ background: on ? c.fg : 'var(--text-muted)' }} />{t.name}
							</button>
						);
					})}
					{shown.length === 0 && <span style={{ fontFamily: 'var(--mono)', fontSize: 11.5, color: 'var(--text-muted)' }}>No tags here.</span>}
				</div>
			</div>
		</div>
	);
}

// ── Read-only Usage section (Tags): which items / mods reference this tag. ──
function UsageSection({ item }) {
	const u = window.tagUsage(item.id);
	const c = window.tagColor(item.tagCategoryId);
	const Group = ({ label, glyph, list, noun }) => (
		<div style={{ flex: '1 1 320px', minWidth: 280 }}>
			<div className="sec-title" style={{ marginBottom: 10 }}>
				<window.Icon k={glyph} size={13} stroke="var(--text-tertiary)" />{label}
				<span className="count-badge">{list.length}</span><span className="ln" />
			</div>
			{list.length === 0 ? (
				<span style={{ fontFamily: 'var(--mono)', fontSize: 11.5, color: 'var(--text-muted)' }}>No {noun} use this tag.</span>
			) : (
				<div style={{ display: 'flex', flexWrap: 'wrap', gap: 7 }}>
					{list.slice(0, 40).map((r) => (
						<span key={r.id} className="ov-tag" style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
							{r.rarityId && <span className="tdot" style={{ background: window.rarityColor(r.rarityId) }} />}
							{r.name}
						</span>
					))}
					{list.length > 40 && <span className="ov-tag" style={{ color: 'var(--text-muted)' }}>+{list.length - 40}</span>}
				</div>
			)}
		</div>
	);
	return (
		<div>
			<div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 18, padding: '12px 14px', border: '1px solid var(--border-subtle)', borderRadius: 6, background: 'rgba(255,255,255,.015)' }}>
				<span className="tdot" style={{ background: c.fg, width: 9, height: 9 }} />
				<span style={{ fontSize: 13.5 }}>Applied to <b>{u.items + u.mods}</b> record{u.items + u.mods === 1 ? '' : 's'}</span>
				<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--text-muted)', marginLeft: 'auto' }}>read-only · derived from item & mod tag lists</span>
			</div>
			<div style={{ display: 'flex', gap: 28, flexWrap: 'wrap' }}>
				<Group label="Items" glyph="box" list={u.itemList} noun="items" />
				<Group label="Item Mods" glyph="rune" list={u.modList} noun="mods" />
			</div>
		</div>
	);
}

function GenericSection({ section, item, baseline, patch }) {
	if (section.kind === 'fields') return <FieldsSection section={section} item={item} baseline={baseline} patch={patch} />;
	if (section.kind === 'table') return <TableSection section={section} item={item} baseline={baseline} patch={patch} />;
	if (section.kind === 'chips') return <ChipsSection section={section} item={item} baseline={baseline} patch={patch} />;
	if (section.kind === 'tags') return <TagsSection section={section} item={item} baseline={baseline} patch={patch} />;
	if (section.kind === 'usage') return <UsageSection item={item} />;
	return null;
}

// ── Generic checklist from an entity's sections. ──
function EntityChecklist({ entity, item }) {
	return (
		<div className="checklist">
			{entity.sections.filter((s) => s.complete).map((s) => {
				const done = s.complete(item);
				return (
					<div key={s.key} className={'check-item ' + (done ? 'done' : 'warn')}>
						<span className="box"><window.Icon k={done ? 'check' : 'warn'} size={11} sw={1.7} /></span>
						<span style={{ flex: 1 }}>{s.label}</span>
						<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: done ? 'var(--text-tertiary)' : 'var(--warning)' }}>{s.detail ? s.detail(item) : ''}</span>
					</div>
				);
			})}
		</div>
	);
}

Object.assign(window, { GenericSection, EntityChecklist });
