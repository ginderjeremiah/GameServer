/* Nav B · Grouped Top Bar
   Horizontal items clustered by purpose with thin vertical dividers.
   Same chrome as A but reads as a curated menu rather than a flat list.
   Admin pushed to the far right (set apart from gameplay items). */

function NavGrouped() {
  const { active, setActive } = useNav('fight');

  const gameplayGroups = ['play', 'hero', 'meta'];
  const groupedScreens = gameplayGroups.map((g) => ({
    group: g,
    items: SCREENS.filter((s) => s.group === g),
  }));
  const adminItems = SCREENS.filter((s) => s.group === 'admin');

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0',
      display: 'flex', flexDirection: 'column',
    }}>
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

        <div style={{ flex: 1, display: 'flex', alignItems: 'stretch', justifyContent: 'center', gap: 0 }}>
          {groupedScreens.map((g, gi) => (
            <React.Fragment key={g.group}>
              {gi > 0 && <Divider />}
              <div style={{ display: 'flex', alignItems: 'stretch', gap: 2, padding: '0 12px' }}
                title={GROUP_META[g.group].label}>
                {g.items.map((s) => (
                  <GroupedItem key={s.key} screen={s} active={active === s.key} onClick={() => setActive(s.key)} />
                ))}
              </div>
            </React.Fragment>
          ))}
        </div>

        <div style={{ display: 'flex', alignItems: 'stretch', gap: 12, minWidth: 200, justifyContent: 'flex-end' }}>
          {adminItems.map((s) => (
            <GroupedItem key={s.key} screen={s} active={active === s.key} onClick={() => setActive(s.key)} />
          ))}
          <div style={{ alignSelf: 'center' }}><TickCounter /></div>
        </div>
      </div>

      <GameContent active={active} />
    </div>
  );
}

function Divider() {
  return (
    <div style={{
      width: 1, alignSelf: 'center',
      height: 22,
      background: 'rgba(240,240,240,0.12)',
    }} />
  );
}

function GroupedItem({ screen, active, onClick }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: active ? 'rgba(161,194,247,0.08)' : 'transparent',
        border: 'none',
        color: active ? '#f0f0f0' : hover ? '#f0f0f0' : 'rgba(240,240,240,0.6)',
        fontFamily: 'inherit',
        fontSize: 12.5,
        letterSpacing: 0.5,
        padding: '0 11px',
        cursor: 'pointer',
        position: 'relative',
        transition: 'color 140ms, background 140ms',
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
      <span style={{
        position: 'absolute', left: 11, right: 11, bottom: 0,
        height: 2,
        background: active ? '#a1c2f7' : 'transparent',
        boxShadow: active ? '0 0 8px rgba(161,194,247,0.6)' : 'none',
        transition: 'background 160ms',
      }} />
    </button>
  );
}

window.NavGrouped = NavGrouped;
