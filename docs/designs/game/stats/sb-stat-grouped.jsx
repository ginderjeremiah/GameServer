/* sb-stat-grouped.jsx — Direction B · "Category Tabs"
   Top-level segmented tabs split the 14 types into Combat / Survival /
   Exploration / Time (instead of 14 cramped tabs). The active category shows
   its stat types as cards, each with its headline and a compact top-entities
   preview. An "All" tab shows everything. */

function StatGrouped({ accent = '#a1c2f7' }) {
  const [tab, setTab] = React.useState('combat');
  const tabs = [
    { key: 'all', label: 'All', color: accent, count: STAT_TYPES.length },
    ...STAT_CATEGORIES.map((c) => ({ key: c.key, label: c.label, color: c.color, count: STAT_TYPES.filter((s) => s.cat === c.key).length })),
  ];
  const shown = STAT_TYPES.filter((s) => tab === 'all' || s.cat === tab);

  return (
    <StatBoard label="Statistics" sub="grouped by category" accent={accent}>
      <UnderlineTabs tabs={tabs} active={tab} onChange={setTab} style={{ marginBottom: 18 }} />

      {/* cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
        {shown.map((st) => <StatPreviewCard key={st.key} st={st} accent={accent} />)}
      </div>
    </StatBoard>
  );
}

function StatPreviewCard({ st, accent, onPickEntity }) {
  const rows = rowsForStat(st.key);
  const headline = statHeadline(st.key);
  const maxVal = Math.max(...rows.map((r) => r.value), 1);
  const sumVal = rows.reduce((a, r) => a + r.value, 0);
  const top = rows.slice(0, 4);
  const more = rows.length - top.length;
  const kindMeta = ENTITY_KIND[st.kind];

  return (
    <div style={{ background: 'rgba(255,255,255,0.025)', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6,
      padding: '15px 17px 16px', display: 'flex', flexDirection: 'column' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, marginBottom: 13 }}>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
            <span style={{ fontSize: 15, fontWeight: 500, color: '#f0f0f0' }}>{st.name}</span>
            <CategoryTag cat={st.cat} />
          </div>
          <KindBadge kind={st.kind} />
        </div>
        <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
          <div style={{ fontFamily: 'Geist, sans-serif', fontSize: 24, fontWeight: 600, letterSpacing: -0.6, color: '#fff' }}>{fmtValue(headline, st.unit)}</div>
          <CompHint st={st} />
        </div>
      </div>
      <div style={{ borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: 9 }}>
        {top.map((r) => (
          <PreviewRow key={r.entityId} r={r} st={st} maxVal={maxVal} kindMeta={kindMeta}
            onClick={onPickEntity ? () => onPickEntity(st.kind, r.entityId) : null} />
        ))}
        {more > 0 && <SMono size={9} ls={0.6} color="rgba(240,240,240,0.35)" style={{ textTransform: 'none', display: 'block', marginTop: 6, paddingLeft: 19 }}>+{more} more {kindMeta.plural.toLowerCase()}</SMono>}
      </div>
    </div>
  );
}

function PreviewRow({ r, st, maxVal, kindMeta, onClick }) {
  const [hover, setHover] = React.useState(false);
  const clickable = !!onClick;
  return (
    <div onClick={onClick || undefined} onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{ display: 'grid', gridTemplateColumns: '1fr 96px 70px', gap: 10, alignItems: 'center',
        padding: '4px 6px', margin: '0 -6px', borderRadius: 4, cursor: clickable ? 'pointer' : 'default',
        background: hover && clickable ? 'rgba(255,255,255,0.045)' : 'transparent', transition: 'background 110ms' }}>
      <span style={{ display: 'flex', alignItems: 'center', gap: 7, minWidth: 0 }}>
        <EntityGlyph kind={st.kind} size={12} />
        <span style={{ fontSize: 12, color: hover && clickable ? '#fff' : 'rgba(240,240,240,0.82)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.entity.name}</span>
        {clickable && hover && (
          <svg width="10" height="10" viewBox="0 0 12 12" fill="none" stroke={kindMeta.color} strokeWidth="1.5" style={{ flexShrink: 0 }}>
            <path d="M4 2.5L8 6l-4 3.5" strokeLinecap="round" strokeLinejoin="round" /></svg>
        )}
      </span>
      <MiniBar frac={r.value / maxVal} color={kindMeta.color} height={5} />
      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5, textAlign: 'right', color: 'rgba(240,240,240,0.85)' }}>{fmtValue(r.value, st.unit)}</span>
    </div>
  );
}

Object.assign(window, { StatGrouped, StatPreviewCard, PreviewRow });
