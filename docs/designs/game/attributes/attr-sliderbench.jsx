/* Direction B · Slider Bench
   Allocation as drag. Each attribute is a bar on a common 0–28 scale so
   builds are visually comparable at a glance. The committed portion is
   solid; pending points extend in accent; a tick marks your saved value
   (before→after). A points budget drains as you drag. */

const SLIDER_MAX = 28;

function SliderBench({ accent, detail }) {
  const b = useBuild();
  return (
    <Board label="Slider Bench" sub="drag to allocate" accent={accent}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, marginBottom: 18 }}>
        <BudgetMeter remaining={b.remaining} budget={b.budget} accent={accent} />
        <PresetRow onApply={b.applyPreset} accent={accent} />
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', gap: 12, justifyContent: 'center' }}>
        {ATTRS.map((a, i) => <AllocSlider key={a.id} i={i} a={a} b={b} accent={accent} detail={detail} />)}
      </div>

      <DerivedStrip build={b} accent={accent} detail={detail} />
      <div style={{ marginTop: 14 }}><CommitBar build={b} accent={accent} /></div>
    </Board>
  );
}

function AllocSlider({ i, a, b, accent, detail }) {
  const trackRef = React.useRef(null);
  const [drag, setDrag] = React.useState(false);
  const value = b.values[i], saved = b.savedValues[i], base = b.base[i];
  const reach = Math.min(SLIDER_MAX, value + b.remaining);   // furthest this bar can be dragged
  const pctFrom = (v) => `${(Math.min(v, SLIDER_MAX) / SLIDER_MAX) * 100}%`;

  const apply = (clientX) => {
    const el = trackRef.current; if (!el) return;
    const r = el.getBoundingClientRect();
    const f = Math.max(0, Math.min(1, (clientX - r.left) / r.width));
    b.setSpend(i, Math.round(f * SLIDER_MAX) - base);
  };
  React.useEffect(() => {
    if (!drag) return;
    const move = (e) => apply(e.clientX);
    const up = () => setDrag(false);
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    return () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
  }, [drag]);

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
      {/* label */}
      <div style={{ width: 150, flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 7 }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', background: a.color, boxShadow: `0 0 7px ${a.color}99` }} />
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: a.color, letterSpacing: 0.5 }}>{a.key}</span>
          <span style={{ fontSize: 13.5, color: '#f0f0f0' }}>{a.name}</span>
        </div>
        <div style={{ fontSize: 11, color: 'rgba(240,240,240,0.4)', marginTop: 2, marginLeft: 15 }}>{a.blurb}</div>
      </div>

      {/* track */}
      <div
        ref={trackRef}
        onPointerDown={(e) => { setDrag(true); apply(e.clientX); }}
        style={{ flex: 1, position: 'relative', height: 26, cursor: 'pointer',
          display: 'flex', alignItems: 'center' }}>
        <div style={{ position: 'absolute', left: 0, right: 0, height: 8, borderRadius: 4,
          background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.08)' }} />
        {/* reachable hint */}
        <div style={{ position: 'absolute', left: 0, width: pctFrom(reach), height: 8, borderRadius: 4,
          background: `${accent}10` }} />
        {/* committed fill */}
        <div style={{ position: 'absolute', left: 0, width: pctFrom(saved), height: 8, borderRadius: 4,
          background: 'rgba(240,240,240,0.28)' }} />
        {/* pending fill */}
        {value !== saved && (
          <div style={{ position: 'absolute', left: pctFrom(Math.min(saved, value)),
            width: `${(Math.abs(value - saved) / SLIDER_MAX) * 100}%`, height: 8, borderRadius: 4,
            background: value > saved ? accent : 'rgba(232,138,120,0.6)',
            boxShadow: value > saved ? `0 0 8px ${accent}88` : 'none' }} />
        )}
        {/* saved tick */}
        <div style={{ position: 'absolute', left: pctFrom(saved), top: 3, bottom: 3, width: 2,
          marginLeft: -1, background: 'rgba(240,240,240,0.6)' }} />
        {/* thumb */}
        <div style={{ position: 'absolute', left: pctFrom(value), top: '50%',
          transform: 'translate(-50%, -50%)', width: drag ? 18 : 15, height: drag ? 18 : 15,
          borderRadius: '50%', background: '#0d0e12', border: `2px solid ${accent}`,
          boxShadow: `0 0 ${drag ? 12 : 7}px ${accent}aa`, transition: 'width 110ms, height 110ms, box-shadow 110ms' }} />
      </div>

      {/* value */}
      <div style={{ width: 96, flexShrink: 0, textAlign: 'right' }}>
        <Delta from={saved} to={value} accent={accent} size={14} />
      </div>
    </div>
  );
}

window.SliderBench = SliderBench;
