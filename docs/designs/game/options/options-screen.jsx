/* Options Screen — the old "Logging Options" popup, reimagined as a full,
   extensible in-game settings screen in the new UI's aesthetic.

   Structure:
     <GameRail>        collapsed game nav (context — Options is active)
     <SettingsRail>    settings categories (Logging built; others wip)
     <LoggingPanel>    the active category's content
       · per-log-type rows with toggle/select controls
       · live combat-log preview, filtered by the enabled types
       · Save / Discard footer that only persists actual changes
   The category list + log-type list are plain config arrays, so adding a
   future settings category (or a new ELogType) is a one-line edit. */

/* ── log groups — sections keep a long list scannable as it grows ─────── */
const LOG_GROUPS = [
  { key: 'combat',      label: 'Combat' },
  { key: 'progression', label: 'Progression' },
  { key: 'items',       label: 'Items' },
  { key: 'system',      label: 'System' },
];

/* ── log types (mirrors ELogType in api/types/enums.ts) ──────────────── */
const LOG_TYPES = [
  { key: 'Damage',        group: 'combat', glyph: 'hit',    color: '#c0d8ff',
    name: 'Combat Damage',  desc: 'Hits you deal and take during battle.',     def: true  },
  { key: 'EnemyDefeated', group: 'combat', glyph: 'kill',   color: '#bde0b4',
    name: 'Enemy Defeated', desc: 'Victories and your own defeats.',           def: true  },
  { key: 'ItemFound',     group: 'items',  glyph: 'loot',   color: '#bde0b4',
    name: 'Items Found',    desc: 'Loot dropped by enemies and zones.',        def: true  },
  { key: 'Exp',           group: 'progression', glyph: 'crit',   color: '#f0d28a',
    name: 'Experience',     desc: 'XP earned from winning battles.',           def: true  },
  { key: 'LevelUp',       group: 'progression', glyph: 'level',  color: '#f0d28a',
    name: 'Level Up',       desc: 'Alerts when your hero reaches a new level.', def: true  },
  { key: 'Debug',         group: 'system', glyph: 'system', color: 'rgba(240,240,240,0.7)',
    name: 'Debug',          desc: 'Verbose engine output, for troubleshooting.', def: false },
];

/* ── speculative future log types — used by the "expanded" Tweak to show
   how the grouped layout scales. Not wired to a real ELogType yet. ────── */
const EXTRA_LOG_TYPES = [
  { key: 'CritHit',      group: 'combat', glyph: 'crit',   color: '#f0d28a',
    name: 'Critical Hits',  desc: 'Call out high-damage critical strikes.',    def: true  },
  { key: 'Healing',      group: 'combat', glyph: 'heal',   color: '#bde0b4',
    name: 'Healing',        desc: 'Health restored by skills and effects.',    def: true  },
  { key: 'StatusEffect', group: 'combat', glyph: 'status', color: '#e8b6a6',
    name: 'Status Effects', desc: 'Buffs and debuffs applied or expired.',     def: false },
  { key: 'DodgeBlock',   group: 'combat', glyph: 'shield', color: '#c0d8ff',
    name: 'Dodges & Blocks',desc: 'Attacks you avoid or absorb.',             def: false },
  { key: 'Challenge',    group: 'progression', glyph: 'challenges', color: '#f0d28a',
    name: 'Challenges',     desc: 'Challenge progress and completions.',       def: true  },
  { key: 'Attribute',    group: 'progression', glyph: 'attributes', color: '#f0d28a',
    name: 'Attribute Points',desc: 'Points earned and ready to spend.',        def: true  },
  { key: 'Equipment',    group: 'items',  glyph: 'inventory', color: '#bde0b4',
    name: 'Equipment Changes',desc: 'Gear equipped, swapped, or removed.',     def: true  },
  { key: 'RareDrop',     group: 'items',  glyph: 'loot',   color: '#f0d28a',
    name: 'Rare Drops',     desc: 'Highlight uncommon-or-better loot.',        def: true  },
  { key: 'Connection',   group: 'system', glyph: 'system', color: 'rgba(240,240,240,0.7)',
    name: 'Connection',     desc: 'Server connection and sync notices.',       def: false },
];

/* ── settings categories — Logging is built; the rest demonstrate that the
   shell extends cleanly. Flip `built` to wire up a new category. ───────── */
const SETTINGS_CATS = [
  { key: 'logging',  label: 'Logging',  glyph: 'logging',  built: true,
    blurb: 'Combat Log' },
  { key: 'display',  label: 'Display',  glyph: 'display',  built: false },
  { key: 'audio',    label: 'Audio',    glyph: 'audio',    built: false },
  { key: 'gameplay', label: 'Gameplay', glyph: 'gameplay', built: false },
  { key: 'account',  label: 'Account',  glyph: 'account',  built: false },
];

/* ── glyphs — small abstract marks, same family as KindGlyph/SideGlyph ── */
function OptGlyph({ kind, size = 14, color = 'currentColor' }) {
  const s = { width: size, height: size, fill: 'none', stroke: color, strokeWidth: 1.4,
    strokeLinecap: 'round', strokeLinejoin: 'round' };
  switch (kind) {
    // log-type marks
    case 'hit':    return <svg {...s} viewBox="0 0 14 14"><path d="M10 1.5l2.5 2.5-7 7-2.5-2.5z M3 11l1.5 1.5" /></svg>;
    case 'crit':   return <svg {...s} viewBox="0 0 14 14"><path d="M7 1.5l1.6 3.3 3.6.5-2.6 2.6.6 3.6L7 9.7 3.8 11.5l.6-3.6-2.6-2.6 3.6-.5z" /></svg>;
    case 'loot':   return <svg {...s} viewBox="0 0 14 14"><path d="M2.5 4.5h9v8h-9z M5 4.5V3a2 2 0 014 0v1.5" /></svg>;
    case 'kill':   return <svg {...s} viewBox="0 0 14 14"><path d="M7 2v10M2 7h10" /></svg>;
    case 'level':  return <svg {...s} viewBox="0 0 14 14"><path d="M3.5 7.5L7 4l3.5 3.5M3.5 11L7 7.5l3.5 3.5" /></svg>;
    case 'heal':   return <svg {...s} viewBox="0 0 14 14"><circle cx="7" cy="7" r="5" /><path d="M7 4.3v5.4M4.3 7h5.4" /></svg>;
    case 'status': return <svg {...s} viewBox="0 0 14 14"><path d="M7 2C7 2 4 5.5 4 8a3 3 0 006 0c0-2.5-3-6-3-6z" /></svg>;
    case 'shield': return <svg {...s} viewBox="0 0 14 14"><path d="M7 1.8l4 1.3v3.5c0 2.4-1.7 3.9-4 4.8-2.3-0.9-4-2.4-4-4.8V3.1z" /></svg>;
    case 'system': return <svg {...s} viewBox="0 0 14 14"><circle cx="7" cy="7" r="4.5" /><path d="M7 5v3M7 9.5v0.01" /></svg>;
    // settings-category marks
    case 'logging':  return <svg {...s} viewBox="0 0 16 16"><path d="M3 3.5h10M3 6.5h10M3 9.5h7M3 12.5h5" /></svg>;
    case 'display':  return <svg {...s} viewBox="0 0 16 16"><rect x="2" y="3" width="12" height="8.5" rx="1" /><path d="M6 14h4M8 11.5V14" /></svg>;
    case 'audio':    return <svg {...s} viewBox="0 0 16 16"><path d="M3 6v4h2.5L9 13V3L5.5 6z" /><path d="M11.5 5.5a3.5 3.5 0 010 5" /></svg>;
    case 'gameplay': return <svg {...s} viewBox="0 0 16 16"><rect x="2.5" y="5" width="11" height="6.5" rx="3.25" /><path d="M5 8h2M6 7v2" /><circle cx="10.7" cy="8" r="0.6" fill={color} stroke="none" /></svg>;
    case 'account':  return <svg {...s} viewBox="0 0 16 16"><circle cx="8" cy="5.5" r="2.5" /><path d="M3.5 13c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4" /></svg>;
    // game-nav marks (collapsed rail context)
    case 'fight':      return <svg {...s} viewBox="0 0 16 16"><path d="M11 1.5l3.5 3.5-7.5 7.5-3.5-3.5z M3 12l1.7 1.7" /></svg>;
    case 'inventory':  return <svg {...s} viewBox="0 0 16 16"><rect x="2" y="4" width="12" height="9.5" rx="0.5" /><path d="M5.5 4V2.5h5V4" /></svg>;
    case 'challenges': return <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l1.8 3.7 4.1.6-3 2.9.7 4.1L8 11l-3.6 1.8.7-4.1-3-2.9 4.1-.6z" /></svg>;
    case 'options':    return <svg {...s} viewBox="0 0 16 16"><circle cx="8" cy="8" r="2.2" /><path d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.5 1.5M11.1 11.1l1.5 1.5M3.4 12.6l1.5-1.5M11.1 4.9l1.5-1.5" /></svg>;
    case 'admin':      return <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l5 1.7v4.5c0 2.9-2.2 4.7-5 5.8-2.8-1.1-5-2.9-5-5.8V3.2z" /><path d="M5.6 8l1.7 1.7L10.5 6.5" /></svg>;
    default: return null;
  }
}

/* ── controls ────────────────────────────────────────────────────────── */

/* Toggle switch — accent glow when on, muted when off. */
function Toggle({ on, onClick, accent }) {
  return (
    <button
      onClick={onClick}
      role="switch"
      aria-checked={on}
      style={{
        position: 'relative', width: 40, height: 22, flexShrink: 0,
        borderRadius: 999, cursor: 'pointer', padding: 0,
        border: `1px solid ${on ? accent : 'rgba(255,255,255,0.2)'}`,
        background: on ? `${accent}26` : 'rgba(255,255,255,0.04)',
        boxShadow: on ? `0 0 10px ${accent}55, inset 0 0 6px ${accent}22` : 'none',
        transition: 'all 160ms ease',
      }}>
      <span style={{
        position: 'absolute', top: 2, left: on ? 19 : 2,
        width: 16, height: 16, borderRadius: '50%',
        background: on ? accent : 'rgba(240,240,240,0.55)',
        boxShadow: on ? `0 0 8px ${accent}` : 'none',
        transition: 'left 160ms cubic-bezier(.4,0,.2,1), background 160ms',
      }} />
    </button>
  );
}

/* Group toggle — reflects all-on / all-off / mixed for a whole section.
   Click turns the group all-on, or all-off if it's already fully on. */
function GroupToggle({ on, mixed, onClick, accent }) {
  const lit = on || mixed;
  return (
    <button
      onClick={onClick}
      title={on ? 'Disable all in group' : 'Enable all in group'}
      style={{
        position: 'relative', width: 36, height: 20, flexShrink: 0,
        borderRadius: 999, cursor: 'pointer', padding: 0,
        border: `1px solid ${lit ? accent : 'rgba(255,255,255,0.2)'}`,
        background: on ? `${accent}26` : mixed ? `${accent}14` : 'rgba(255,255,255,0.04)',
        boxShadow: on ? `0 0 8px ${accent}44` : 'none',
        transition: 'all 160ms ease',
      }}>
      <span style={{
        position: 'absolute', top: 2,
        left: on ? 17 : mixed ? 9 : 2,
        width: 14, height: 14, borderRadius: '50%',
        background: lit ? accent : 'rgba(240,240,240,0.55)',
        boxShadow: on ? `0 0 7px ${accent}` : 'none',
        transition: 'left 160ms cubic-bezier(.4,0,.2,1), background 160ms',
      }} />
    </button>
  );
}

/* Select — mirrors the original Enabled/Disabled dropdown, in DS skin. */
function EnabledSelect({ on, onChange, accent }) {
  return (
    <div style={{ position: 'relative', width: 116, flexShrink: 0 }}>
      <select
        value={on ? 'true' : 'false'}
        onChange={(e) => onChange(e.target.value === 'true')}
        style={{
          width: '100%', appearance: 'none', WebkitAppearance: 'none',
          background: 'rgba(255,255,255,0.04)',
          border: `1px solid ${on ? `${accent}99` : 'rgba(255,255,255,0.16)'}`,
          color: on ? '#f0f0f0' : 'rgba(240,240,240,0.6)',
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5, letterSpacing: 0.5,
          padding: '6px 26px 6px 11px', borderRadius: 3, cursor: 'pointer',
          outline: 'none', transition: 'border-color 140ms, color 140ms',
        }}>
        <option value="true">Enabled</option>
        <option value="false">Disabled</option>
      </select>
      <svg width="10" height="10" viewBox="0 0 10 10" fill="none"
        stroke="rgba(240,240,240,0.55)" strokeWidth="1.4"
        style={{ position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }}>
        <path d="M2 3.5L5 6.5L8 3.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </div>
  );
}

/* ── one log-type row ────────────────────────────────────────────────── */
function LogRow({ type, on, dirty, control, accent, onToggle }) {
  const [hover, setHover] = React.useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 16,
        padding: '14px 18px',
        background: hover ? 'rgba(255,255,255,0.025)' : 'transparent',
        borderBottom: '1px solid rgba(255,255,255,0.05)',
        transition: 'background 130ms',
        position: 'relative',
      }}>
      {/* dirty marker */}
      <span style={{
        position: 'absolute', left: 0, top: 8, bottom: 8, width: 2,
        background: dirty ? '#f0d28a' : 'transparent',
        boxShadow: dirty ? '0 0 8px rgba(240,210,138,0.8)' : 'none',
        transition: 'background 140ms',
      }} />
      {/* glyph chip */}
      <div style={{
        width: 34, height: 34, flexShrink: 0, borderRadius: 3,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: on ? `${type.color}1a` : 'rgba(255,255,255,0.03)',
        border: `1px solid ${on ? `${type.color}55` : 'rgba(255,255,255,0.1)'}`,
        transition: 'all 160ms',
      }}>
        <OptGlyph kind={type.glyph} size={16} color={on ? type.color : 'rgba(240,240,240,0.4)'} />
      </div>
      {/* label + desc */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 9,
          fontSize: 14.5, color: on ? '#f0f0f0' : 'rgba(240,240,240,0.62)',
          letterSpacing: -0.1, marginBottom: 2, transition: 'color 140ms',
        }}>
          {type.name}
          {dirty && (
            <span style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 8.5, letterSpacing: 0.8,
              textTransform: 'uppercase', color: '#f0d28a',
              border: '1px solid rgba(240,210,138,0.4)', borderRadius: 2,
              padding: '0px 4px',
            }}>edited</span>
          )}
        </div>
        <div style={{
          fontSize: 12.5, color: 'rgba(240,240,240,0.45)', letterSpacing: 0,
        }}>{type.desc}</div>
      </div>
      {/* control */}
      {control === 'select'
        ? <EnabledSelect on={on} onChange={onToggle} accent={accent} />
        : <Toggle on={on} onClick={() => onToggle(!on)} accent={accent} />}
    </div>
  );
}

window.OptionsConfig = { LOG_TYPES, EXTRA_LOG_TYPES, LOG_GROUPS, SETTINGS_CATS };
Object.assign(window, { OptGlyph, Toggle, GroupToggle, EnabledSelect, LogRow });
