/* Nav A · Minimal Top Bar
   Single horizontal row: wordmark left, items center, tick counter right.
   Items are text-only. Active = bright text + bottom accent line. Hover =
   text brightens. Carries the login/loading vocabulary directly. */

function NavTopbar() {
  const { active, setActive } = useNav('fight');

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0',
      display: 'flex', flexDirection: 'column',
    }}>
      {/* nav */}
      <div style={{
        background: '#14151b',
        borderBottom: '1px solid rgba(255,255,255,0.08)',
        height: 52,
        display: 'flex', alignItems: 'stretch',
        padding: '0 24px',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', minWidth: 200 }}>
          <Wordmark />
        </div>
        <div style={{ flex: 1, display: 'flex', alignItems: 'stretch', justifyContent: 'center', gap: 2 }}>
          {SCREENS.map((s) => (
            <TopItem key={s.key} screen={s} active={active === s.key} onClick={() => setActive(s.key)} />
          ))}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', minWidth: 200, justifyContent: 'flex-end' }}>
          <TickCounter />
        </div>
      </div>

      <GameContent active={active} />
    </div>
  );
}

function TopItem({ screen, active, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: 'transparent',
        border: 'none',
        color: active ? '#f0f0f0' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.55)',
        fontFamily: 'inherit',
        fontSize: 12.5,
        letterSpacing: 0.6,
        padding: '0 14px',
        cursor: 'pointer',
        position: 'relative',
        transition: 'color 140ms',
        display: 'flex', alignItems: 'center', gap: 6,
        whiteSpace: 'nowrap',
      }}>
      {screen.label}
      {!screen.built && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
          color: 'rgba(240,240,240,0.4)', letterSpacing: 0.5,
          padding: '1px 4px',
          border: '1px solid rgba(240,240,240,0.15)',
          borderRadius: 2,
          textTransform: 'uppercase',
        }}>wip</span>
      )}
      {/* active underline */}
      <span style={{
        position: 'absolute', left: 14, right: 14, bottom: 0,
        height: 2,
        background: active ? '#a1c2f7' : 'transparent',
        boxShadow: active ? '0 0 8px rgba(161,194,247,0.6)' : 'none',
        transition: 'background 160ms, box-shadow 160ms',
      }} />
      {/* hover indicator */}
      <span style={{
        position: 'absolute', left: 14, right: 14, bottom: 0,
        height: 1,
        background: !active && hover ? 'rgba(240,240,240,0.35)' : 'transparent',
        transition: 'background 140ms',
      }} />
    </button>
  );
}

window.NavTopbar = NavTopbar;
