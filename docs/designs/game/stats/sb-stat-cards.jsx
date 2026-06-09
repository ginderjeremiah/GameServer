/* sb-stat-cards.jsx — Direction C · "Scroll"
   No navigation at all — one honest scroll. Stat types are grouped under
   category section headers; each type is a full-width card with its complete
   entity breakdown laid out inline (two columns of rows when there are many
   entities). Best when you just want to read everything top to bottom. */

function StatCards({ accent = '#a1c2f7' }) {
  return (
    <StatBoard label="Statistics" sub="everything, top to bottom" accent={accent}>
      {STAT_CATEGORIES.map((cat) => (
        <div key={cat.key} style={{ marginBottom: 26 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
            <span style={{ width: 8, height: 8, background: cat.color, borderRadius: 1, boxShadow: `0 0 8px ${cat.color}88` }} />
            <SMono size={11} ls={1.8} color="rgba(240,240,240,0.78)">{cat.label}</SMono>
            <span style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.06)' }} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {STAT_TYPES.filter((s) => s.cat === cat.key).map((st) => <FullStatCard key={st.key} st={st} accent={accent} />)}
          </div>
        </div>
      ))}
    </StatBoard>
  );
}

function FullStatCard({ st, accent }) {
  const rows = rowsForStat(st.key);
  const headline = statHeadline(st.key);
  const maxVal = Math.max(...rows.map((r) => r.value), 1);
  const sumVal = rows.reduce((a, r) => a + r.value, 0);
  const kindMeta = ENTITY_KIND[st.kind];
  const twoCol = rows.length > 5;

  return (
    <div style={{ background: 'rgba(255,255,255,0.025)', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6,
      padding: '15px 18px 16px' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 14, marginBottom: 13 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginBottom: 4 }}>
            <span style={{ fontSize: 15.5, fontWeight: 500, color: '#f0f0f0' }}>{st.name}</span>
            <KindBadge kind={st.kind} />
          </div>
          <span style={{ fontSize: 12, color: 'rgba(240,240,240,0.5)' }}>{st.desc}</span>
        </div>
        <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
          <div style={{ fontFamily: 'Geist, sans-serif', fontSize: 25, fontWeight: 600, letterSpacing: -0.6, color: '#fff' }}>{fmtValue(headline, st.unit)}</div>
          <CompHint st={st} />
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: twoCol ? '1fr 1fr' : '1fr', gap: '2px 28px', borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: 9 }}>
        {rows.map((r, i) => (
          <div key={r.entityId} style={{ display: 'grid', gridTemplateColumns: '20px 1fr 110px 84px 48px', gap: 9, alignItems: 'center', padding: '5px 0' }}>
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10.5, color: i < 3 ? accent : 'rgba(240,240,240,0.35)' }}>{i + 1}</span>
            <span style={{ display: 'flex', alignItems: 'center', gap: 7, minWidth: 0 }}>
              <EntityGlyph kind={st.kind} size={12} />
              <span style={{ fontSize: 12, color: 'rgba(240,240,240,0.85)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.entity.name}</span>
            </span>
            <MiniBar frac={r.value / maxVal} color={kindMeta.color} height={5} />
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5, textAlign: 'right', color: '#f0f0f0' }}>{fmtValue(r.value, st.unit)}</span>
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, textAlign: 'right', color: 'rgba(240,240,240,0.4)' }}>{st.agg === 'sum' ? Math.round((r.value / sumVal) * 100) + '%' : ''}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

Object.assign(window, { StatCards });
