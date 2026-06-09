/* Shared bits for the game-screen nav variants:
   - SCREENS list (mirrors src/routes/game/screens/types.ts)
   - useNav() hook (selection + last-clicked timestamp for transitions)
   - <FightScreenMock /> realistic content for the default screen
   - <PlaceholderScreen /> for not-yet-built screens
   - <LogPanel /> minimal combat log
   - <DiamondMark /> + <Tick /> small chrome bits
*/

const SCREENS = [
  { key: 'fight',      label: 'Fight',      group: 'play',  built: true },
  { key: 'cardGame',   label: 'Card Game',  group: 'play',  built: false },
  { key: 'challenges', label: 'Challenges', group: 'play',  built: true },
  { key: 'inventory',  label: 'Inventory',  group: 'hero',  built: true },
  { key: 'attributes', label: 'Attributes', group: 'hero',  built: false },
  { key: 'stats',      label: 'Stats',      group: 'hero',  built: false },
  { key: 'options',    label: 'Options',    group: 'meta',  built: false },
  { key: 'help',       label: 'Help',       group: 'meta',  built: false },
  { key: 'quit',       label: 'Quit',       group: 'meta',  built: false },
  { key: 'admin',      label: 'Admin',      group: 'admin', built: true },
];

const GROUP_META = {
  play: { label: 'Combat' },
  hero: { label: 'Character' },
  meta: { label: 'Settings' },
  admin: { label: 'Admin' },
};

function useNav(initial = 'fight') {
  const [active, setActive] = React.useState(initial);
  return { active, setActive };
}

/* ─── small chrome ─────────────────────────────────────────────────── */

function GameDiamond({ size = 12, color = '#a1c2f7', pulse = false }) {
  return (
    <div style={{
      width: size, height: size, transform: 'rotate(45deg)',
      border: `1px solid ${color}`,
      boxShadow: `0 0 8px ${color}55`,
      position: 'relative',
      animation: pulse ? 'gd-pulse 1.8s ease-in-out infinite' : 'none',
    }}>
      <div style={{ position: 'absolute', inset: size > 14 ? 4 : 3, background: color }} />
      <style>{`@keyframes gd-pulse {
        0%, 100% { box-shadow: 0 0 6px ${color}44; }
        50% { box-shadow: 0 0 14px ${color}cc; }
      }`}</style>
    </div>
  );
}

function Wordmark({ size = 11 }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
    }}>
      <GameDiamond size={11} pulse />
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: size,
        letterSpacing: 2, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.92)',
      }}>Tactic Foundry</div>
    </div>
  );
}

function TickCounter() {
  // animate two faux tick rates so it looks alive
  const [t, setT] = React.useState({ logic: 60, render: 60 });
  React.useEffect(() => {
    const id = setInterval(() => setT({
      logic: 58 + Math.round(Math.random() * 4),
      render: 58 + Math.round(Math.random() * 4),
    }), 600);
    return () => clearInterval(id);
  }, []);
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
      color: 'rgba(240,240,240,0.55)', letterSpacing: 0.5,
    }}>
      <span title="Logic tick rate">L · {t.logic}</span>
      <span style={{ opacity: 0.4 }}>·</span>
      <span title="Render tick rate">R · {t.render}</span>
    </div>
  );
}

/* ─── Fight screen mock ───────────────────────────────────────────── */

function FightScreenMock() {
  return (
    <div style={{ padding: '28px 36px 0', height: '100%', display: 'flex', flexDirection: 'column', gap: 24 }}>
      <ZoneNavMock />
      <div style={{ display: 'flex', gap: 28, justifyContent: 'center', alignItems: 'flex-start', flex: 1 }}>
        <BattlerCardMock
          name="Aelara"
          level={12}
          hp={228} maxHp={300}
          skills={[
            { name: 'Cleave',  charge: 0.85, icon: 'sword' },
            { name: 'Guard',   charge: 0.40, icon: 'shield' },
            { name: 'Mend',    charge: 0.20, icon: 'leaf' },
            { name: 'Surge',   charge: 1.00, icon: 'bolt' },
          ]}
          side="player"
        />
        <BattlerCardMock
          name="Skeleton Mage"
          level={11}
          hp={92} maxHp={220}
          skills={[
            { name: 'Hex',    charge: 0.62, icon: 'rune' },
            { name: 'Drain',  charge: 0.30, icon: 'rune' },
            { name: '—',      charge: 0,    icon: null },
            { name: '—',      charge: 0,    icon: null },
          ]}
          side="enemy"
        />
      </div>
    </div>
  );
}

function ZoneNavMock() {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14,
      alignSelf: 'center',
      background: 'rgba(255,255,255,0.04)',
      border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 3,
      padding: '6px 14px',
    }}>
      <ZoneArrow dir="left" />
      <div style={{
        display: 'flex', alignItems: 'baseline', gap: 8,
        minWidth: 240, justifyContent: 'center',
      }}>
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
          letterSpacing: 1.5, textTransform: 'uppercase',
          color: 'rgba(192,216,255,0.85)',
        }}>Zone 03</span>
        <span style={{ fontSize: 16, color: '#f0f0f0', fontWeight: 400, letterSpacing: 0.2 }}>
          Forgotten Catacombs
        </span>
      </div>
      <ZoneArrow dir="right" />
    </div>
  );
}

function ZoneArrow({ dir }) {
  return (
    <button style={{
      width: 26, height: 26,
      background: 'transparent',
      border: '1px solid rgba(255,255,255,0.18)',
      color: 'rgba(240,240,240,0.85)',
      borderRadius: 2,
      cursor: 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist Mono, monospace', fontSize: 11,
      transition: 'all 140ms',
    }}>{dir === 'left' ? '‹' : '›'}</button>
  );
}

function BattlerCardMock({ name, level, hp, maxHp, skills, side }) {
  const pct = Math.max(0, Math.min(100, (hp / maxHp) * 100));
  const accent = side === 'player' ? '#a1c2f7' : '#e08a78';
  return (
    <div style={{
      flex: '1 1 0',
      maxWidth: 380,
      background: 'rgba(255,255,255,0.03)',
      border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 4,
      padding: '16px 18px',
      color: '#f0f0f0',
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{
            width: 4, height: 16, background: accent,
            boxShadow: `0 0 8px ${accent}66`,
          }} />
          <span style={{ fontSize: 17, fontWeight: 500, letterSpacing: 0.1 }}>{name}</span>
        </div>
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
          color: 'rgba(240,240,240,0.6)', letterSpacing: 0.5,
        }}>LV · {level}</span>
      </div>

      {/* health */}
      <div style={{
        position: 'relative', height: 16,
        background: 'rgba(224,138,120,0.25)',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 2, overflow: 'hidden',
        marginBottom: 14,
      }}>
        <div style={{
          position: 'absolute', inset: 0, width: `${pct}%`,
          background: 'linear-gradient(180deg, #7fc28b 0%, #5da66a 100%)',
          transition: 'width 200ms',
        }} />
        <div style={{
          position: 'absolute', inset: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'Geist Mono, monospace', fontSize: 11,
          color: '#fff', textShadow: '0 1px 2px rgba(0,0,0,0.6)',
          letterSpacing: 0.4,
        }}>{hp} / {maxHp}</div>
      </div>

      {/* skills row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 6 }}>
        {skills.map((s, i) => <SkillSlot key={i} skill={s} accent={accent} />)}
      </div>
    </div>
  );
}

function SkillSlot({ skill, accent }) {
  const filled = skill.icon != null;
  const charge = skill.charge ?? 0;
  return (
    <div style={{
      aspectRatio: '1', position: 'relative',
      background: filled ? 'rgba(255,255,255,0.06)' : 'rgba(255,255,255,0.02)',
      border: '1px solid rgba(255,255,255,0.14)',
      borderRadius: 2,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      overflow: 'hidden',
    }}>
      {filled && <SkillIcon kind={skill.icon} />}
      {filled && charge < 1 && (
        <div style={{
          position: 'absolute', inset: 0,
          background: `conic-gradient(rgba(0,0,0,0.6) ${charge * 360}deg, transparent 0deg)`,
          pointerEvents: 'none',
        }} />
      )}
      {filled && charge >= 1 && (
        <div style={{
          position: 'absolute', inset: 0,
          boxShadow: `inset 0 0 6px ${accent}80`,
          pointerEvents: 'none',
        }} />
      )}
    </div>
  );
}

function SkillIcon({ kind }) {
  // simple geometric placeholders — game's real icons load over these
  const s = { width: 18, height: 18, fill: 'none', stroke: 'rgba(240,240,240,0.85)', strokeWidth: 1.4 };
  if (kind === 'sword')  return <svg {...s} viewBox="0 0 16 16"><path d="M11 1.5l3.5 3.5-7 7-3.5-3.5zM3 11l2 2M2 13l1 1" strokeLinecap="round" strokeLinejoin="round" /></svg>;
  if (kind === 'shield') return <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l5 1.5v5c0 3-2 5-5 7-3-2-5-4-5-7v-5z" strokeLinejoin="round" /></svg>;
  if (kind === 'leaf')   return <svg {...s} viewBox="0 0 16 16"><path d="M3 13c0-6 4-10 10-10 0 6-4 10-10 10zM3 13l4-4" strokeLinecap="round" strokeLinejoin="round" /></svg>;
  if (kind === 'bolt')   return <svg {...s} viewBox="0 0 16 16"><path d="M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z" strokeLinejoin="round" /></svg>;
  if (kind === 'rune')   return <svg {...s} viewBox="0 0 16 16"><path d="M8 2v12M4 5l8 6M12 5l-8 6" strokeLinecap="round" /></svg>;
  return null;
}

/* ─── Placeholder screen for unbuilt routes ───────────────────────── */

function PlaceholderScreen({ label }) {
  return (
    <div style={{
      height: '100%', padding: 36,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      flexDirection: 'column', gap: 14,
    }}>
      <div style={{
        width: 64, height: 64, borderRadius: 2,
        border: '1px dashed rgba(240,240,240,0.18)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <GameDiamond size={16} color="rgba(240,240,240,0.4)" />
      </div>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: 18, color: '#f0f0f0', fontWeight: 400, letterSpacing: -0.1 }}>{label}</div>
        <div style={{
          marginTop: 4,
          fontFamily: 'Geist Mono, monospace', fontSize: 11,
          color: 'rgba(240,240,240,0.5)', letterSpacing: 1, textTransform: 'uppercase',
        }}>Not yet implemented</div>
      </div>
    </div>
  );
}

/* ─── Combat log ──────────────────────────────────────────────────── */

const SAMPLE_LOGS = [
  { t: '00:42', text: 'Aelara casts Cleave on Skeleton Mage — 38 damage.', kind: 'hit' },
  { t: '00:42', text: 'Skeleton Mage casts Hex on Aelara — 12 damage.',     kind: 'enemy' },
  { t: '00:41', text: 'Aelara picked up: Sapphire Shard ×1.',                kind: 'loot' },
  { t: '00:41', text: 'Surge is ready.',                                     kind: 'system' },
  { t: '00:40', text: 'Aelara casts Cleave on Skeleton Mage — 41 damage.',   kind: 'hit' },
  { t: '00:40', text: 'Skeleton Mage casts Drain on Aelara — 8 damage.',     kind: 'enemy' },
  { t: '00:39', text: 'Aelara entered Forgotten Catacombs.',                 kind: 'system' },
];

function LogPanel() {
  return (
    <div style={{
      borderTop: '1px solid rgba(255,255,255,0.08)',
      background: 'rgba(0,0,0,0.25)',
      padding: '10px 24px',
      height: 130, overflow: 'hidden',
      position: 'relative',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 10,
        marginBottom: 6,
      }}>
        <div style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
          letterSpacing: 1.5, textTransform: 'uppercase',
          color: 'rgba(192,216,255,0.7)',
        }}>Combat Log</div>
        <div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.07)' }} />
      </div>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 11,
        lineHeight: 1.55,
      }}>
        {SAMPLE_LOGS.slice(0, 5).map((l, i) => (
          <div key={i} style={{
            display: 'flex', gap: 10,
            opacity: 1 - i * 0.13,
          }}>
            <span style={{ color: 'rgba(240,240,240,0.4)', minWidth: 36 }}>{l.t}</span>
            <span style={{
              color:
                l.kind === 'hit'    ? '#c0d8ff'
                : l.kind === 'enemy'  ? '#e8b6a6'
                : l.kind === 'loot'   ? '#bde0b4'
                : 'rgba(240,240,240,0.65)',
            }}>{l.text}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ─── Game frame shell ────────────────────────────────────────────── */

function GameContent({ active, fightScreen }) {
  const screen = SCREENS.find((s) => s.key === active) ?? SCREENS[0];
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
      <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        {screen.key === 'fight'
          ? (fightScreen || <FightScreenMock />)
          : <PlaceholderScreen label={screen.label} />}
      </div>
      <LogPanel />
    </div>
  );
}

Object.assign(window, {
  SCREENS, GROUP_META, useNav,
  GameDiamond, Wordmark, TickCounter,
  FightScreenMock, PlaceholderScreen, LogPanel, GameContent,
});
