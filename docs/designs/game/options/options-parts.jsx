/* Live combat-log preview + the screen shell (rails, save bar, OptionsScreen). */

/* ── live preview event stream — emits across all six log categories so
   every toggle visibly changes the output. Self-contained (the shared
   useLogStream doesn't carry Exp/LevelUp), keyed by ELogType key. ─────── */
const PREVIEW_TEMPLATES = {
  Damage: [
    () => ({ msg: <><b style={{color:'#c0d8ff'}}>Aelara</b> cast <i>Cleave</i> on <b style={{color:'#e8b6a6'}}>Skeleton Mage</b></>, val: 20 + Math.floor(Math.random()*36), sign: '' }),
    () => ({ msg: <><b style={{color:'#e8b6a6'}}>Skeleton Mage</b> cast <i>Hex</i> on <b style={{color:'#c0d8ff'}}>Aelara</b></>, val: 8 + Math.floor(Math.random()*18), sign: '−' }),
  ],
  EnemyDefeated: [() => ({ msg: <><b style={{color:'#e8b6a6'}}>Skeleton Mage</b> was defeated.</>, val: null })],
  ItemFound:     [() => ({ msg: <>Picked up <b style={{color:'#bde0b4'}}>Sapphire Shard</b></>, val: 1, sign: '×' })],
  Exp:           [() => ({ msg: <>Gained experience from victory.</>, val: 30 + Math.floor(Math.random()*40), sign: '+' })],
  LevelUp:       [() => ({ msg: <><b style={{color:'#f0d28a'}}>Aelara</b> reached level 13!</>, val: null })],
  CritHit:       [() => ({ msg: <><b style={{color:'#c0d8ff'}}>Aelara</b> landed a <b style={{color:'#f0d28a'}}>critical</b> Surge!</>, val: 70 + Math.floor(Math.random()*30), sign: '' })],
  Healing:       [() => ({ msg: <><b style={{color:'#c0d8ff'}}>Aelara</b> restored health with <i>Mend</i></>, val: 18 + Math.floor(Math.random()*20), sign: '+' })],
  Challenge:     [() => ({ msg: <>Challenge complete: <b style={{color:'#f0d28a'}}>Slayer III</b></>, val: null })],
  Equipment:     [() => ({ msg: <>Equipped <b style={{color:'#bde0b4'}}>Iron Greaves</b></>, val: null })],
  Debug:         [() => ({ msg: <span style={{opacity:0.7}}>battle.tick resolved · 4ms</span>, val: null })],
};
const PREVIEW_WEIGHTS = [
  ['Damage', 5], ['Damage', 5], ['ItemFound', 2], ['Exp', 2],
  ['EnemyDefeated', 1], ['LevelUp', 1], ['Debug', 2],
  ['CritHit', 2], ['Healing', 2], ['Challenge', 1], ['Equipment', 1],
];

function usePreviewStream() {
  const [events, setEvents] = React.useState([]);
  const seq = React.useRef(0);
  const clock = React.useRef(42);
  React.useEffect(() => {
    const make = () => {
      const [key] = PREVIEW_WEIGHTS[Math.floor(Math.random() * PREVIEW_WEIGHTS.length)];
      const data = PREVIEW_TEMPLATES[key][Math.floor(Math.random() * PREVIEW_TEMPLATES[key].length)]();
      clock.current += 1 + Math.floor(Math.random() * 3);
      const m = String(Math.floor(clock.current / 60) % 100).padStart(2, '0');
      const s = String(clock.current % 60).padStart(2, '0');
      return { id: ++seq.current, key, time: `${m}:${s}`, ...data };
    };
    // seed
    setEvents(Array.from({ length: 9 }, make));
    const iv = setInterval(() => setEvents((e) => [...e.slice(-13), make()]), 1500);
    return () => clearInterval(iv);
  }, []);
  return events;
}

function LivePreview({ prefs, accent }) {
  const events = usePreviewStream();
  const ALL = [...LOG_TYPES, ...EXTRA_LOG_TYPES];
  const colorOf = (k) => ALL.find((t) => t.key === k)?.color ?? 'rgba(240,240,240,0.7)';
  const glyphOf = (k) => ALL.find((t) => t.key === k)?.glyph ?? 'system';
  const shown = events.filter((e) => prefs[e.key]).slice(-9);

  return (
    <div style={{
      display: 'flex', flexDirection: 'column', minHeight: 0, height: '100%',
      background: 'rgba(0,0,0,0.28)',
      border: '1px solid rgba(255,255,255,0.08)', borderRadius: 4,
      overflow: 'hidden',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 9,
        padding: '12px 16px', borderBottom: '1px solid rgba(255,255,255,0.06)',
      }}>
        <span style={{ width: 6, height: 6, borderRadius: '50%', background: accent,
          boxShadow: `0 0 8px ${accent}`, animation: 'op-pulse 1.6s ease-in-out infinite' }} />
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 1.6,
          textTransform: 'uppercase', color: 'rgba(192,216,255,0.7)',
        }}>Live Preview</span>
        <div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.06)' }} />
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 0.5,
          color: 'rgba(240,240,240,0.4)',
        }}>Combat Log</span>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflow: 'hidden', padding: '8px 4px',
        display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
        {shown.length === 0 ? (
          <div style={{
            flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontFamily: 'Geist Mono, monospace', fontSize: 11, letterSpacing: 0.5,
            color: 'rgba(240,240,240,0.35)', textAlign: 'center', padding: 20,
          }}>All log types are disabled —<br />the combat log stays empty.</div>
        ) : shown.map((e, i) => (
          <div key={e.id} style={{
            display: 'flex', alignItems: 'center', gap: 11, padding: '4px 12px',
            opacity: Math.max(0.4, 0.55 + i * 0.05),
            animation: i === shown.length - 1 ? 'op-rowin 360ms ease-out' : 'none',
          }}>
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
              color: 'rgba(240,240,240,0.4)', minWidth: 38, letterSpacing: 0.3 }}>{e.time}</span>
            <OptGlyph kind={glyphOf(e.key)} size={11} color={colorOf(e.key)} />
            <span style={{ flex: 1, fontSize: 12.5, color: 'rgba(240,240,240,0.82)',
              whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{e.msg}</span>
            {e.val != null && (
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                color: colorOf(e.key), letterSpacing: 0.3 }}>{e.sign}{e.val}</span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

/* ── settings category rail ──────────────────────────────────────────── */
function SettingsRail({ active, onPick, accent, showFuture }) {
  const cats = showFuture ? SETTINGS_CATS : SETTINGS_CATS.filter((c) => c.built);
  return (
    <div style={{
      width: 210, flexShrink: 0, borderRight: '1px solid rgba(255,255,255,0.07)',
      padding: '22px 0', display: 'flex', flexDirection: 'column',
    }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 2,
        textTransform: 'uppercase', color: 'rgba(240,240,240,0.4)',
        padding: '0 22px 12px',
      }}>Categories</div>
      {cats.map((c) => {
        const isActive = active === c.key;
        const disabled = !c.built;
        return (
          <button key={c.key}
            onClick={() => c.built && onPick(c.key)}
            disabled={disabled}
            style={{
              position: 'relative', display: 'flex', alignItems: 'center', gap: 12,
              width: '100%', padding: '10px 22px', border: 'none', textAlign: 'left',
              background: isActive ? `${accent}12` : 'transparent',
              color: disabled ? 'rgba(240,240,240,0.32)'
                : isActive ? '#f0f0f0' : 'rgba(240,240,240,0.72)',
              fontFamily: 'inherit', fontSize: 13.5, cursor: disabled ? 'default' : 'pointer',
              transition: 'background 130ms, color 130ms',
            }}>
            <span style={{ position: 'absolute', left: 0, top: 6, bottom: 6, width: 2,
              background: isActive ? accent : 'transparent',
              boxShadow: isActive ? `0 0 10px ${accent}aa` : 'none' }} />
            <OptGlyph kind={c.glyph} size={15} color={isActive ? accent : 'currentColor'} />
            <span style={{ flex: 1 }}>{c.label}</span>
            {disabled && (
              <span style={{
                fontFamily: 'Geist Mono, monospace', fontSize: 8, letterSpacing: 0.6,
                textTransform: 'uppercase', color: 'rgba(240,240,240,0.3)',
                border: '1px solid rgba(240,240,240,0.13)', borderRadius: 2, padding: '1px 4px',
              }}>soon</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

/* ── game nav rail (collapsed) — context that this is an in-game screen ── */
const GAME_NAV = [
  { key: 'fight', label: 'Fight' }, { key: 'inventory', label: 'Inventory' },
  { key: 'challenges', label: 'Challenges' }, { key: 'options', label: 'Options' },
  { key: 'admin', label: 'Admin' },
];
function GameRail({ accent }) {
  return (
    <div style={{
      width: 60, flexShrink: 0, background: '#14151b',
      borderRight: '1px solid rgba(255,255,255,0.08)',
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      paddingTop: 18, gap: 4,
    }}>
      <div style={{ width: 12, height: 12, transform: 'rotate(45deg)',
        border: `1px solid ${accent}`, boxShadow: `0 0 8px ${accent}55`,
        position: 'relative', marginBottom: 18 }}>
        <div style={{ position: 'absolute', inset: 3, background: accent }} />
      </div>
      {GAME_NAV.map((n) => {
        const isActive = n.key === 'options';
        return (
          <div key={n.key} title={n.label} style={{
            position: 'relative', width: '100%', height: 40,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: isActive ? `${accent}14` : 'transparent', cursor: 'default',
          }}>
            <span style={{ position: 'absolute', left: 0, top: 6, bottom: 6, width: 2,
              background: isActive ? accent : 'transparent',
              boxShadow: isActive ? `0 0 10px ${accent}aa` : 'none' }} />
            <OptGlyph kind={n.key} size={17} color={isActive ? accent : 'rgba(240,240,240,0.6)'} />
          </div>
        );
      })}
    </div>
  );
}

/* ── save / discard footer ───────────────────────────────────────────── */
function SaveBar({ dirtyCount, saved, accent, onSave, onDiscard }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '14px 24px', borderTop: '1px solid rgba(255,255,255,0.07)',
      background: 'rgba(0,0,0,0.2)', flexShrink: 0,
    }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 11.5, letterSpacing: 0.4,
        color: 'rgba(240,240,240,0.55)', display: 'flex', alignItems: 'center', gap: 9,
      }}>
        {saved ? (
          <span style={{ color: '#bde0b4', display: 'inline-flex', alignItems: 'center', gap: 7 }}>
            <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.6">
              <path d="M3 7.3L6 10.2L11 4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            Preferences saved
          </span>
        ) : dirtyCount === 0 ? (
          <span>No unsaved changes</span>
        ) : (
          <>
            <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#f0d28a',
              boxShadow: '0 0 6px rgba(240,210,138,0.9)' }} />
            <span style={{ color: 'rgba(240,240,240,0.8)' }}>
              {dirtyCount} unsaved {dirtyCount === 1 ? 'change' : 'changes'}
            </span>
          </>
        )}
      </div>
      <div style={{ display: 'flex', gap: 10 }}>
        <DSButton variant="ghost" disabled={dirtyCount === 0} onClick={onDiscard} accent={accent}>Discard</DSButton>
        <DSButton variant="primary" disabled={dirtyCount === 0} onClick={onSave} accent={accent}>Save Changes</DSButton>
      </div>
    </div>
  );
}

function DSButton({ children, variant, disabled, onClick, accent }) {
  const [hover, setHover] = React.useState(false);
  const primary = variant === 'primary';
  return (
    <button
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      disabled={disabled}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 7,
        fontFamily: 'Geist Mono, monospace', fontSize: 11.5, letterSpacing: 0.6,
        textTransform: 'uppercase', padding: '8px 18px', borderRadius: 3,
        cursor: disabled ? 'not-allowed' : 'pointer', transition: 'all 140ms ease',
        background: disabled ? 'transparent'
          : primary ? `${accent}1f` : hover ? 'rgba(255,255,255,0.05)' : 'transparent',
        border: `1px solid ${disabled ? 'rgba(255,255,255,0.08)'
          : primary ? accent : hover ? 'rgba(255,255,255,0.3)' : 'rgba(255,255,255,0.16)'}`,
        color: disabled ? 'rgba(240,240,240,0.3)'
          : primary ? '#c0d8ff' : 'rgba(240,240,240,0.85)',
        boxShadow: hover && !disabled && primary ? `0 0 12px ${accent}55` : 'none',
      }}>
      {children}
    </button>
  );
}

window.OptionsParts = { LivePreview, SettingsRail, GameRail, SaveBar, DSButton };
