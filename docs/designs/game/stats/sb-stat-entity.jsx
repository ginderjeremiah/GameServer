/* sb-stat-entity.jsx — Direction D · "Entity Pivot"
   The inverse cut. Instead of "pick a stat, see its entities", you pick an
   ENTITY and see every stat that references it at once. Choose a kind
   (Enemy / Zone / Skill), search/scan the list, select one — the right pane
   becomes that entity's dossier: each relevant stat type with this entity's
   value, its rank against all peers, and a bar placing it in the field.
   Answers "how does this specific enemy/zone/skill show up across my stats?". */

function StatEntity({ accent = '#a1c2f7' }) {
  const [kind, setKind] = React.useState('enemy');
  const [q, setQ] = React.useState('');
  const list = ENTITY_KIND[kind].list;
  const [selId, setSelId] = React.useState(list[0].id);

  const filtered = list.filter((e) => e.name.toLowerCase().includes(q.toLowerCase()));
  const sel = entity(kind, selId) || filtered[0] || list[0];
  const stats = statsForEntity(kind, sel.id);

  const switchKind = (k) => { setKind(k); setQ(''); setSelId(ENTITY_KIND[k].list[0].id); };

  return (
    <StatBoard label="Statistics" sub="filter by entity — see all of its stats at once" accent={accent} scroll={false}>
      <UnderlineTabs
        tabs={Object.values(ENTITY_KIND).map((k) => ({
          key: k.key, label: k.plural, color: k.color, count: k.list.length,
          glyph: (on) => <EntityGlyph kind={k.key} size={13} color={on ? k.color : 'rgba(240,240,240,0.5)'} />,
        }))}
        active={kind} onChange={switchKind} style={{ marginBottom: 16 }} />
      <div style={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: '286px 1fr', gap: 22 }}>
        {/* picker */}
        <div style={{ display: 'flex', flexDirection: 'column', minHeight: 0, borderRight: '1px solid rgba(255,255,255,0.06)', paddingRight: 16 }}>
          {/* search */}
          <div style={{ position: 'relative', marginBottom: 10 }}>
            <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={`Search ${ENTITY_KIND[kind].plural.toLowerCase()}…`}
              style={{ width: '100%', background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.12)',
                borderRadius: 4, padding: '8px 10px 8px 28px', color: '#f0f0f0', fontFamily: 'Geist, sans-serif', fontSize: 12.5, outline: 'none' }} />
            <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="rgba(240,240,240,0.4)" strokeWidth="1.4"
              style={{ position: 'absolute', left: 9, top: 9 }}><circle cx="6" cy="6" r="4.2" /><path d="M9.2 9.2L12 12" strokeLinecap="round" /></svg>
          </div>
          {/* entity list */}
          <div style={{ overflow: 'auto', flex: 1, minHeight: 0 }}>
            {filtered.map((e) => {
              const on = e.id === sel.id;
              return (
                <button key={e.id} onClick={() => setSelId(e.id)}
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

          {/* stat tiles */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12, marginTop: 18 }}>
            {stats.map(({ stat, value, rank, of }) => {
              const rows = rowsForStat(stat.key);
              const maxVal = Math.max(...rows.map((r) => r.value), 1);
              const topName = rows[0].entity.name;
              const isTop = rows[0].entityId === sel.id;
              return (
                <div key={stat.key} style={{ background: 'rgba(255,255,255,0.025)', border: '1px solid rgba(255,255,255,0.08)',
                  borderRadius: 6, padding: '13px 15px 14px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 9 }}>
                    <span style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                      <span style={{ fontSize: 12.5, color: 'rgba(240,240,240,0.82)' }}>{stat.name}</span>
                      <CategoryTag cat={stat.cat} />
                    </span>
                    <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: isTop ? accent : 'rgba(240,240,240,0.42)',
                      border: `1px solid ${isTop ? accent + '55' : 'rgba(255,255,255,0.12)'}`, borderRadius: 8, padding: '1px 7px', whiteSpace: 'nowrap' }}>
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
            })}
          </div>
        </div>
      </div>
    </StatBoard>
  );
}

Object.assign(window, { StatEntity });
