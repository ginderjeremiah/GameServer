/* sb-stat-final.jsx — the finalized Statistics screen.

   Unifies the two winning directions into one screen with a top-level view
   toggle:
     · "By statistic" (Direction B) — category tabs + stat cards.
     · "By entity"    (Direction D) — kind tabs + entity picker + dossier.
   The two cross-link: clicking an entity in a stat card pivots to that
   entity's dossier; clicking a stat in a dossier jumps back to that stat's
   category. Both sub-navigations use the shared UnderlineTabs so the screen
   reads as one system. Depends on sb-stat-data, sb-stat-shared, and
   StatPreviewCard from sb-stat-grouped. */

function StatisticsScreen({ accent = '#a1c2f7' }) {
  const [view, setView] = React.useState('stat');
  const [statCat, setStatCat] = React.useState('combat');
  const [entKind, setEntKind] = React.useState('enemy');
  const [entId, setEntId] = React.useState(ENEMIES[0].id);
  const [q, setQ] = React.useState('');

  const goEntity = (kind, id) => { setEntKind(kind); setEntId(id); setQ(''); setView('entity'); };
  const goStat = (cat) => { setStatCat(cat); setView('stat'); };
  const switchKind = (k) => { setEntKind(k); setEntId(ENTITY_KIND[k].list[0].id); setQ(''); };

  return (
    <StatBoard label="Statistics" sub="tracked totals, broken down by entity" accent={accent} scroll={false}>
      <ViewToggle view={view} onChange={setView} accent={accent} />
      {view === 'stat'
        ? <ByStatView accent={accent} cat={statCat} onCat={setStatCat} onPickEntity={goEntity} />
        : <ByEntityView accent={accent} kind={entKind} onKind={switchKind} id={entId} onId={setEntId}
            q={q} onQ={setQ} onPickStat={goStat} />}
    </StatBoard>
  );
}

/* ── high-level view toggle (filled segmented — distinct from the underline
   sub-tabs so the hierarchy is clear) ────────────────────────────────── */
function ViewToggle({ view, onChange, accent }) {
  const opts = [{ key: 'stat', label: 'By statistic' }, { key: 'entity', label: 'By entity' }];
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 15, flexWrap: 'wrap' }}>
      <div style={{ display: 'inline-flex', gap: 3, padding: 3, background: 'rgba(255,255,255,0.04)',
        border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6 }}>
        {opts.map((o) => {
          const on = o.key === view;
          return (
            <button key={o.key} onClick={() => onChange(o.key)}
              style={{ padding: '6px 16px', border: 'none', borderRadius: 4, cursor: 'pointer',
                background: on ? accent : 'transparent', color: on ? '#0b0c0f' : 'rgba(240,240,240,0.62)',
                fontFamily: 'Geist, sans-serif', fontSize: 12.5, fontWeight: on ? 600 : 500, transition: 'all 120ms' }}>
              {o.label}
            </button>
          );
        })}
      </div>
      <SMono size={9.5} ls={0.3} color="rgba(240,240,240,0.4)" style={{ textTransform: 'none' }}>
        {view === 'stat'
          ? 'Pick a statistic to see its per-entity breakdown — click any entity to pivot to it.'
          : 'Pick an entity to see every statistic that references it — click any stat to jump back.'}
      </SMono>
    </div>
  );
}

/* ── By statistic (Direction B) ──────────────────────────────────────── */
function ByStatView({ accent, cat, onCat, onPickEntity }) {
  const tabs = [
    { key: 'all', label: 'All', color: accent, count: STAT_TYPES.length },
    ...STAT_CATEGORIES.map((c) => ({ key: c.key, label: c.label, color: c.color, count: STAT_TYPES.filter((s) => s.cat === c.key).length })),
  ];
  const shown = STAT_TYPES.filter((s) => cat === 'all' || s.cat === cat);
  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      <UnderlineTabs tabs={tabs} active={cat} onChange={onCat} style={{ marginBottom: 16 }} />
      <div style={{ flex: 1, minHeight: 0, overflow: 'auto', paddingRight: 6 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14, paddingBottom: 8 }}>
          {shown.map((st) => <StatPreviewCard key={st.key} st={st} accent={accent} onPickEntity={onPickEntity} />)}
        </div>
      </div>
    </div>
  );
}

/* ── By entity (Direction D) ─────────────────────────────────────────── */
function ByEntityView({ accent, kind, onKind, id, onId, q, onQ, onPickStat }) {
  const list = ENTITY_KIND[kind].list;
  const filtered = list.filter((e) => e.name.toLowerCase().includes(q.toLowerCase()));
  const sel = entity(kind, id) || filtered[0] || list[0];
  const stats = statsForEntity(kind, sel.id);
  const kindTabs = Object.values(ENTITY_KIND).map((k) => ({
    key: k.key, label: k.plural, color: k.color, count: k.list.length,
    glyph: (on) => <EntityGlyph kind={k.key} size={13} color={on ? k.color : 'rgba(240,240,240,0.5)'} />,
  }));

  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      <UnderlineTabs tabs={kindTabs} active={kind} onChange={onKind} style={{ marginBottom: 16 }} />
      <div style={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: '286px 1fr', gap: 22 }}>
        {/* picker */}
        <div style={{ display: 'flex', flexDirection: 'column', minHeight: 0, borderRight: '1px solid rgba(255,255,255,0.06)', paddingRight: 16 }}>
          <div style={{ position: 'relative', marginBottom: 10 }}>
            <input value={q} onChange={(e) => onQ(e.target.value)} placeholder={`Search ${ENTITY_KIND[kind].plural.toLowerCase()}…`}
              style={{ width: '100%', background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.12)',
                borderRadius: 4, padding: '8px 10px 8px 28px', color: '#f0f0f0', fontFamily: 'Geist, sans-serif', fontSize: 12.5, outline: 'none' }} />
            <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="rgba(240,240,240,0.4)" strokeWidth="1.4"
              style={{ position: 'absolute', left: 9, top: 9 }}><circle cx="6" cy="6" r="4.2" /><path d="M9.2 9.2L12 12" strokeLinecap="round" /></svg>
          </div>
          <div style={{ overflow: 'auto', flex: 1, minHeight: 0 }}>
            {filtered.map((e) => {
              const on = e.id === sel.id;
              return (
                <button key={e.id} onClick={() => onId(e.id)}
                  style={{ width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px',
                    marginBottom: 2, borderRadius: 4, cursor: 'pointer', background: on ? `${accent}1a` : 'transparent',
                    border: `1px solid ${on ? accent + '66' : 'transparent'}`, transition: 'background 120ms' }}>
                  <EntityGlyph kind={kind} size={15} color={on ? '#fff' : undefined} />
                  <span style={{ fontSize: 13, color: on ? '#fff' : 'rgba(240,240,240,0.82)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', flex: 1 }}>{e.name}</span>
                  {kind === 'enemy' && e.boss && <SMono size={8} ls={0.8} color="#e8c878" style={{ border: '1px solid #e8c87840', borderRadius: 2, padding: '1px 4px' }}>Boss</SMono>}
                  {kind === 'zone' && <SMono size={9} ls={0.6} color="rgba(240,240,240,0.4)">Z{e.num}</SMono>}
                  {kind === 'skill' && <SMono size={8.5} ls={0.5} color="rgba(240,240,240,0.4)" style={{ textTransform: 'none' }}>{e.kind}</SMono>}
                </button>
              );
            })}
            {filtered.length === 0 && <SMono size={11} ls={0.4} color="rgba(240,240,240,0.4)" style={{ textTransform: 'none', padding: '10px' }}>No matches.</SMono>}
          </div>
        </div>

        {/* dossier */}
        <div style={{ overflow: 'auto', paddingRight: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 4 }}>
            <div style={{ width: 46, height: 46, borderRadius: 5, background: `${ENTITY_KIND[kind].color}14`,
              border: `1px solid ${ENTITY_KIND[kind].color}40`, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
              <EntityGlyph kind={kind} size={24} />
            </div>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 3 }}>
                <h2 style={{ margin: 0, fontSize: 24, fontWeight: 500, letterSpacing: -0.3 }}>{sel.name}</h2>
                <KindBadge kind={kind} />
                {kind === 'enemy' && sel.boss && <SMono size={9} ls={0.8} color="#e8c878" style={{ border: '1px solid #e8c87840', borderRadius: 2, padding: '2px 6px' }}>Boss</SMono>}
              </div>
              <SMono size={10} ls={0.6} color="rgba(240,240,240,0.45)" style={{ textTransform: 'none' }}>
                {kind === 'zone' ? `Zone ${sel.num}` : kind === 'skill' ? `${sel.kind} skill` : 'Enemy'} · appears in {stats.length} statistic{stats.length === 1 ? '' : 's'}
              </SMono>
            </div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12, marginTop: 18 }}>
            {stats.map((s) => <DossierTile key={s.stat.key} info={s} kind={kind} accent={accent} selId={sel.id} onPickStat={onPickStat} />)}
          </div>
        </div>
      </div>
    </div>
  );
}

function DossierTile({ info, kind, accent, selId, onPickStat }) {
  const { stat, value, rank, of } = info;
  const [hover, setHover] = React.useState(false);
  const rows = rowsForStat(stat.key);
  const maxVal = Math.max(...rows.map((r) => r.value), 1);
  const topName = rows[0].entity.name;
  const isTop = rows[0].entityId === selId;
  return (
    <div onClick={() => onPickStat(stat.cat)} onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{ background: hover ? 'rgba(255,255,255,0.045)' : 'rgba(255,255,255,0.025)',
        border: `1px solid ${hover ? 'rgba(255,255,255,0.16)' : 'rgba(255,255,255,0.08)'}`, borderRadius: 6,
        padding: '13px 15px 14px', cursor: 'pointer', transition: 'all 110ms' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 9 }}>
        <span style={{ display: 'flex', alignItems: 'center', gap: 7, minWidth: 0 }}>
          <span style={{ fontSize: 12.5, color: hover ? '#fff' : 'rgba(240,240,240,0.82)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{stat.name}</span>
          <CategoryTag cat={stat.cat} />
        </span>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: isTop ? accent : 'rgba(240,240,240,0.42)',
          border: `1px solid ${isTop ? accent + '55' : 'rgba(255,255,255,0.12)'}`, borderRadius: 8, padding: '1px 7px', whiteSpace: 'nowrap', flexShrink: 0 }}>
          #{rank} / {of}</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 9 }}>
        <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 22, fontWeight: 600, letterSpacing: -0.4, color: '#fff' }}>{fmtValue(value, stat.unit)}</span>
        <CompHint st={stat} />
      </div>
      <MiniBar frac={value / maxVal} color={ENTITY_KIND[kind].color} height={5} />
      <SMono size={8.5} ls={0.4} color="rgba(240,240,240,0.34)" style={{ textTransform: 'none', display: 'block', marginTop: 6 }}>
        {isTop ? 'Your highest for this stat' : `vs. ${topName} (${fmtValue(maxVal, stat.unit)})`}
      </SMono>
    </div>
  );
}

Object.assign(window, { StatisticsScreen });
