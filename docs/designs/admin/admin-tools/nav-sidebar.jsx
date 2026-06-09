/* Nav C · Collapsible Sidebar Rail
   60px collapsed (icons only) → 218px expanded on hover. Overlay: the
   content area is offset by the collapsed width and stays put, the
   expanded sidebar floats over it with a soft shadow. Optional pin to
   keep it open. Labels fade in/out; group headers and tick counter only
   appear in expanded state. */

const COLLAPSED = 60;
const EXPANDED = 218;

function NavSidebar({ fightScreen }) {
  const { active, setActive } = useNav('fight');
  const [hovering, setHovering] = React.useState(false);
  const [pinned, setPinned] = React.useState(false);
  const expanded = pinned || hovering;

  const groups = ['play', 'hero', 'meta', 'admin'];

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0',
      display: 'flex',
      position: 'relative',
      overflow: 'hidden',
    }}>
      {/* fixed-width spacer so the content area never shifts */}
      <div style={{ width: COLLAPSED, flexShrink: 0 }} />

      <GameContent active={active} fightScreen={fightScreen} />

      {/* sidebar floats above the content */}
      <div
        onMouseEnter={() => setHovering(true)}
        onMouseLeave={() => setHovering(false)}
        style={{
          position: 'absolute', top: 0, bottom: 0, left: 0,
          width: expanded ? EXPANDED : COLLAPSED,
          background: '#14151b',
          borderRight: '1px solid rgba(255,255,255,0.08)',
          boxShadow: expanded ? '6px 0 28px rgba(0,0,0,0.55)' : 'none',
          transition: 'width 220ms cubic-bezier(.4,0,.2,1), box-shadow 220ms ease',
          display: 'flex', flexDirection: 'column',
          zIndex: 10,
          overflow: 'hidden',
        }}>
        {/* wordmark / pin */}
        <div style={{
          padding: '18px 0',
          borderBottom: '1px solid rgba(255,255,255,0.06)',
          position: 'relative',
          display: 'flex', alignItems: 'center',
          height: 58,
        }}>
          <div style={{
            width: COLLAPSED, flexShrink: 0,
            display: 'flex', justifyContent: 'center', alignItems: 'center',
          }}>
            <GameDiamond size={13} pulse />
          </div>
          <div style={{
            opacity: expanded ? 1 : 0,
            transition: 'opacity 180ms ease',
            transitionDelay: expanded ? '90ms' : '0ms',
            fontFamily: 'Geist Mono, monospace', fontSize: 11,
            letterSpacing: 2, textTransform: 'uppercase',
            color: 'rgba(240,240,240,0.92)',
            whiteSpace: 'nowrap',
          }}>Tactic Foundry</div>
          {expanded && (
            <PinButton pinned={pinned} onClick={() => setPinned((p) => !p)} />
          )}
        </div>

        {/* nav body */}
        <div style={{ flex: 1, overflowY: 'auto', overflowX: 'hidden', padding: '12px 0' }}>
          {groups.map((g, gi) => {
            const items = SCREENS.filter((s) => s.group === g);
            if (!items.length) return null;
            return (
              <div key={g} style={{ marginBottom: 8 }}>
                <GroupHeader label={GROUP_META[g].label} expanded={expanded} firstGroup={gi === 0} />
                {items.map((s) => (
                  <SideItem
                    key={s.key}
                    screen={s}
                    active={active === s.key}
                    expanded={expanded}
                    onClick={() => setActive(s.key)}
                  />
                ))}
              </div>
            );
          })}
        </div>

        {/* footer: tick counter */}
        <div style={{
          borderTop: '1px solid rgba(255,255,255,0.06)',
          display: 'flex', alignItems: 'center',
          height: 44, flexShrink: 0,
        }}>
          <div style={{
            width: COLLAPSED, flexShrink: 0,
            display: 'flex', justifyContent: 'center', alignItems: 'center',
          }}>
            <PulseDot />
          </div>
          <div style={{
            opacity: expanded ? 1 : 0,
            transition: 'opacity 160ms ease',
            transitionDelay: expanded ? '90ms' : '0ms',
            whiteSpace: 'nowrap',
          }}>
            <TickCounter />
          </div>
        </div>
      </div>
    </div>
  );
}

function GroupHeader({ label, expanded, firstGroup }) {
  return (
    <div style={{
      padding: expanded ? '8px 22px 4px' : '0',
      marginTop: firstGroup ? 0 : 6,
      height: expanded ? 'auto' : 12,
      display: 'flex', alignItems: 'center',
      transition: 'padding 180ms ease',
    }}>
      {expanded ? (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
          letterSpacing: 1.8, textTransform: 'uppercase',
          color: 'rgba(240,240,240,0.4)',
          whiteSpace: 'nowrap',
          opacity: expanded ? 1 : 0,
          transition: 'opacity 160ms ease',
          transitionDelay: '80ms',
        }}>{label}</span>
      ) : (
        <div style={{
          width: COLLAPSED, display: 'flex', justifyContent: 'center',
        }}>
          {!firstGroup && (
            <div style={{ width: 18, height: 1, background: 'rgba(240,240,240,0.12)' }} />
          )}
        </div>
      )}
    </div>
  );
}

function SideItem({ screen, active, expanded, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      title={!expanded ? screen.label : undefined}
      style={{
        position: 'relative',
        width: '100%',
        background: active ? 'rgba(161,194,247,0.08)' : hover ? 'rgba(255,255,255,0.03)' : 'transparent',
        border: 'none',
        color: active ? '#f0f0f0' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.65)',
        fontFamily: 'inherit',
        fontSize: 13,
        padding: 0,
        cursor: 'pointer',
        textAlign: 'left',
        display: 'flex', alignItems: 'center',
        height: 38,
        transition: 'color 140ms, background 140ms',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
      }}>
      {/* glyph slot — fixed 60px so position is constant */}
      <div style={{
        width: COLLAPSED, flexShrink: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <SideGlyph kind={screen.key} active={active || hover} />
      </div>

      {/* label */}
      <span style={{
        flex: 1,
        opacity: expanded ? 1 : 0,
        transition: 'opacity 160ms ease',
        transitionDelay: expanded ? '90ms' : '0ms',
      }}>{screen.label}</span>

      {/* wip badge */}
      {!screen.built && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
          color: 'rgba(240,240,240,0.42)', letterSpacing: 0.5,
          padding: '1px 4px',
          border: '1px solid rgba(240,240,240,0.15)',
          borderRadius: 2,
          textTransform: 'uppercase',
          marginRight: 14,
          opacity: expanded ? 1 : 0,
          transition: 'opacity 160ms ease',
          transitionDelay: expanded ? '110ms' : '0ms',
        }}>wip</span>
      )}

      {/* active accent bar — always present */}
      <span style={{
        position: 'absolute', left: 0, top: 5, bottom: 5,
        width: 2,
        background: active ? '#a1c2f7' : 'transparent',
        boxShadow: active ? '0 0 10px rgba(161,194,247,0.75)' : 'none',
        transition: 'background 160ms, box-shadow 160ms',
      }} />
    </button>
  );
}

function PinButton({ pinned, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      title={pinned ? 'Unpin' : 'Keep open'}
      style={{
        position: 'absolute', right: 12, top: '50%',
        transform: 'translateY(-50%)',
        background: 'transparent',
        border: 'none',
        padding: 6,
        cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: pinned ? '#a1c2f7' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.5)',
        transition: 'color 140ms',
      }}>
      <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round">
        {pinned ? (
          // pinned: rotated thumbtack (driven in)
          <>
            <path d="M9 1.5l3.5 3.5-2 2L7 3.5z" />
            <path d="M7 3.5l-3 3 3.5 3.5 3-3" />
            <path d="M4 6.5L1.5 12.5" />
          </>
        ) : (
          // unpinned: tilted
          <>
            <path d="M10.5 1.5L12.5 3.5l-1.5 1.5L9 3z" />
            <path d="M9 3l-2.5 2.5 3 3 2.5-2.5" />
            <path d="M6.5 5.5L2 10" />
          </>
        )}
      </svg>
    </button>
  );
}

function PulseDot() {
  return (
    <div style={{
      width: 6, height: 6, borderRadius: '50%',
      background: '#a1c2f7',
      boxShadow: '0 0 8px rgba(161,194,247,0.8)',
      animation: 'pd-pulse 1.6s ease-in-out infinite',
    }}>
      <style>{`@keyframes pd-pulse {
        0%, 100% { opacity: 0.6; box-shadow: 0 0 6px rgba(161,194,247,0.5); }
        50%      { opacity: 1;   box-shadow: 0 0 12px rgba(161,194,247,0.95); }
      }`}</style>
    </div>
  );
}

/* Glyphs — small abstract marks, sized for the collapsed rail. */
function SideGlyph({ kind, active }) {
  const stroke = active ? '#a1c2f7' : 'rgba(240,240,240,0.7)';
  const s = { width: 16, height: 16, fill: 'none', stroke, strokeWidth: 1.4 };
  const map = {
    fight:      <svg {...s} viewBox="0 0 16 16"><path d="M11 1.5l3.5 3.5-7.5 7.5-3.5-3.5z M3 12l1.7 1.7" strokeLinejoin="round" strokeLinecap="round" /></svg>,
    cardGame:   <svg {...s} viewBox="0 0 16 16"><rect x="2.5" y="3" width="7" height="10" rx="1" /><rect x="6.5" y="2" width="7" height="10" rx="1" /></svg>,
    challenges: <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l1.8 3.7 4.1.6-3 2.9.7 4.1L8 11l-3.6 1.8.7-4.1-3-2.9 4.1-.6z" strokeLinejoin="round" /></svg>,
    inventory:  <svg {...s} viewBox="0 0 16 16"><rect x="2" y="4" width="12" height="9.5" rx="0.5" /><path d="M5.5 4V2.5h5V4" strokeLinecap="round" /></svg>,
    attributes: <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l4.7 2v4.7c0 2.9-2.3 4.7-4.7 5.8-2.4-1.1-4.7-2.9-4.7-5.8V3.5z" strokeLinejoin="round" /></svg>,
    stats:      <svg {...s} viewBox="0 0 16 16"><path d="M2 13.5h12M3.5 13.5V8M7 13.5V4.5M10.5 13.5V10M14 13.5V6" strokeLinecap="round" /></svg>,
    options:    <svg {...s} viewBox="0 0 16 16"><circle cx="8" cy="8" r="2.2" /><path d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.5 1.5M11.1 11.1l1.5 1.5M3.4 12.6l1.5-1.5M11.1 4.9l1.5-1.5" strokeLinecap="round" /></svg>,
    help:       <svg {...s} viewBox="0 0 16 16"><circle cx="8" cy="8" r="5.5" /><path d="M6.3 6.3c0-1 .8-1.8 1.7-1.8s1.7.8 1.7 1.7c0 1.4-1.7 1.3-1.7 3" strokeLinecap="round" /><circle cx="8" cy="11.5" r="0.5" fill={stroke} stroke="none" /></svg>,
    quit:       <svg {...s} viewBox="0 0 16 16"><path d="M6 2.5H3a1 1 0 00-1 1v9a1 1 0 001 1h3M9.5 5L12.5 8l-3 3M5 8h7.5" strokeLinecap="round" strokeLinejoin="round" /></svg>,
    admin:      <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l5 1.7v4.5c0 2.9-2.2 4.7-5 5.8-2.8-1.1-5-2.9-5-5.8V3.2z" strokeLinejoin="round" /><path d="M5.6 8l1.7 1.7L10.5 6.5" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  };
  return map[kind] ?? null;
}

window.NavSidebar = NavSidebar;
