/* Shared simulation + primitives for Fight screen variants.

   useFightSim() runs a continuous battle:
     - elapsed time drives skill cooldowns: progress = (elapsed % cd) / cd
     - every ~800-1400ms a random side takes damage (15-40, or crit 45-80)
     - damaged side loses HP; on KO both sides reset to a fresh state
     - damage bubbles flow into a list; CSS animation drives float+fade
     - bubbles auto-expire on animation end via onDone
*/

const PLAYER_BASE = { name: 'Aelara', lvl: 12, maxHp: 300, hp: 228 };
const ENEMY_BASE = { name: 'Skeleton Mage', lvl: 11, maxHp: 220, hp: 152 };

const PLAYER_SKILLS = [
  { name: 'Cleave', icon: 'sword',  cdMs: 1800 },
  { name: 'Guard',  icon: 'shield', cdMs: 2400 },
  { name: 'Mend',   icon: 'leaf',   cdMs: 3200 },
  { name: 'Surge',  icon: 'bolt',   cdMs: 1300 },
];
const ENEMY_SKILLS = [
  { name: 'Hex',    icon: 'rune', cdMs: 2600 },
  { name: 'Drain',  icon: 'rune', cdMs: 1900 },
];

function useFightSim() {
  const [, force] = React.useReducer((x) => x + 1, 0);
  const startRef = React.useRef(performance.now());
  const lastSpawnRef = React.useRef(performance.now() + 400);
  const hpRef = React.useRef({ player: PLAYER_BASE.hp, enemy: ENEMY_BASE.hp });
  const dispHpRef = React.useRef({ player: PLAYER_BASE.hp, enemy: ENEMY_BASE.hp }); // for "disappearing" red layer
  const damagesRef = React.useRef([]);
  const seqRef = React.useRef(0);

  React.useEffect(() => {
    let raf;
    const loop = () => {
      const now = performance.now();

      // smooth disappearing-hp lerp
      ['player', 'enemy'].forEach((k) => {
        const d = dispHpRef.current[k];
        const target = hpRef.current[k];
        if (d > target) dispHpRef.current[k] = Math.max(target, d - 0.6);
      });

      // spawn damage on interval
      const since = now - lastSpawnRef.current;
      const wait = 800 + (seqRef.current % 5) * 120; // 800-1280ms cadence
      if (since > wait) {
        const target = Math.random() > 0.5 ? 'enemy' : 'player';
        const isCrit = Math.random() > 0.82;
        const value = isCrit
          ? Math.floor(45 + Math.random() * 28)
          : Math.floor(14 + Math.random() * 24);
        hpRef.current[target] = Math.max(0, hpRef.current[target] - value);

        damagesRef.current.push({
          id: ++seqRef.current,
          side: target,             // which side took damage
          value, isCrit,
          jitter: (Math.random() - 0.5) * 32,
          spawn: now,
        });

        // reset on KO
        if (hpRef.current[target] === 0) {
          setTimeout(() => {
            hpRef.current.player = PLAYER_BASE.hp;
            hpRef.current.enemy = ENEMY_BASE.hp;
            dispHpRef.current.player = PLAYER_BASE.hp;
            dispHpRef.current.enemy = ENEMY_BASE.hp;
          }, 600);
        }
        lastSpawnRef.current = now;
      }

      // prune expired
      damagesRef.current = damagesRef.current.filter((d) => now - d.spawn < 1500);

      force();
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, []);

  const elapsed = performance.now() - startRef.current;
  return {
    elapsed,
    player: { ...PLAYER_BASE, hp: hpRef.current.player, dispHp: dispHpRef.current.player },
    enemy:  { ...ENEMY_BASE,  hp: hpRef.current.enemy,  dispHp: dispHpRef.current.enemy },
    damages: damagesRef.current,
  };
}

function skillProgress(elapsed, cdMs, offset = 0) {
  const phase = (elapsed + offset) % cdMs;
  return phase / cdMs; // 0..1
}

/* ─── Refined ZoneNav (one shared treatment across all variants) ── */

function FightZoneNav({ zoneNum = 3, zoneName = 'Forgotten Catacombs' }) {
  return (
    <div style={{
      display: 'inline-flex', alignItems: 'center', gap: 12,
      background: 'rgba(255,255,255,0.04)',
      border: '1px solid rgba(255,255,255,0.14)',
      borderRadius: 3,
      padding: '6px 10px 6px 6px',
    }}>
      <ZoneBtn>‹</ZoneBtn>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
          letterSpacing: 1.5, textTransform: 'uppercase',
          color: 'rgba(192,216,255,0.85)',
        }}>Zone · {String(zoneNum).padStart(2, '0')}</span>
        <span style={{ fontSize: 16, color: '#f0f0f0', fontWeight: 400, letterSpacing: 0.1 }}>
          {zoneName}
        </span>
      </div>
      <ZoneBtn>›</ZoneBtn>
    </div>
  );
}

function ZoneBtn({ children }) {
  return (
    <button style={{
      width: 24, height: 24,
      background: 'rgba(255,255,255,0.03)',
      border: '1px solid rgba(255,255,255,0.18)',
      color: 'rgba(240,240,240,0.85)',
      borderRadius: 2, cursor: 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist Mono, monospace', fontSize: 12,
      transition: 'background 140ms',
    }}>{children}</button>
  );
}

/* ─── Damage bubble — CSS-driven rise + fade ────────────────────── */

function DamageBubble({ value, isCrit, side, jitter, x = '50%', y = '40%' }) {
  const color = side === 'player' ? '#f0a094' : '#f5e8c8';
  return (
    <div style={{
      position: 'absolute',
      left: `calc(${x} + ${jitter}px)`,
      top: y,
      transform: 'translate(-50%, 0)',
      pointerEvents: 'none',
      animation: 'dmg-rise 1.3s cubic-bezier(.2,.6,.2,1) forwards',
      color,
      fontFamily: 'Geist, sans-serif',
      fontWeight: isCrit ? 700 : 600,
      fontSize: isCrit ? 26 : 18,
      letterSpacing: isCrit ? -0.4 : -0.2,
      textShadow: '0 2px 8px rgba(0,0,0,0.85), 0 0 12px ' + color + '88',
      whiteSpace: 'nowrap',
      zIndex: 5,
    }}>
      {isCrit && (
        <span style={{
          fontFamily: 'Geist Mono, monospace',
          fontSize: 9, letterSpacing: 1.4, textTransform: 'uppercase',
          color, marginRight: 6,
          textShadow: '0 1px 4px rgba(0,0,0,0.85)',
          verticalAlign: 'middle',
        }}>crit</span>
      )}
      −{value}
      <style>{`
        @keyframes dmg-rise {
          0%   { transform: translate(-50%, 8px) scale(0.85); opacity: 0; }
          15%  { transform: translate(-50%, 0)   scale(1.05); opacity: 1; }
          25%  { transform: translate(-50%, -4px) scale(1);    opacity: 1; }
          85%  { opacity: 1; }
          100% { transform: translate(-50%, -64px); opacity: 0; }
        }
      `}</style>
    </div>
  );
}

/* ─── HP bar — two-layer with disappearing red lerp ─────────────── */

function HpBar({ hp, dispHp, maxHp, accent = '#7fc28b', height = 18, showText = true }) {
  const pct = Math.max(0, (hp / maxHp) * 100);
  const dispPct = Math.max(0, (dispHp / maxHp) * 100);
  return (
    <div style={{
      position: 'relative', height,
      background: 'rgba(224,138,120,0.18)',
      border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 2, overflow: 'hidden',
    }}>
      <div style={{
        position: 'absolute', inset: 0, width: `${dispPct}%`,
        background: 'rgba(224,138,120,0.55)',
      }} />
      <div style={{
        position: 'absolute', inset: 0, width: `${pct}%`,
        background: `linear-gradient(180deg, ${accent} 0%, ${shade(accent, -0.18)} 100%)`,
        transition: 'width 120ms ease-out',
      }} />
      {showText && (
        <div style={{
          position: 'absolute', inset: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'Geist Mono, monospace', fontSize: Math.max(10, height - 7),
          color: '#fff', textShadow: '0 1px 2px rgba(0,0,0,0.7)',
          letterSpacing: 0.3,
        }}>{Math.round(hp)} / {maxHp}</div>
      )}
    </div>
  );
}

function shade(hex, amt) {
  // tiny helper to darken/lighten a hex by amount [-1..1]; works for #rrggbb
  const c = parseInt(hex.slice(1), 16);
  let r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
  r = Math.max(0, Math.min(255, Math.round(r + r * amt)));
  g = Math.max(0, Math.min(255, Math.round(g + g * amt)));
  b = Math.max(0, Math.min(255, Math.round(b + b * amt)));
  return '#' + ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0');
}

/* ─── Skill slot with cooldown overlay + name label ─────────────── */

function FightSkillSlot({ skill, progress, accent, size = 48, showLabel = false }) {
  const ready = progress >= 1 - 0.001;
  const swept = progress * 360;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, minWidth: size }}>
      <div style={{
        width: size, height: size, position: 'relative',
        background: 'rgba(255,255,255,0.05)',
        border: `1px solid ${ready ? accent + '88' : 'rgba(255,255,255,0.14)'}`,
        borderRadius: 2,
        boxShadow: ready ? `inset 0 0 8px ${accent}55` : 'none',
        transition: 'border-color 140ms, box-shadow 140ms',
        overflow: 'hidden',
      }}>
        <div style={{
          position: 'absolute', inset: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          opacity: 0.92,
        }}>
          <FightSkillIcon kind={skill.icon} />
        </div>
        {progress < 1 && (
          <div style={{
            position: 'absolute', inset: 0,
            background: `conic-gradient(transparent ${swept}deg, rgba(0,0,0,0.65) ${swept}deg)`,
            pointerEvents: 'none',
          }} />
        )}
        {ready && (
          <div style={{
            position: 'absolute', inset: -1,
            boxShadow: `0 0 8px ${accent}aa`,
            animation: 'ready-pulse 1.2s ease-in-out infinite',
            pointerEvents: 'none',
            borderRadius: 2,
          }} />
        )}
      </div>
      {showLabel && (
        <div style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9,
          letterSpacing: 0.5, textTransform: 'uppercase',
          color: ready ? accent : 'rgba(240,240,240,0.6)',
          whiteSpace: 'nowrap',
        }}>{skill.name}</div>
      )}
      <style>{`@keyframes ready-pulse {
        0%, 100% { box-shadow: 0 0 6px ${accent}66; }
        50%      { box-shadow: 0 0 14px ${accent}cc; }
      }`}</style>
    </div>
  );
}

function FightSkillIcon({ kind }) {
  const s = { width: 22, height: 22, fill: 'none', stroke: 'rgba(240,240,240,0.92)', strokeWidth: 1.3 };
  if (kind === 'sword')  return <svg {...s} viewBox="0 0 16 16"><path d="M11 1.5l3.5 3.5-7 7-3.5-3.5zM3 11l2 2M2 13l1 1" strokeLinecap="round" strokeLinejoin="round" /></svg>;
  if (kind === 'shield') return <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5l5 1.5v5c0 3-2 5-5 7-3-2-5-4-5-7v-5z" strokeLinejoin="round" /></svg>;
  if (kind === 'leaf')   return <svg {...s} viewBox="0 0 16 16"><path d="M3 13c0-6 4-10 10-10 0 6-4 10-10 10zM3 13l4-4" strokeLinecap="round" strokeLinejoin="round" /></svg>;
  if (kind === 'bolt')   return <svg {...s} viewBox="0 0 16 16"><path d="M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z" strokeLinejoin="round" /></svg>;
  if (kind === 'rune')   return <svg {...s} viewBox="0 0 16 16"><path d="M8 2v12M4 5l8 6M12 5l-8 6" strokeLinecap="round" /></svg>;
  return null;
}

Object.assign(window, {
  PLAYER_BASE, ENEMY_BASE, PLAYER_SKILLS, ENEMY_SKILLS,
  useFightSim, skillProgress,
  FightZoneNav, DamageBubble, HpBar, FightSkillSlot, FightSkillIcon, shade,
});
