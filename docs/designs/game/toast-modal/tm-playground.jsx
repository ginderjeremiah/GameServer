/* Live playground — real entrance/exit motion + anchor placement, driven by the
   Tweaks panel. Fire toasts (auto-dismiss with a timer + exit animation) and
   open modals over a faux game frame. This is the interactive proof of the two
   systems; the canvas sections above are the static spec. */

const ANCHORS = {
  'top-right':     { top: 16, right: 16, align: 'flex-end',   reverse: false },
  'top-center':    { top: 16, left: '50%', transform: 'translateX(-50%)', align: 'center', reverse: false },
  'bottom-right':  { bottom: 16, right: 16, align: 'flex-end', reverse: true },
  'bottom-center': { bottom: 16, left: '50%', transform: 'translateX(-50%)', align: 'center', reverse: true },
};

let __tid = 0;

function Playground({ toastDir = 'iconed', modalDir = 'framed', modalKind = 'confirm', anchor = 'top-right' }) {
  const [toasts, setToasts] = React.useState([]);
  const [modal, setModal] = React.useState(null); // {animateIn} | null
  const [cycle, setCycle] = React.useState(0);
  const timers = React.useRef({});

  const removeToast = React.useCallback((id) => {
    setToasts((ts) => ts.map((t) => (t.id === id ? { ...t, dismissing: true } : t)));
    clearTimeout(timers.current[id]);
    setTimeout(() => setToasts((ts) => ts.filter((t) => t.id !== id)), 280);
  }, []);

  const fire = React.useCallback((type) => {
    const id = ++__tid;
    setToasts((ts) => {
      const next = { id, type };
      return anchor.startsWith('bottom') ? [...ts, next] : [next, ...ts];
    });
    timers.current[id] = setTimeout(() => removeToast(id), 6000);
  }, [anchor, removeToast]);

  const fireNext = () => { fire(STATUS_ORDER[cycle % 4]); setCycle((c) => c + 1); };

  React.useEffect(() => () => Object.values(timers.current).forEach(clearTimeout), []);

  const a = ANCHORS[anchor] || ANCHORS['top-right'];
  const openModal = () => setModal({ animateIn: true });

  return (
    <div style={{ width: '100%', height: '100%', position: 'relative', overflow: 'hidden', fontFamily: T.sans }}>
      <GameBackdrop dim={modal ? 0.62 : 0.18} />

      {/* demo control cluster */}
      <div style={{
        position: 'absolute', left: '50%', bottom: 20, transform: 'translateX(-50%)',
        display: 'flex', alignItems: 'center', gap: 8, padding: '9px 11px', zIndex: 5,
        background: 'rgba(10,11,15,0.82)', border: `1px solid ${T.borderLight}`, borderRadius: 4,
        boxShadow: '0 8px 24px rgba(0,0,0,0.5)', backdropFilter: 'blur(8px)',
      }}>
        <MonoLabel color={T.textMuted} size={9} style={{ marginRight: 2 }}>Demo</MonoLabel>
        {STATUS_ORDER.map((type) => (
          <button key={type} onClick={() => fire(type)} title={`Fire ${type}`} style={{
            width: 22, height: 22, borderRadius: 2, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: `color-mix(in srgb, ${STATUS[type].color} 12%, transparent)`,
            border: `1px solid color-mix(in srgb, ${STATUS[type].color} 45%, transparent)`,
          }}>
            <StatusGlyph kind={STATUS[type].glyph} color={STATUS[type].color} size={12} />
          </button>
        ))}
        <div style={{ width: 1, height: 20, background: T.borderLight, margin: '0 2px' }} />
        <CtrlBtn onClick={fireNext}>Fire toast</CtrlBtn>
        <CtrlBtn onClick={openModal} accent>Open modal</CtrlBtn>
      </div>

      {/* toast container at chosen anchor */}
      <div style={{
        position: 'absolute', top: a.top, bottom: a.bottom, right: a.right, left: a.left,
        transform: a.transform, zIndex: 6,
        display: 'flex', flexDirection: a.reverse ? 'column-reverse' : 'column',
        gap: 10, alignItems: a.align, pointerEvents: 'none',
      }}>
        {toasts.map((t) => (
          <ToastCard key={t.id} direction={toastDir} type={t.type} anchor={anchor}
            dismissing={t.dismissing} showTimer onDismiss={() => removeToast(t.id)} />
        ))}
      </div>

      {/* modal overlay */}
      {modal && (
        <div style={{ position: 'absolute', inset: 0, zIndex: 8, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24,
          background: 'rgba(8,9,12,0.5)' }}
          onClick={() => setModal(null)}>
          <div onClick={(e) => e.stopPropagation()}>
            <ModalCard direction={modalDir} kind={modalKind} animateIn={modal.animateIn} onClose={() => setModal(null)} />
          </div>
        </div>
      )}
    </div>
  );
}

function CtrlBtn({ children, onClick, accent }) {
  const [h, setH] = React.useState(false);
  return (
    <button onClick={onClick} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)} style={{
      padding: '5px 12px', borderRadius: 2, cursor: 'pointer', fontFamily: T.sans, fontSize: 12, fontWeight: 500,
      color: accent ? T.accentLight : T.textSecondary,
      background: accent ? (h ? 'color-mix(in srgb, #a1c2f7 26%, transparent)' : 'color-mix(in srgb, #a1c2f7 16%, transparent)') : (h ? 'rgba(255,255,255,0.06)' : 'transparent'),
      border: `1px solid ${accent ? '#a1c2f7' : T.borderLight}`, transition: 'background .14s',
    }}>{children}</button>
  );
}

Object.assign(window, { Playground });
