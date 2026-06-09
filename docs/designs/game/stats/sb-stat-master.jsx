/* sb-stat-master.jsx — Direction A · "Master + Detail"
   Left rail lists all 14 stat types grouped by category, each with its
   headline value. Pick one; the right pane shows its per-entity breakdown as a
   ranked table with mini bars and a small summary (total · top entity ·
   entities tracked). The fastest way to answer "where do my X come from?". */

function StatMaster({ accent = '#a1c2f7' }) {
  const [sel, setSel] = React.useState('EnemiesKilled');
  const st = STAT_BY_KEY[sel];
  const rows = rowsForStat(sel);
  const headline = statHeadline(sel);
  const maxVal = Math.max(...rows.map((r) => r.value), 1);
  const sumVal = rows.reduce((a, r) => a + r.value, 0);
  const kindMeta = ENTITY_KIND[st.kind];

  return (
    <StatBoard label="Statistics" sub="per-type breakdowns" accent={accent} scroll={false}>
      <div style={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: '270px 1fr', gap: 22 }}>
        {/* rail */}
        <div style={{ overflow: 'auto', paddingRight: 4, borderRight: '1px solid rgba(255,255,255,0.06)' }}>
          {STAT_CATEGORIES.map((cat) => (
            <div key={cat.key} style={{ marginBottom: 14 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, margin: '2px 0 7px' }}>
                <span style={{ width: 7, height: 7, background: cat.color, borderRadius: 1 }} />
                <SMono size={9} ls={1.4}>{cat.label}</SMono>
              </div>
              {STAT_TYPES.filter((s) => s.cat === cat.key).map((s) => {
                const on = s.key === sel;
                return (
                  <button key={s.key} onClick={() => setSel(s.key)}
                    style={{ width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center',
                      justifyContent: 'space-between', gap: 10, padding: '7px 10px', marginBottom: 2, borderRadius: 4,
                      cursor: 'pointer', background: on ? `${accent}1a` : 'transparent',
                      border: `1px solid ${on ? accent + '66' : 'transparent'}`, transition: 'background 120ms' }}>
                    <span style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                      <EntityGlyph kind={s.kind} size={12} color={on ? '#fff' : undefined} />
                      <span style={{ fontSize: 12.5, color: on ? '#fff' : 'rgba(240,240,240,0.82)', whiteSpace: 'nowrap',
                        overflow: 'hidden', textOverflow: 'ellipsis' }}>{s.name}</span>
                    </span>
                    <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                      color: on ? '#fff' : 'rgba(240,240,240,0.55)', whiteSpace: 'nowrap' }}>{fmtValue(statHeadline(s.key), s.unit)}</span>
                  </button>
                );
              })}
            </div>
          ))}
        </div>

        {/* detail */}
        <div style={{ overflow: 'auto', paddingRight: 6 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, marginBottom: 4 }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 5 }}>
                <h2 style={{ margin: 0, fontSize: 23, fontWeight: 500, letterSpacing: -0.3 }}>{st.name}</h2>
                <CategoryTag cat={st.cat} />
                <KindBadge kind={st.kind} />
              </div>
              <p style={{ margin: 0, fontSize: 12.5, color: 'rgba(240,240,240,0.55)' }}>{st.desc}</p>
            </div>
            <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
              <div style={{ fontFamily: 'Geist, sans-serif', fontSize: 34, fontWeight: 600, letterSpacing: -1, color: '#fff',
                textShadow: `0 0 22px ${accent}33` }}>{fmtValue(headline, st.unit)}</div>
              <CompHint st={st} />
            </div>
          </div>

          {/* summary chips */}
          <div style={{ display: 'flex', gap: 10, margin: '16px 0 14px' }}>
            <SummaryChip label="Entities tracked" value={rows.length} />
            <SummaryChip label={st.agg === 'min' ? 'Best' : 'Top'} value={rows[0] ? rows[0].entity.name : '—'} />
            {st.agg === 'sum' && <SummaryChip label="Top share"
              value={rows[0] ? Math.round((rows[0].value / sumVal) * 100) + '%' : '—'} />}
          </div>

          {/* ranked breakdown */}
          <div style={{ display: 'grid', gridTemplateColumns: '22px 1fr 150px 92px 56px', gap: 10, padding: '0 4px 7px',
            borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
            <SMono size={9} ls={1}>#</SMono>
            <SMono size={9} ls={1}>{kindMeta.label}</SMono>
            <SMono size={9} ls={1} style={{ textAlign: 'right' }}></SMono>
            <SMono size={9} ls={1} style={{ textAlign: 'right' }}>{unitLabel(st.unit)}</SMono>
            <SMono size={9} ls={1} style={{ textAlign: 'right' }}>{st.agg === 'sum' ? 'share' : ''}</SMono>
          </div>
          {rows.map((r, i) => (
            <div key={r.entityId} style={{ display: 'grid', gridTemplateColumns: '22px 1fr 150px 92px 56px', gap: 10,
              alignItems: 'center', padding: '8px 4px', borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: i < 3 ? accent : 'rgba(240,240,240,0.4)' }}>{i + 1}</span>
              <span style={{ display: 'flex', alignItems: 'center', gap: 9, minWidth: 0 }}>
                <EntityGlyph kind={st.kind} size={14} />
                <span style={{ fontSize: 13, color: 'rgba(240,240,240,0.9)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.entity.name}</span>
                {st.kind === 'enemy' && r.entity.boss && <SMono size={8} ls={0.8} color="#e8c878" style={{ border: '1px solid #e8c87840', borderRadius: 2, padding: '1px 4px' }}>Boss</SMono>}
              </span>
              <MiniBar frac={r.value / maxVal} color={kindMeta.color} />
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12.5, textAlign: 'right', color: '#f0f0f0' }}>{fmtValue(r.value, st.unit)}</span>
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, textAlign: 'right', color: 'rgba(240,240,240,0.45)' }}>
                {st.agg === 'sum' ? Math.round((r.value / sumVal) * 100) + '%' : ''}</span>
            </div>
          ))}
        </div>
      </div>
    </StatBoard>
  );
}

function SummaryChip({ label, value }) {
  return (
    <div style={{ flex: '0 1 auto', background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 4, padding: '8px 14px', minWidth: 0 }}>
      <SMono size={8.5} ls={1.2} style={{ display: 'block', marginBottom: 3 }}>{label}</SMono>
      <span style={{ fontSize: 14, color: '#f0f0f0', fontWeight: 500, whiteSpace: 'nowrap' }}>{value}</span>
    </div>
  );
}

Object.assign(window, { StatMaster });
