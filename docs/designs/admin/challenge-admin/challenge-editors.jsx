// ─────────────────────────────────────────────────────────────────────────
//  Challenge section editors. Two condition-builder explorations (Guided +
//  Compact) over the same data, plus the reward editor with HARD exclusivity
//  (items/mods already unlocked by another challenge are disabled). Dirty
//  state is computed against the saved baseline, matching the rest of the
//  Workbench.
// ─────────────────────────────────────────────────────────────────────────

const eqv = (a, b) => JSON.stringify(a) === JSON.stringify(b);

// Decimal-safe numeric input (keeps in-progress text so "100" / "0." don't reset).
function ChNum({ value, onChange, className, allowNeg, style }) {
	const [text, setText] = React.useState(() => String(value ?? 0));
	React.useEffect(() => {
		if (text.trim() === '' || text === '-' || text === '.') return;
		if (parseFloat(text) !== value) setText(String(value));
	}, [value]); // eslint-disable-line
	const pattern = allowNeg ? /^-?\d*\.?\d*$/ : /^\d*\.?\d*$/;
	const handle = (e) => {
		const raw = e.target.value;
		if (raw !== '' && !pattern.test(raw)) return;
		setText(raw);
		if (raw === '' || raw === '-' || raw === '.') { onChange(0); return; }
		const n = parseFloat(raw);
		if (!isNaN(n)) onChange(n);
	};
	return <input className={className} style={style} value={text} inputMode="decimal" onChange={handle} />;
}

// ── mutators: type now fully determines the statistic, entity type, and
//    whether a target is even possible. Changing the type re-derives them. ──
function applyType(patch, c, typeId) {
	patch(c.id, (x) => {
		x.type = typeId;
		const st = window.typeStat(typeId);
		x.statisticType = st ? st.id : null;
		x.entityType = st ? st.entityType : 0;
		x.targetEntityId = null;
	});
}

// ═══ shared field bits ════════════════════════════════════════════════════

// Plain-language preview of the assembled objective. Every type is now tracked
// (statistic, or the player's level for Level Reached); only the goal comparison
// is worth surfacing, and only when it's the unusual "at most" case.
function SentenceBox({ c }) {
	const atMost = window.goalComparisonOf(c.type) === 'AtMost';
	return (
		<div className="ch-sentence">
			<span className="ch-sentence-eye">Players see</span>
			<span className="ch-sentence-text">{window.challengeSentence(c)}</span>
			{atMost && <span className="ch-flag info"><window.Icon k="gauge" size={11} sw={1.5} />completes at or under goal</span>}
		</div>
	);
}

// Read-only tracked-value readout. What a challenge tracks is defined by its
// type (no longer editable here): a statistic bucketed by an entity dimension,
// or the player's level for Level Reached.
function StatReadout({ c }) {
	const kind = window.trackedKind(c); // 'stat' | 'level' | 'none'
	const et = c.entityType;
	return (
		<div className="fld" style={{ minWidth: 248, flex: '1 1 248px' }}>
			<label className="lbl">Tracked Value</label>
			<div className={'ch-stat-readout' + (kind === 'none' ? ' none' : '')}>
				<window.Icon k={kind === 'level' ? 'bars' : kind === 'none' ? 'target' : 'gauge'} size={13} stroke={kind === 'none' ? 'var(--text-tertiary)' : 'var(--accent)'} />
				<span className={'nm' + (kind === 'none' ? ' muted' : '')}>{window.trackedLabel(c)}</span>
				{kind === 'stat' && <span className="ch-stat-ent">{et !== 0 ? window.entityTypeName(et) : 'global'}</span>}
				{kind === 'level' && <span className="ch-stat-ent">player</span>}
			</div>
		</div>
	);
}

// Scope: global vs a specific entity of the statistic's entity type.
function ScopeField({ c, baseline, patch, compact }) {
	const et = c.entityType;
	const catalog = window.entityCatalog(et);
	const dirty = baseline ? !eqv(c.targetEntityId, baseline.targetEntityId) : false;
	const specific = c.targetEntityId != null;

	if (et === 0) {
		return (
			<div className="fld" style={{ minWidth: compact ? 220 : 260, flex: compact ? '1 1 220px' : 'none' }}>
				<label className="lbl">Scope</label>
				<div className="ch-scope-global">
					<window.Icon k="map" size={13} stroke="var(--text-tertiary)" />
					<span>{window.trackedKind(c) === 'level' ? 'Tracked from player level — no target entity' : 'Tracked globally — this statistic has no target entity'}</span>
				</div>
			</div>
		);
	}

	return (
		<div className="fld" style={{ minWidth: compact ? 260 : 300, flex: compact ? '1 1 260px' : 'none' }}>
			<label className="lbl" style={dirty ? { color: 'var(--warning)' } : null}>Scope · {window.entityTypeName(et)}</label>
			<div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
				<div className="eswitch">
					<button className={!specific ? 'active' : ''} onClick={() => patch(c.id, (x) => { x.targetEntityId = null; })}>Global</button>
					<button className={specific ? 'active' : ''} onClick={() => patch(c.id, (x) => { x.targetEntityId = catalog[0] ? catalog[0].id : null; })}>Specific</button>
				</div>
				{specific && (
					<div className="fld" style={{ position: 'relative', minWidth: 200, flex: 1 }}>
						<select className={'sel' + (dirty ? ' dirty' : '')} value={c.targetEntityId}
							onChange={(e) => patch(c.id, (x) => { x.targetEntityId = +e.target.value; })}>
							{catalog.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
						</select>
						<window.Caret />
					</div>
				)}
			</div>
			{!compact && (
				<div className="ch-note">{specific
					? `Counts only ${window.entityTypeName(et).toLowerCase()}-specific progress for the chosen target.`
					: `Counts ${window.trackedLabel(c)} across all ${window.entityTypeName(et).toLowerCase()}s.`}</div>
			)}
		</div>
	);
}

// Goal amount with a statistic-derived unit.
function GoalField({ c, baseline, patch, compact }) {
	const dirty = baseline ? !eqv(c.progressGoal, baseline.progressGoal) : false;
	const unit = window.goalUnit(c);
	const isMs = unit === 'ms';
	const label = isMs ? 'ms' : unit;
	const pad = Math.round(label.length * 6.6 + 20);
	const atMost = window.goalComparisonOf(c.type) === 'AtMost';
	return (
		<div className="fld" style={{ width: compact ? 210 : 220 }}>
			<label className="lbl">Goal {isMs ? '(time)' : 'amount'}{atMost ? ' · at most' : ''}</label>
			<div className="num-unit">
				<ChNum className={'inp num' + (dirty ? ' dirty' : '')} style={{ paddingRight: pad }} value={c.progressGoal} onChange={(v) => patch(c.id, (x) => { x.progressGoal = v; })} />
				<span className="suffix">{label}</span>
			</div>
			{dirty && <span className="dirty-dot" />}
		</div>
	);
}

// ═══ Condition builder — Compact form (statistic is type-driven & read-only) ═
function ChallengeCondition({ item: c, baseline, patch }) {
	const typeDirty = baseline ? !eqv(c.type, baseline.type) : false;
	return (
		<div className="ch-compact">
			<div className="ch-compact-grid">
				<div className="fld" style={{ width: 220 }}>
					<label className="lbl">Objective Type</label>
					<div style={{ position: 'relative' }}>
						<select className={'sel' + (typeDirty ? ' dirty' : '')} value={c.type} onChange={(e) => applyType(patch, c, +e.target.value)}>
							{window.CHALLENGE_TYPES.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
						</select>
						<window.Caret />
						{typeDirty && <span className="dirty-dot" />}
					</div>
				</div>
				<StatReadout c={c} />
				<GoalField c={c} baseline={baseline} patch={patch} compact />
				<div style={{ flexBasis: '100%', height: 0 }} />
				<ScopeField c={c} baseline={baseline} patch={patch} compact />
			</div>
			<div className="ch-grid-cap">
				<window.Icon k="bolt" size={11} stroke="var(--text-muted)" />
				Statistic &amp; entity scope are defined by the challenge type.
			</div>
			<SentenceBox c={c} />
		</div>
	);
}

// ═══ Reward section — hard exclusivity ═════════════════════════════════════
function RewardSlot({ kind, label, valueId, ownerName, color, sub, dirty, onClear, onOpen, open }) {
	return (
		<div className={'ch-reward-slot' + (valueId != null ? ' filled' : '') + (open ? ' open' : '')}>
			<div className="ch-reward-head">
				<span className="ch-reward-ic" style={valueId != null && color ? { color, borderColor: color } : null}>
					<window.Icon k={kind === 'item' ? 'box' : 'rune'} size={15} />
				</span>
				<div style={{ flex: 1, minWidth: 0 }}>
					<div className="ch-reward-label">{label}{dirty && <span style={{ width: 5, height: 5, borderRadius: '50%', background: 'var(--warning)', boxShadow: '0 0 5px var(--warning)', display: 'inline-block', marginLeft: 7 }} />}</div>
					{valueId != null
						? <div className="ch-reward-name" style={color ? { color } : null}>{kind === 'item' ? window.itemRecName(valueId) : window.itemModName(valueId)}<span className="ch-reward-sub">{sub}</span></div>
						: <div className="ch-reward-empty">Nothing unlocked</div>}
				</div>
				{valueId != null
					? <button className="row-x" title="Clear reward" onClick={onClear}><window.Icon k="x" size={11} /></button>
					: null}
			</div>
			<button className={'btn sm' + (valueId != null ? '' : ' primary')} style={{ marginTop: 12 }} onClick={onOpen}>
				<window.Icon k={open ? 'chevD' : 'plus'} size={12} />{valueId != null ? 'Change…' : `Choose ${kind === 'item' ? 'item' : 'mod'}…`}
			</button>
		</div>
	);
}

function RewardPicker({ kind, records, currentId, claimed, onPick, onClose }) {
	const [q, setQ] = React.useState('');
	const ql = q.trim().toLowerCase();
	const matches = records.filter((r) => ql === '' || r.name.toLowerCase().includes(ql));
	const availableCount = records.filter((r) => !claimed.has(r.id) || r.id === currentId).length;

	return (
		<div className="ch-picker">
			<div className="ch-picker-bar">
				<div className="search" style={{ flex: 1 }}>
					<window.Icon k="search" sw={1.4} />
					<input className="inp" autoFocus placeholder={`Search ${kind === 'item' ? 'items' : 'item mods'}…`} value={q} onChange={(e) => setQ(e.target.value)} />
				</div>
				<span className="ch-picker-count">{availableCount} of {records.length} unclaimed</span>
				<button className="row-x" title="Close" onClick={onClose}><window.Icon k="x" size={11} /></button>
			</div>
			<div className="ch-picker-list">
				{matches.slice(0, 80).map((r) => {
					const owner = claimed.get(r.id);
					const isClaimed = !!owner && r.id !== currentId;
					const isCurrent = r.id === currentId;
					const color = kind === 'item' ? window.rarityColor(r.rarityId) : 'var(--accent)';
					const tag = kind === 'item' ? window.rarityName(r.rarityId) : (window.MOD_TYPES.find((t) => t.id === r.itemModTypeId) || {}).name;
					return (
						<button key={r.id} className={'ch-picker-row' + (isClaimed ? ' claimed' : '') + (isCurrent ? ' current' : '')}
							disabled={isClaimed} onClick={() => !isClaimed && onPick(r.id)}
							title={isClaimed ? `Already unlocked by “${owner.name}”` : undefined}>
							<span className="tdot" style={{ background: isClaimed ? 'var(--text-muted)' : color }} />
							<span className="ch-picker-nm">{r.name}</span>
							<span className="rare-tag" style={{ color: isClaimed ? 'var(--text-muted)' : color, borderColor: 'currentColor' }}>{tag}</span>
							<span style={{ flex: 1 }} />
							{isCurrent && <span className="ch-picker-state current">selected</span>}
							{isClaimed && <span className="ch-picker-state"><window.Icon k="x" size={9} sw={2} />{owner.name}</span>}
						</button>
					);
				})}
				{matches.length === 0 && <div style={{ fontFamily: 'var(--mono)', fontSize: 11.5, color: 'var(--text-muted)', padding: '14px 4px' }}>No matches.</div>}
			</div>
		</div>
	);
}

function ChallengeReward({ item, baseline, patch, allItems }) {
	const [open, setOpen] = React.useState(null); // 'item' | 'mod' | null
	React.useEffect(() => { setOpen(null); }, [item.id]);

	const claimedItems = window.claimedItemMap(allItems, item.id);
	const claimedMods = window.claimedModMap(allItems, item.id);
	const none = item.rewardItemId == null && item.rewardItemModId == null;

	const itemDirty = baseline ? !eqv(item.rewardItemId, baseline.rewardItemId) : false;
	const modDirty = baseline ? !eqv(item.rewardItemModId, baseline.rewardItemModId) : false;

	const setItem = (id) => { patch(item.id, (x) => { x.rewardItemId = id; }); setOpen(null); };
	const setMod = (id) => { patch(item.id, (x) => { x.rewardItemModId = id; }); setOpen(null); };

	return (
		<div>
			<div className="ch-reward-intro">
				<window.Icon k="gift" size={13} stroke="var(--text-tertiary)" />
				<span>Grant an item, a mod, or both. Each unlock belongs to exactly one challenge — already-claimed rewards are disabled below.</span>
			</div>

			<div className="ch-reward-grid">
				<RewardSlot kind="item" label="Item Reward" valueId={item.rewardItemId}
					color={item.rewardItemId != null ? window.rarityColor(window.itemRarityId(item.rewardItemId)) : null}
					sub={item.rewardItemId != null ? window.rarityName(window.itemRarityId(item.rewardItemId)) : ''}
					dirty={itemDirty} open={open === 'item'}
					onClear={() => setItem(null)} onOpen={() => setOpen(open === 'item' ? null : 'item')} />
				<RewardSlot kind="mod" label="Item Mod Reward" valueId={item.rewardItemModId}
					color={item.rewardItemModId != null ? 'var(--accent)' : null}
					sub={item.rewardItemModId != null ? window.itemModTypeName(item.rewardItemModId) : ''}
					dirty={modDirty} open={open === 'mod'}
					onClear={() => setMod(null)} onOpen={() => setOpen(open === 'mod' ? null : 'mod')} />
			</div>

			{open === 'item' && <RewardPicker kind="item" records={window.ITEM_RECORDS} currentId={item.rewardItemId} claimed={claimedItems} onPick={setItem} onClose={() => setOpen(null)} />}
			{open === 'mod' && <RewardPicker kind="mod" records={window.ITEMMOD_RECORDS} currentId={item.rewardItemModId} claimed={claimedMods} onPick={setMod} onClose={() => setOpen(null)} />}

			{none && (
				<div className="warn-chip" style={{ marginTop: 16 }}>
					<window.Icon k="warn" size={12} sw={1.4} />This challenge unlocks nothing — players get no reward for completing it.
				</div>
			)}
		</div>
	);
}

// ═══ dispatcher used by the challenge-aware Workbench ══════════════════════
function ChallengeSection({ section, item, baseline, patch, allItems }) {
	if (section.kind === 'fields') return window.GenericSection({ section, item, baseline, patch });
	if (section.kind === 'challenge-condition') return <ChallengeCondition item={item} baseline={baseline} patch={patch} />;
	if (section.kind === 'challenge-reward') return <ChallengeReward item={item} baseline={baseline} patch={patch} allItems={allItems} />;
	return window.GenericSection({ section, item, baseline, patch });
}

Object.assign(window, { ChallengeSection, ChallengeCondition, ChallengeReward });
