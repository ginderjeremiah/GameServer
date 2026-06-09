/* sb-attr-ledger.jsx — Direction B · "Ledger"
   Theorycrafter's view. Each attribute is a card whose every modifier is a
   line: source swatch · label · signed amount · running total. Additive lines
   are summed first (matching the engine), a subtotal rule is drawn, then any
   multiplicative modifiers are applied on top — so the apply order is literal
   and the final number is auditable. Two-column masonry-ish grid. */

function AttrLedger({ accent = '#a1c2f7', showInactive = true, showRunning = true }) {
  const computed = React.useMemo(() => computeAttributes(), []);
  const attrs = ATTR_META.filter((a) => showInactive || (!a.unused && !a.obsolete));
  const core = attrs.filter((a) => a.group === 'core');
  const derived = attrs.filter((a) => a.group === 'derived');

  return (
    <SBoard label="Full ledger" sub="every modifier, in apply order" accent={accent}>
      <SourceLegend style={{ marginBottom: 18 }} />
      <Mono style={{ marginBottom: 10, display: 'block' }}>Core attributes</Mono>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 22 }}>
        {core.map((a) => <LedgerCard key={a.key} meta={a} computed={computed[a.key]} showRunning={showRunning} />)}
      </div>
      <Mono style={{ marginBottom: 10, display: 'block' }}>Derived attributes</Mono>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
        {derived.map((a) => <LedgerCard key={a.key} meta={a} computed={computed[a.key]} showRunning={showRunning} />)}
      </div>
    </SBoard>
  );
}

function LedgerCard({ meta, computed, showRunning }) {
  const adds = computed.lines.filter((l) => !l.multiplied);
  const mults = computed.lines.filter((l) => l.multiplied);
  const dim = meta.unused || meta.obsolete;
  const empty = computed.lines.length === 0;

  return (
    <div style={{ background: 'rgba(255,255,255,0.025)', border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 5, padding: '13px 15px 14px', opacity: dim ? 0.7 : 1, display: 'flex', flexDirection: 'column' }}>
      {/* head */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 8, marginBottom: 10 }}>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
            <span style={{ fontSize: 14.5, fontWeight: 500, color: '#f0f0f0' }}>{meta.name}</span>
            <KindTag meta={meta} />
          </div>
          <Mono size={8.5} ls={1.2} color="rgba(240,240,240,0.38)" style={{ marginTop: 2, display: 'block' }}>{meta.short}</Mono>
        </div>
        <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
          <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 22, fontWeight: 600, letterSpacing: -0.5, color: '#f0f0f0' }}>
            {fmtNum(computed.total, meta.dec)}
          </span>
          {meta.unit && <span style={{ fontSize: 11, color: 'rgba(240,240,240,0.4)', marginLeft: 1 }}>{meta.unit === '×' ? '×' : meta.unit}</span>}
        </div>
      </div>

      {empty ? (
        <Mono size={10} ls={0.5} color="rgba(240,240,240,0.35)" style={{ textTransform: 'none', padding: '6px 0' }}>
          No contributors — not currently produced by any source.
        </Mono>
      ) : (
        <div style={{ borderTop: '1px solid rgba(255,255,255,0.06)' }}>
          {adds.map((ln, i) => <LedgerRow key={i} ln={ln} meta={meta} showRunning={showRunning} />)}
          {mults.length > 0 && (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0 5px', borderTop: '1px dashed rgba(255,255,255,0.12)', marginTop: 2 }}>
                <Mono size={9.5} ls={1} color="rgba(240,240,240,0.45)">Additive subtotal</Mono>
                <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12, color: 'rgba(240,240,240,0.7)' }}>{fmtNum(computed.additiveSubtotal, meta.dec)}</span>
              </div>
              {mults.map((ln, i) => <LedgerRow key={'m' + i} ln={ln} meta={meta} showRunning={showRunning} />)}
            </>
          )}
        </div>
      )}
    </div>
  );
}

Object.assign(window, { AttrLedger });
