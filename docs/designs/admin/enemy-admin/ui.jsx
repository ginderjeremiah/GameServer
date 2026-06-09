// ─────────────────────────────────────────────────────────────────────────
//  Shared UI: icons, primitives, and the FOUR reusable section editors that
//  every direction composes. These are the "design-system" pieces — drop them
//  into any admin entity (skills, zones, items) and the same flow generalizes.
// ─────────────────────────────────────────────────────────────────────────

// ── Icons ──
const PATHS = {
	skull: 'M8 1.5c3 0 5 2.2 5 5.2 0 1.8-.8 2.7-.8 3.8 0 .8-.6 1.2-1.4 1.2H5.2c-.8 0-1.4-.4-1.4-1.2 0-1.1-.8-2-.8-3.8 0-3 2-5.2 5-5.2z',
	box: 'M2.5 5.2L8 2.5l5.5 2.7v5.6L8 13.5l-5.5-2.7z|M2.5 5.2L8 8l5.5-2.8M8 8v5.5',
	bolt: 'M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z',
	map: 'M2 4l4-1.5 4 1.5 4-1.5v9.5L10 13.5 6 12 2 13.5z|M6 2.5V12M10 4v9.5',
	bars: 'M3 13.5V8.5M7 13.5V3.5M11 13.5V6.5',
	rune: 'M8 2v12M4 5l8 6M12 5l-8 6',
	pin: 'M8 14s4.2-4 4.2-7A4.2 4.2 0 008 2.8 4.2 4.2 0 003.8 7c0 3 4.2 7 4.2 7z',
	multiply: 'M4 4l8 8M12 4l-8 8',
	tag: 'M2.5 2.5h5L13 8l-5 5-5.5-5.5z',
	plus: 'M8 3v10M3 8h10',
	chevR: 'M6 3.5L10.5 8L6 12.5',
	chevD: 'M3.5 6L8 10.5L12.5 6',
	check: 'M3 7.3L6 10.2L11 4',
	x: 'M4 4l8 8M12 4l-8 8',
	warn: 'M8 1.8L15 14H1zM8 6v3.5',
	search: 'M7 1.5a5.5 5.5 0 104 9.4 5.5 5.5 0 00-4-9.4zM11.2 11.2L14.5 14.5',
	wand: 'M3 13l7-7M11 2l.6 1.6L13 4.2l-1.4.6L11 6.4l-.6-1.6L9 4.2l1.4-.6zM4 3l.4 1L5.4 4.4l-1 .4L4 5.8l-.4-1L2.6 4.4l1-.4z',
	back: 'M9.5 3.5L5 8l4.5 4.5M5 8h8',
	target: 'M8 2a6 6 0 100 12 6 6 0 000-12z|M8 5a3 3 0 100 6 3 3 0 000-6z|M8 7.4a0.6 0.6 0 100 1.2 0.6 0.6 0 000-1.2z',
	trophy: 'M5 3h6v2.6a3 3 0 01-6 0z|M5 3.6C3.1 3.6 3.1 6 5.2 6M11 3.6C12.9 3.6 12.9 6 10.8 6|M8 8.5v2.2M5.6 12.8h4.8M6.7 10.8h2.6',
	gauge: 'M2.9 11.6a5.1 5.1 0 0110.2 0|M8 11.6L10.4 8.2|M8 11.6a0.5 0.5 0 100 .1z',
	flag: 'M4.5 14V2.5M4.5 3.2l6.4 .6L9.4 6.2l1.5 2.5-6.4-.6',
	gift: 'M3.3 7h9.4V13H3.3z|M2.6 7h10.8v2.2H2.6zM8 7v6|M8 7C7.3 4.3 4.7 4.7 5.2 7M8 7C8.7 4.3 11.3 4.7 10.8 7'
};
function Icon({ k, size = 14, className = '', stroke = 'currentColor', sw = 1.5 }) {
	const segs = (PATHS[k] || '').split('|');
	const dot = k === 'warn';
	return (
		<svg className={'ic ' + className} width={size} height={size} viewBox="0 0 16 16" fill="none"
			stroke={stroke} strokeWidth={sw} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
			{segs.map((d, i) => <path key={i} d={d} />)}
			{dot && <circle cx="8" cy="11.6" r="0.4" fill={stroke} stroke="none" />}
			{k === 'skull' && <><circle cx="6" cy="7.2" r="1.1" /><circle cx="10" cy="7.2" r="1.1" /></>}
		</svg>
	);
}

function Caret() {
	return <svg className="sel-caret" width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" strokeWidth="1.4"><path d="M2 3.5L5 6.5L8 3.5" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function BossTag() { return <span className="boss-tag"><Icon k="skull" size={9} sw={1.6} />Boss</span>; }

function WarnChip({ text }) { return <span className="warn-chip"><Icon k="warn" size={12} sw={1.4} />{text}</span>; }

// One-line stat summary used in lists/tables.
function MetaLine({ e }) {
	return (
		<span className="meta">
			<span><b>{e.attrs.length}</b> attr</span>
			<span><b>{e.skills.length}</b> skill</span>
			<span><b>{e.spawns.length}</b> zone</span>
		</span>
	);
}

function CompletenessMeter({ e }) {
	const c = window.completeness(e);
	const segs = [c.identity, c.attrs, c.skills, c.spawns];
	return <div className="cmeter">{segs.map((on, i) => <div key={i} className={'seg' + (on ? ' on' : '')} />)}</div>;
}

// ════════════════════════════════════════════════════════════════════════
//  REUSABLE SECTION EDITORS
// ════════════════════════════════════════════════════════════════════════

// ── Identity: name + isBoss. ──
function IdentityFields({ e, patch }) {
	return (
		<div style={{ display: 'flex', gap: 18, flexWrap: 'wrap', alignItems: 'flex-end' }}>
			<div className="fld" style={{ flex: '1 1 280px' }}>
				<label className="lbl">Enemy Name</label>
				<input className="inp" value={e.name} placeholder="Name this enemy…"
					onChange={(ev) => patch(e.id, (x) => { x.name = ev.target.value; })} />
			</div>
			<div className="fld">
				<label className="lbl">Classification</label>
				<div className={'toggle' + (e.isBoss ? ' on' : '')} style={{ height: 38 }}
					onClick={() => patch(e.id, (x) => { x.isBoss = !x.isBoss; })}>
					<span className="track"><span className="knob" /></span>
					<span className="tg-label">{e.isBoss ? 'Boss enemy' : 'Standard enemy'}</span>
				</div>
			</div>
		</div>
	);
}

// ── Attribute distribution: attribute / base / per-level table. ──
function AttrEditor({ e, patch }) {
	const used = new Set(e.attrs.map((a) => a.attributeId));
	const firstFree = (window.ATTRIBUTES.find((a) => !used.has(a.id)) || window.ATTRIBUTES[0]).id;
	const add = () => patch(e.id, (x) => x.attrs.push({ attributeId: firstFree, base: 0, perLevel: 0 }));
	if (e.attrs.length === 0) {
		return <EmptySection icon="bars" title="No attributes set" sub="This enemy has no stat distribution yet." onAdd={add} addLabel="Add attribute" />;
	}
	return (
		<div>
			<table className="mtable">
				<thead><tr><th>Attribute</th><th className="r">Base</th><th className="r">Per Level</th><th className="c" style={{ width: 40 }}></th></tr></thead>
				<tbody>
					{e.attrs.map((a, i) => (
						<tr key={i}>
							<td style={{ minWidth: 190 }}>
								<div className="fld">
									<select className="sel" value={a.attributeId}
										onChange={(ev) => patch(e.id, (x) => { x.attrs[i].attributeId = +ev.target.value; })}>
										{window.ATTRIBUTES.map((at) => <option key={at.id} value={at.id}>{at.name}</option>)}
									</select>
									<Caret />
								</div>
							</td>
							<td className="r" style={{ width: 110 }}>
								<input className="inp num" value={a.base}
									onChange={(ev) => patch(e.id, (x) => { x.attrs[i].base = +ev.target.value || 0; })} />
							</td>
							<td className="r" style={{ width: 110 }}>
								<input className="inp num" value={a.perLevel}
									onChange={(ev) => patch(e.id, (x) => { x.attrs[i].perLevel = +ev.target.value || 0; })} />
							</td>
							<td className="c">
								<button className="row-x" title="Remove" onClick={() => patch(e.id, (x) => x.attrs.splice(i, 1))}><Icon k="x" size={11} /></button>
							</td>
						</tr>
					))}
				</tbody>
			</table>
			<button className="btn sm" style={{ marginTop: 12 }} onClick={add}><Icon k="plus" size={12} />Add attribute</button>
		</div>
	);
}

// ── Skill pool: chips + add-from-pool. ──
function SkillEditor({ e, patch }) {
	const avail = window.SKILLS.filter((s) => !e.skills.includes(s.id));
	const add = (id) => { if (id) patch(e.id, (x) => x.skills.push(+id)); };
	return (
		<div>
			{e.skills.length === 0 ? (
				<EmptySection icon="rune" title="No skills in pool" sub="Enemies with no skills can't act in battle." />
			) : (
				<div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
					{e.skills.map((sid) => {
						const s = window.skillById(sid);
						return (
							<div className="skill-chip" key={sid}>
								<span className="nm">{s ? s.name : '#' + sid}</span>
								<span className="dm">{s ? s.dmg + ' dmg' : ''}</span>
								<span className="x" title="Remove" onClick={() => patch(e.id, (x) => { x.skills = x.skills.filter((k) => k !== sid); })}><Icon k="x" size={11} /></span>
							</div>
						);
					})}
				</div>
			)}
			{avail.length > 0 && (
				<div className="fld" style={{ marginTop: 14, maxWidth: 260 }}>
					<select className="sel" value="" onChange={(ev) => add(ev.target.value)}>
						<option value="">＋ Add skill from pool…</option>
						{avail.map((s) => <option key={s.id} value={s.id}>{s.name} · {s.dmg} dmg</option>)}
					</select>
					<Caret />
				</div>
			)}
		</div>
	);
}

// ── Zone spawns: zone / weight / derived spawn-share. ──
function SpawnEditor({ e, patch }) {
	const total = e.spawns.reduce((s, z) => s + (z.weight || 0), 0) || 1;
	const used = new Set(e.spawns.map((z) => z.zoneId));
	const firstFree = (window.ZONES.find((z) => !used.has(z.id)) || window.ZONES[0]).id;
	const add = () => patch(e.id, (x) => x.spawns.push({ zoneId: firstFree, weight: 5 }));
	if (e.spawns.length === 0) {
		return <EmptySection icon="pin" title="Not assigned to any zone" sub="This enemy will never spawn in the world." onAdd={add} addLabel="Assign to zone" />;
	}
	return (
		<div>
			<table className="mtable">
				<thead><tr><th>Zone</th><th className="r">Weight</th><th style={{ width: 130 }}>Spawn share</th><th className="c" style={{ width: 40 }}></th></tr></thead>
				<tbody>
					{e.spawns.map((z, i) => {
						const zone = window.zoneById(z.zoneId);
						const pct = Math.round((z.weight / total) * 100);
						return (
							<tr key={i}>
								<td style={{ minWidth: 200 }}>
									<div className="fld">
										<select className="sel" value={z.zoneId}
											onChange={(ev) => patch(e.id, (x) => { x.spawns[i].zoneId = +ev.target.value; })}>
											{window.ZONES.map((zn) => <option key={zn.id} value={zn.id}>{zn.name} · L{zn.levelMin}–{zn.levelMax}</option>)}
										</select>
										<Caret />
									</div>
								</td>
								<td className="r" style={{ width: 100 }}>
									<input className="inp num" value={z.weight}
										onChange={(ev) => patch(e.id, (x) => { x.spawns[i].weight = +ev.target.value || 0; })} />
								</td>
								<td>
									<div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
										<div className="share-bar" style={{ flex: 1 }}><span style={{ width: pct + '%' }} /></div>
										<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: 'var(--text-tertiary)', minWidth: 30, textAlign: 'right' }}>{pct}%</span>
									</div>
								</td>
								<td className="c">
									<button className="row-x" title="Remove" onClick={() => patch(e.id, (x) => x.spawns.splice(i, 1))}><Icon k="x" size={11} /></button>
								</td>
							</tr>
						);
					})}
				</tbody>
			</table>
			<button className="btn sm" style={{ marginTop: 12 }} onClick={add}><Icon k="plus" size={12} />Assign zone</button>
		</div>
	);
}

function EmptySection({ icon, title, sub, onAdd, addLabel }) {
	return (
		<div className="empty-state">
			<div className="glyph"><Icon k={icon} size={20} /></div>
			<div><div className="et">{title}</div><div className="es">{sub}</div></div>
			{onAdd && <button className="btn sm" onClick={onAdd}><Icon k="plus" size={12} />{addLabel}</button>}
		</div>
	);
}

// ── Completeness checklist (used in summary panels + review). ──
function Checklist({ e }) {
	const c = window.completeness(e);
	const items = [
		['identity', c.identity, 'Identity', e.name ? `“${e.name}”` : 'Name required'],
		['attrs', c.attrs, 'Attributes', c.attrs ? `${e.attrs.length} defined` : 'None set'],
		['skills', c.skills, 'Skills', c.skills ? `${e.skills.length} in pool` : 'None assigned'],
		['spawns', c.spawns, 'Spawns', c.spawns ? `${e.spawns.length} zone${e.spawns.length > 1 ? 's' : ''}` : 'No zones']
	];
	return (
		<div className="checklist">
			{items.map(([key, done, label, detail]) => (
				<div key={key} className={'check-item ' + (done ? 'done' : 'warn')}>
					<span className="box"><Icon k={done ? 'check' : 'warn'} size={11} sw={1.7} /></span>
					<span style={{ flex: 1 }}>{label}</span>
					<span style={{ fontFamily: 'var(--mono)', fontSize: 11, color: done ? 'var(--text-tertiary)' : 'var(--warning)' }}>{detail}</span>
				</div>
			))}
		</div>
	);
}

// ── Shared save bar. ──
function SaveBar({ store }) {
	const { dirty, saved, save, discard } = store;
	return (
		<div className="save-bar">
			<div className="save-summary">
				{saved ? (
					<span className="saved"><Icon k="check" size={13} sw={1.7} />Changes saved</span>
				) : dirty === 0 ? (
					<span>No unsaved changes</span>
				) : (
					<span className="pending">{dirty} unsaved {dirty === 1 ? 'change' : 'changes'}</span>
				)}
			</div>
			<div className="save-actions">
				<button className="btn" disabled={dirty === 0} onClick={discard}>Discard</button>
				<button className="btn primary" disabled={dirty === 0} onClick={save}>Save Changes</button>
			</div>
		</div>
	);
}

// Section metadata reused across directions.
const SECTIONS = [
	{ key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name & classification' },
	{ key: 'attrs', label: 'Attributes', glyph: 'bars', desc: 'Stat distribution per level' },
	{ key: 'skills', label: 'Skills', glyph: 'rune', desc: 'Skill pool used in battle' },
	{ key: 'spawns', label: 'Spawns', glyph: 'pin', desc: 'Zones this enemy appears in' }
];

function SectionEditor({ which, e, patch }) {
	if (which === 'identity') return <IdentityFields e={e} patch={patch} />;
	if (which === 'attrs') return <AttrEditor e={e} patch={patch} />;
	if (which === 'skills') return <SkillEditor e={e} patch={patch} />;
	if (which === 'spawns') return <SpawnEditor e={e} patch={patch} />;
	return null;
}

Object.assign(window, {
	Icon, Caret, BossTag, WarnChip, MetaLine, CompletenessMeter,
	IdentityFields, AttrEditor, SkillEditor, SpawnEditor, EmptySection, Checklist,
	SaveBar, SECTIONS, SectionEditor
});
