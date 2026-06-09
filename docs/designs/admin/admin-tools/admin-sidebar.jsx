/* Admin sidebar — the collapsible rail from the game (NavSidebar), retuned
   for the admin tool tree. 60px icon rail → 218px on hover/pin. Entity types
   are group headers; tools are items. A pinned "Return to Game" item sits
   above the tick-counter footer (replaces the old top-bar "Game" link). */

const A_COLLAPSED = 60;
const A_EXPANDED = 224;

function AdminSidebar({ active, onNavigate, onBackToGame }) {
  const [hovering, setHovering] = React.useState(false);
  const [pinned, setPinned] = React.useState(false);
  const expanded = pinned || hovering;

  return (
    <div
      onMouseEnter={() => setHovering(true)}
      onMouseLeave={() => setHovering(false)}
      style={{
        position: 'absolute', top: 0, bottom: 0, left: 0,
        width: expanded ? A_EXPANDED : A_COLLAPSED,
        background: '#14151b',
        borderRight: '1px solid rgba(255,255,255,0.08)',
        boxShadow: expanded ? '6px 0 28px rgba(0,0,0,0.55)' : 'none',
        transition: 'width 220ms cubic-bezier(.4,0,.2,1), box-shadow 220ms ease',
        display: 'flex', flexDirection: 'column',
        zIndex: 10, overflow: 'hidden',
      }}>
      {/* wordmark + admin tag + pin */}
      <div style={{
        padding: '14px 0', borderBottom: '1px solid rgba(255,255,255,0.06)',
        position: 'relative', display: 'flex', alignItems: 'center', height: 64,
      }}>
        <div style={{
          width: A_COLLAPSED, flexShrink: 0,
          display: 'flex', justifyContent: 'center', alignItems: 'center',
        }}>
          <GameDiamond size={13} pulse />
        </div>
        <div style={{
          opacity: expanded ? 1 : 0,
          transition: 'opacity 180ms ease', transitionDelay: expanded ? '90ms' : '0ms',
          whiteSpace: 'nowrap', lineHeight: 1.35,
        }}>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 11, letterSpacing: 2,
            textTransform: 'uppercase', color: 'rgba(240,240,240,0.92)',
          }}>Tactic Foundry</div>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 2.4,
            textTransform: 'uppercase', color: '#a1c2f7', marginTop: 2,
          }}>Admin Console</div>
        </div>
        {expanded && <APinButton pinned={pinned} onClick={() => setPinned((p) => !p)} />}
      </div>

      {/* nav body */}
      <div style={{ flex: 1, overflowY: 'auto', overflowX: 'hidden', padding: '12px 0' }}>
        {ADMIN_GROUPS.map((g, gi) => {
          const items = ADMIN_TOOLS.filter((t) => t.group === g.key);
          if (!items.length) return null;
          return (
            <div key={g.key} style={{ marginBottom: 8 }}>
              <AGroupHeader label={g.label} expanded={expanded} firstGroup={gi === 0} />
              {items.map((t) => (
                <ASideItem key={t.key} tool={t} active={active === t.key}
                  expanded={expanded} onClick={() => onNavigate(t.key)} />
              ))}
            </div>
          );
        })}
      </div>

      {/* return to game */}
      <div style={{ borderTop: '1px solid rgba(255,255,255,0.06)', padding: '6px 0' }}>
        <ASideItem
          tool={{ key: '__back', label: 'Return to Game', glyph: 'back' }}
          active={false} expanded={expanded} onClick={onBackToGame} />
      </div>

      {/* footer: tick counter */}
      <div style={{
        borderTop: '1px solid rgba(255,255,255,0.06)',
        display: 'flex', alignItems: 'center', height: 44, flexShrink: 0,
      }}>
        <div style={{
          width: A_COLLAPSED, flexShrink: 0,
          display: 'flex', justifyContent: 'center', alignItems: 'center',
        }}>
          <APulseDot />
        </div>
        <div style={{
          opacity: expanded ? 1 : 0, transition: 'opacity 160ms ease',
          transitionDelay: expanded ? '90ms' : '0ms', whiteSpace: 'nowrap',
        }}>
          <TickCounter />
        </div>
      </div>
    </div>
  );
}

function AGroupHeader({ label, expanded, firstGroup }) {
  return (
    <div style={{
      padding: expanded ? '8px 22px 4px' : '0', marginTop: firstGroup ? 0 : 6,
      height: expanded ? 'auto' : 12, display: 'flex', alignItems: 'center',
      transition: 'padding 180ms ease',
    }}>
      {expanded ? (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 1.8,
          textTransform: 'uppercase', color: 'rgba(240,240,240,0.4)', whiteSpace: 'nowrap',
          transition: 'opacity 160ms ease', transitionDelay: '80ms',
        }}>{label}</span>
      ) : (
        <div style={{ width: A_COLLAPSED, display: 'flex', justifyContent: 'center' }}>
          {!firstGroup && <div style={{ width: 18, height: 1, background: 'rgba(240,240,240,0.12)' }} />}
        </div>
      )}
    </div>
  );
}

function ASideItem({ tool, active, expanded, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      title={!expanded ? tool.label : undefined}
      style={{
        position: 'relative', width: '100%',
        background: active ? 'rgba(161,194,247,0.08)' : hover ? 'rgba(255,255,255,0.03)' : 'transparent',
        border: 'none',
        color: active ? '#f0f0f0' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.65)',
        fontFamily: 'inherit', fontSize: 13, padding: 0, cursor: 'pointer',
        textAlign: 'left', display: 'flex', alignItems: 'center', height: 38,
        transition: 'color 140ms, background 140ms', whiteSpace: 'nowrap', overflow: 'hidden',
      }}>
      <div style={{
        width: A_COLLAPSED, flexShrink: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <AdminGlyph kind={tool.glyph} active={active || hover} />
      </div>
      <span style={{
        flex: 1, opacity: expanded ? 1 : 0, transition: 'opacity 160ms ease',
        transitionDelay: expanded ? '90ms' : '0ms',
      }}>{tool.label}</span>
      <span style={{
        position: 'absolute', left: 0, top: 5, bottom: 5, width: 2,
        background: active ? '#a1c2f7' : 'transparent',
        boxShadow: active ? '0 0 10px rgba(161,194,247,0.75)' : 'none',
        transition: 'background 160ms, box-shadow 160ms',
      }} />
    </button>
  );
}

function APinButton({ pinned, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      title={pinned ? 'Unpin' : 'Keep open'}
      style={{
        position: 'absolute', right: 12, top: '50%', transform: 'translateY(-50%)',
        background: 'transparent', border: 'none', padding: 6, cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: pinned ? '#a1c2f7' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.5)',
        transition: 'color 140ms',
      }}>
      <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round">
        {pinned ? (
          <>
            <path d="M9 1.5l3.5 3.5-2 2L7 3.5z" />
            <path d="M7 3.5l-3 3 3.5 3.5 3-3" />
            <path d="M4 6.5L1.5 12.5" />
          </>
        ) : (
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

function APulseDot() {
  return (
    <div style={{
      width: 6, height: 6, borderRadius: '50%', background: '#a1c2f7',
      boxShadow: '0 0 8px rgba(161,194,247,0.8)',
      animation: 'pulse-dot 1.6s ease-in-out infinite',
    }} />
  );
}

Object.assign(window, { AdminSidebar, A_COLLAPSED });
