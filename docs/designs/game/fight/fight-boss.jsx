/* Boss mechanism for the Fight screen.

   Replaces the old "bosses appear at random mid-fight" behaviour with a
   deliberate, one-boss-per-zone system:

     - Each zone has exactly one boss. Zone 3 (Forgotten Catacombs) → the
       Catacomb Lich (LV 18, 1200 HP) — consistent with the admin enemy table.
     - The boss is ALWAYS available to challenge (no gating).
     - A dedicated affordance (the BossTrigger) sits between the zone-nav header
       and the combat body. The player clicks "Challenge" to engage.
     - Auto-fight: a toggle that re-engages the boss continuously on victory.
     - On victory the zone is marked CLEARED, the next zone unlocks, and the boss
       is permanently flagged defeated — but stays re-challengeable (farmable).

   useFightSession(cfg) drives a live battle, boss-aware. cfg seeds the scenario:
     cfg.bossPhase : 'available' | 'engaged' | 'defeated'
     cfg.bossTrigger: 'banner' | 'inline' | 'plinth'   (presentation only)
     cfg.autoFight : boolean
     cfg.accent    : boss accent hex
     cfg.heightened: boolean — boss-fight visual intensity on/off
*/

const BOSS_ACCENT = '#e8c878';

const FB_ZONE = { num: 3, name: 'Forgotten Catacombs' };

const FB_PLAYER = { name: 'Aelara', lvl: 12, maxHp: 300 };

const FB_NORMAL_ENEMIES = [
  { name: 'Skeleton Mage', lvl: 11, maxHp: 220 },
  { name: 'Crypt Hound',   lvl: 9,  maxHp: 160 },
  { name: 'Plague Rat',    lvl: 8,  maxHp: 120 },
  { name: 'Bone Archer',   lvl: 10, maxHp: 180 },
];
const FB_NORMAL_SKILLS = [
  { name: 'Hex',   icon: 'rune', cdMs: 2600 },
  { name: 'Drain', icon: 'rune', cdMs: 1900 },
];

const ZONE_BOSS = {
  name: 'Catacomb Lich',
  lvl: 18,
  maxHp: 1200,
  title: 'Keeper of the Hollow Vault',
  blurb: 'Bound this catacomb in endless night. Defeat it to clear the zone.',
  skills: [
    { name: 'Soul Rend',  icon: 'rune',   cdMs: 2400 },
    { name: 'Bone Storm', icon: 'bolt',   cdMs: 3200 },
    { name: 'Decay',      icon: 'rune',   cdMs: 1700 },
    { name: 'Raise Dead', icon: 'shield', cdMs: 4200 },
  ],
};

function fbBossBattler() {
  return { ...ZONE_BOSS, skills: ZONE_BOSS.skills, isBoss: true };
}
function fbNormalAt(i) {
  const e = FB_NORMAL_ENEMIES[i % FB_NORMAL_ENEMIES.length];
  return { ...e, skills: FB_NORMAL_SKILLS, isBoss: false };
}

/* ─── session hook ──────────────────────────────────────────────────────── */

function useFightSession(cfg = {}) {
  const accent = cfg.accent || BOSS_ACCENT;
  const heightened = cfg.heightened !== false;
  const phase = cfg.bossPhase || 'available';
  const triggerStyle = cfg.bossTrigger || 'banner';

  const [, force] = React.useReducer((x) => x + 1, 0);
  const ref = React.useRef(null);

  const seed = React.useCallback((p) => {
    const mode = p === 'engaged' ? 'boss' : 'normal';
    const defeated = p === 'defeated';
    const enemy = mode === 'boss' ? fbBossBattler() : fbNormalAt(0);
    const now = performance.now();
    return {
      mode, defeated,
      autoFight: !!cfg.autoFight,
      enemyIdx: 0,
      enemy,
      hp:   { player: FB_PLAYER.maxHp * 0.82, enemy: enemy.maxHp * (mode === 'boss' ? 1 : 0.72) },
      disp: { player: FB_PLAYER.maxHp * 0.82, enemy: enemy.maxHp * (mode === 'boss' ? 1 : 0.72) },
      damages: [],
      seq: 0,
      lastSpawn: now + 500,
      outcome: null,         // null | 'victory'
      outcomeAt: 0,
      start: now,
    };
  }, [cfg.autoFight]);

  if (!ref.current) ref.current = seed(phase);

  // Re-seed only when the scenario phase changes.
  React.useEffect(() => {
    ref.current = seed(phase);
    force();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [phase]);

  // Sync the auto-fight tweak without resetting an in-progress fight.
  React.useEffect(() => {
    if (ref.current) ref.current.autoFight = !!cfg.autoFight;
    force();
  }, [cfg.autoFight]);

  React.useEffect(() => {
    let raf;
    const loop = () => {
      const now = performance.now();
      const s = ref.current;

      // smooth disappearing-hp lerp (faster for the bigger boss bar)
      ['player', 'enemy'].forEach((k) => {
        const rate = s.mode === 'boss' && k === 'enemy' ? 2.6 : 0.8;
        if (s.disp[k] > s.hp[k]) s.disp[k] = Math.max(s.hp[k], s.disp[k] - rate);
      });

      if (s.outcome === 'victory') {
        if (now - s.outcomeAt > 2600) {
          if (s.autoFight) {
            // re-engage the boss immediately (farming)
            s.mode = 'boss';
            s.enemy = fbBossBattler();
            s.hp.enemy = s.enemy.maxHp; s.disp.enemy = s.enemy.maxHp;
            s.hp.player = FB_PLAYER.maxHp; s.disp.player = FB_PLAYER.maxHp;
            s.outcome = null; s.start = now;
          } else {
            // return to the normal field
            s.mode = 'normal';
            s.enemyIdx = 0;
            s.enemy = fbNormalAt(0);
            s.hp.enemy = s.enemy.maxHp * 0.72; s.disp.enemy = s.hp.enemy;
            s.hp.player = FB_PLAYER.maxHp; s.disp.player = FB_PLAYER.maxHp;
            s.outcome = null;
          }
        }
      } else {
        const wait = 740 + (s.seq % 5) * 110;
        if (now - s.lastSpawn > wait) {
          // boss fights tilt toward the boss taking damage so the clear is
          // reachable; the boss still chips the player for tension.
          const bias = s.mode === 'boss' ? 0.66 : 0.5;
          const target = Math.random() < bias ? 'enemy' : 'player';
          const isCrit = Math.random() > 0.82;
          const band = s.mode === 'boss'
            ? (target === 'enemy' ? [26, 42] : [9, 19])
            : (target === 'enemy' ? [16, 30] : [12, 22]);
          let value = Math.floor(band[0] + Math.random() * (band[1] - band[0]));
          if (isCrit) value = Math.floor(value * 1.8);
          s.hp[target] = Math.max(0, s.hp[target] - value);
          s.damages.push({
            id: ++s.seq, side: target, value, isCrit,
            jitter: (Math.random() - 0.5) * 32, spawn: now,
          });

          if (s.hp.enemy === 0) {
            if (s.mode === 'boss') {
              s.outcome = 'victory';
              s.outcomeAt = now;
              s.defeated = true;
            } else {
              setTimeout(() => {
                const s2 = ref.current;
                s2.enemyIdx += 1;
                s2.enemy = fbNormalAt(s2.enemyIdx);
                s2.hp.enemy = s2.enemy.maxHp; s2.disp.enemy = s2.enemy.maxHp;
              }, 450);
            }
          }
          if (s.hp.player === 0) {
            // rally — refill and keep going (keeps the demo flowing)
            setTimeout(() => {
              const s2 = ref.current;
              s2.hp.player = FB_PLAYER.maxHp; s2.disp.player = FB_PLAYER.maxHp;
            }, 450);
          }
          s.lastSpawn = now;
        }
      }

      s.damages = s.damages.filter((d) => now - d.spawn < 1500);
      force();
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, []);

  const s = ref.current;

  const engageBoss = React.useCallback(() => {
    const st = ref.current; const now = performance.now();
    st.mode = 'boss'; st.enemy = fbBossBattler();
    st.hp.enemy = st.enemy.maxHp; st.disp.enemy = st.enemy.maxHp;
    st.hp.player = FB_PLAYER.maxHp; st.disp.player = FB_PLAYER.maxHp;
    st.outcome = null; st.start = now; force();
  }, []);
  const retreat = React.useCallback(() => {
    const st = ref.current;
    st.mode = 'normal'; st.enemyIdx = 0; st.enemy = fbNormalAt(0);
    st.hp.enemy = st.enemy.maxHp * 0.72; st.disp.enemy = st.hp.enemy;
    st.outcome = null; force();
  }, []);
  const setAutoFight = React.useCallback((v) => {
    ref.current.autoFight = !!v; force();
  }, []);

  return {
    mode: s.mode, defeated: s.defeated, autoFight: s.autoFight, outcome: s.outcome,
    player: { ...FB_PLAYER, hp: s.hp.player, dispHp: s.disp.player, skills: window.PLAYER_SKILLS },
    enemy:  { ...s.enemy, hp: s.hp.enemy, dispHp: s.disp.enemy },
    damages: s.damages,
    elapsed: performance.now() - s.start,
    boss: ZONE_BOSS, zone: FB_ZONE, nextZone: FB_ZONE.num + 1,
    accent, heightened, triggerStyle,
    engageBoss, retreat, setAutoFight,
  };
}

/* ─── small primitives ──────────────────────────────────────────────────── */

function BossDiamond({ size = 8, accent = BOSS_ACCENT, glow = true }) {
  return (
    <span style={{
      display: 'inline-block', width: size, height: size,
      transform: 'rotate(45deg)', background: accent,
      boxShadow: glow ? `0 0 8px ${accent}cc` : 'none', flexShrink: 0,
    }} />
  );
}

function BossKicker({ accent = BOSS_ACCENT, children, size = 9.5 }) {
  return (
    <span style={{
      fontFamily: 'Geist Mono, monospace', fontSize: size,
      letterSpacing: 1.8, textTransform: 'uppercase', color: accent,
      whiteSpace: 'nowrap',
    }}>{children}</span>
  );
}

/* striped placeholder for the boss portrait (no hand-drawn art) */
function BossPortrait({ accent = BOSS_ACCENT, w = 56, h = 56, label = 'boss art' }) {
  return (
    <div style={{
      width: w, height: h, flexShrink: 0, position: 'relative',
      border: `1px solid ${accent}55`, borderRadius: 2, overflow: 'hidden',
      background: `repeating-linear-gradient(135deg, ${accent}14 0 6px, transparent 6px 12px), rgba(0,0,0,0.35)`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 7.5, letterSpacing: 0.6,
        color: `${accent}aa`, textTransform: 'uppercase', textAlign: 'center', lineHeight: 1.2,
      }}>{label}</span>
    </div>
  );
}

function ChallengeButton({ accent = BOSS_ACCENT, onClick, children, big = false }) {
  const [hov, setHov] = React.useState(false);
  return (
    <button onClick={onClick}
      onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 8,
        fontFamily: 'Geist Mono, monospace',
        fontSize: big ? 12 : 10.5, letterSpacing: 1.4, textTransform: 'uppercase',
        color: hov ? '#14151b' : accent,
        background: hov ? accent : `${accent}1a`,
        border: `1px solid ${accent}`,
        borderRadius: 2, cursor: 'pointer',
        padding: big ? '9px 18px' : '6px 12px',
        boxShadow: hov ? `0 0 18px ${accent}66` : `0 0 0 ${accent}00`,
        transition: 'all 150ms', fontWeight: 600, whiteSpace: 'nowrap',
      }}>
      <BossDiamond size={6} accent={hov ? '#14151b' : accent} glow={!hov} />
      {children}
    </button>
  );
}

const RETREAT_RED = '#dc6a62';

// Mirrors ChallengeButton's fill-on-hover, in red, without the diamond.
function RetreatButton({ onClick, color = RETREAT_RED }) {
  const [hov, setHov] = React.useState(false);
  return (
    <button onClick={onClick}
      onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      title="Retreat to the normal field"
      style={{
        fontFamily: 'Geist Mono, monospace',
        fontSize: 10.5, letterSpacing: 1.4, textTransform: 'uppercase', fontWeight: 600,
        color: hov ? '#14151b' : color,
        background: hov ? color : `${color}1a`,
        border: `1px solid ${color}`,
        borderRadius: 2, cursor: 'pointer', padding: '6px 12px',
        boxShadow: hov ? `0 0 18px ${color}66` : `0 0 0 ${color}00`,
        transition: 'all 150ms', whiteSpace: 'nowrap',
      }}>Retreat</button>
  );
}

function AutoFightToggle({ on, onChange, accent = BOSS_ACCENT, compact = false }) {
  return (
    <button onClick={() => onChange(!on)}
      title="Keep re-engaging the boss after each victory"
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 8,
        background: on ? `${accent}1a` : 'rgba(255,255,255,0.03)',
        border: `1px solid ${on ? accent + '88' : 'rgba(255,255,255,0.16)'}`,
        borderRadius: 2, cursor: 'pointer', padding: compact ? '5px 9px' : '6px 11px',
        transition: 'all 150ms',
      }}>
      <span style={{
        position: 'relative', width: 26, height: 14, borderRadius: 999,
        background: on ? accent : 'rgba(255,255,255,0.18)', transition: 'background 150ms', flexShrink: 0,
      }}>
        <span style={{
          position: 'absolute', top: 2, left: on ? 14 : 2, width: 10, height: 10,
          borderRadius: '50%', background: on ? '#14151b' : '#f0f0f0',
          transition: 'left 150ms',
        }} />
      </span>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 1.3,
        textTransform: 'uppercase', color: on ? accent : 'rgba(240,240,240,0.6)', whiteSpace: 'nowrap',
      }}>Auto-fight</span>
    </button>
  );
}

function ClearedSeal({ accent = BOSS_ACCENT, compact = false }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      border: `1px solid ${accent}77`, borderRadius: 2,
      padding: compact ? '3px 7px' : '4px 9px', background: `${accent}12`,
    }}>
      <svg width={compact ? 9 : 11} height={compact ? 9 : 11} viewBox="0 0 14 14" fill="none">
        <path d="M3 7.4 5.8 10 11 4.2" stroke={accent} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: compact ? 8.5 : 9.5,
        letterSpacing: 1.5, textTransform: 'uppercase', color: accent,
      }}>Cleared</span>
    </span>
  );
}

/* ─── boss HP bar with phase pips ───────────────────────────────────────── */

function BossHpBar({ hp, dispHp, maxHp, accent = BOSS_ACCENT, height = 22 }) {
  return (
    <div style={{ position: 'relative' }}>
      <HpBar hp={hp} dispHp={dispHp} maxHp={maxHp} accent="#7fc28b" height={height} showText={false} />
      {/* phase ticks */}
      {[25, 50, 75].map((p) => (
        <div key={p} style={{
          position: 'absolute', top: 0, bottom: 0, left: `${p}%`, width: 1,
          background: 'rgba(0,0,0,0.45)', boxShadow: `0 0 0 0.5px ${accent}33`,
        }} />
      ))}
      <div style={{
        position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontFamily: 'Geist Mono, monospace', fontSize: Math.max(11, height - 9),
        color: '#fff', textShadow: '0 1px 3px rgba(0,0,0,0.85)', letterSpacing: 0.4, fontWeight: 500,
      }}>{Math.round(hp)} / {maxHp}</div>
    </div>
  );
}

/* ─── the trigger affordance (3 presentation styles) ────────────────────── */

function BossTrigger({ session, style }) {
  const st = style || session.triggerStyle || 'banner';
  if (st === 'inline')  return <BossTriggerInline session={session} />;
  if (st === 'plinth')  return <BossTriggerPlinth session={session} />;
  return <BossTriggerBanner session={session} />;
}

// A · boxed + centered (banner) but compact, single-row, no portrait (hybrid)
function BossTriggerBanner({ session }) {
  const { accent, boss, defeated } = session;
  return (
    <div style={{ display: 'flex', justifyContent: 'center', width: '100%' }}>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 14,
        background: `linear-gradient(90deg, ${accent}16, rgba(255,255,255,0.02) 72%)`,
        border: `1px solid ${accent}44`, borderLeft: `3px solid ${accent}`,
        borderRadius: 3, padding: '9px 12px 9px 14px',
      }}>
        <BossDiamond size={8} accent={accent} />
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
          <BossKicker accent={accent}>Zone Boss</BossKicker>
          <span style={{ fontSize: 15.5, fontWeight: 500, color: '#f0f0f0', letterSpacing: -0.1 }}>{boss.name}</span>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: 'rgba(240,240,240,0.55)', letterSpacing: 0.5 }}>LV · {boss.lvl}</span>
        </div>
        {defeated && <ClearedSeal accent={accent} compact />}
        <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.1)', margin: '0 2px' }} />
        <AutoFightToggle on={session.autoFight} onChange={session.setAutoFight} accent={accent} compact />
        <ChallengeButton accent={accent} onClick={session.engageBoss}>
          {defeated ? 'Re-challenge' : 'Challenge'}
        </ChallengeButton>
      </div>
    </div>
  );
}

// B · compact inline chip (slim, left-aligned)
function BossTriggerInline({ session }) {
  const { accent, boss, defeated } = session;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 9,
        background: 'rgba(255,255,255,0.03)', border: `1px solid ${accent}55`,
        borderRadius: 2, padding: '6px 11px',
      }}>
        <BossDiamond size={7} accent={accent} />
        <BossKicker accent={accent} size={9}>Boss</BossKicker>
        <span style={{ fontSize: 13.5, color: '#f0f0f0', fontWeight: 500 }}>{boss.name}</span>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: 'rgba(240,240,240,0.5)' }}>LV {boss.lvl}</span>
        {defeated && <ClearedSeal accent={accent} compact />}
      </div>
      <AutoFightToggle on={session.autoFight} onChange={session.setAutoFight} accent={accent} compact />
      <ChallengeButton accent={accent} onClick={session.engageBoss}>
        {defeated ? 'Re-challenge' : 'Challenge'}
      </ChallengeButton>
    </div>
  );
}

// C · centered pedestal / plinth — the most dramatic, "summon" feel
function BossTriggerPlinth({ session }) {
  const { accent, boss, defeated } = session;
  return (
    <div style={{
      display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12,
      padding: '6px 0 2px',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 16,
        background: 'rgba(255,255,255,0.025)',
        border: `1px solid ${accent}44`, borderTop: `2px solid ${accent}`,
        borderRadius: 3, padding: '14px 22px',
        boxShadow: `0 6px 26px ${accent}1f`,
      }}>
        <BossPortrait accent={accent} w={58} h={58} />
        <div style={{ textAlign: 'left' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginBottom: 4 }}>
            <BossKicker accent={accent}>Zone Boss</BossKicker>
            {defeated && <ClearedSeal accent={accent} compact />}
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, marginBottom: 3 }}>
            <span style={{ fontSize: 21, fontWeight: 500, color: '#f0f0f0', letterSpacing: -0.2 }}>{boss.name}</span>
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: 'rgba(240,240,240,0.55)' }}>LV · {boss.lvl}</span>
          </div>
          <div style={{ fontSize: 12, color: 'rgba(240,240,240,0.5)', maxWidth: 320 }}>{boss.title}</div>
        </div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <ChallengeButton accent={accent} onClick={session.engageBoss} big>
          {defeated ? 'Re-challenge boss' : 'Challenge boss'}
        </ChallengeButton>
        <AutoFightToggle on={session.autoFight} onChange={session.setAutoFight} accent={accent} />
      </div>
    </div>
  );
}

/* ─── the in-fight boss bar (replaces trigger while engaged) ─────────────── */

function BossBar({ session }) {
  const { accent, boss, enemy } = session;
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14,
      background: `linear-gradient(90deg, ${accent}1c, rgba(255,255,255,0.02) 55%)`,
      border: `1px solid ${accent}55`, borderLeft: `3px solid ${accent}`,
      borderRadius: 3, padding: '9px 13px',
    }}>
      <BossDiamond size={9} accent={accent} />
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, flex: 1, minWidth: 0 }}>
        <BossKicker accent={accent}>Boss Fight</BossKicker>
        <span style={{ fontSize: 15, fontWeight: 500, color: '#f0f0f0' }}>{boss.name}</span>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: 'rgba(240,240,240,0.5)', textTransform: 'uppercase', letterSpacing: 1 }}>{boss.title}</span>
      </div>
      <AutoFightToggle on={session.autoFight} onChange={session.setAutoFight} accent={accent} compact />
      <RetreatButton onClick={session.retreat} />
    </div>
  );
}

/* ─── the affordance slot — trigger OR boss bar ─────────────────────────── */

function BossAffordanceSlot({ session }) {
  return session.mode === 'boss'
    ? <BossBar session={session} />
    : <BossTrigger session={session} />;
}

/* ─── victory / zone-cleared overlay ────────────────────────────────────── */

function VictoryOverlay({ session }) {
  if (session.outcome !== 'victory') return null;
  const { accent, boss, zone, nextZone, autoFight } = session;
  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 20,
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      gap: 10, pointerEvents: 'none',
      background: 'radial-gradient(ellipse at center, rgba(20,21,27,0.55), rgba(10,10,12,0.86))',
      animation: 'fb-victory-in 360ms ease-out',
    }}>
      <BossKicker accent={accent} size={11}>Zone Cleared</BossKicker>
      <div style={{
        fontSize: 40, fontWeight: 600, color: '#f0f0f0', letterSpacing: -0.6,
        textShadow: `0 0 40px ${accent}66`, textAlign: 'center', lineHeight: 1.05,
      }}>{boss.name} defeated</div>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12, marginTop: 6,
        fontFamily: 'Geist Mono, monospace', fontSize: 12, letterSpacing: 0.8,
        color: 'rgba(240,240,240,0.78)',
      }}>
        <ClearedSeal accent={accent} />
        <span>Zone {String(zone.num).padStart(2, '0')} complete</span>
        <BossDiamond size={5} accent={accent} />
        <span style={{ color: accent }}>Zone {String(nextZone).padStart(2, '0')} unlocked</span>
      </div>
      {autoFight && (
        <div style={{
          marginTop: 4, fontFamily: 'Geist Mono, monospace', fontSize: 10,
          letterSpacing: 1.4, textTransform: 'uppercase', color: 'rgba(240,240,240,0.5)',
        }}>Auto-fight · re-engaging…</div>
      )}
      <style>{`@keyframes fb-victory-in { from { opacity: 0; } to { opacity: 1; } }`}</style>
    </div>
  );
}

/* ─── boss atmosphere (heightened-mode vignette + glow) ─────────────────── */

function BossAtmosphere({ accent = BOSS_ACCENT }) {
  return (
    <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none', zIndex: 0, overflow: 'hidden' }}>
      <div style={{
        position: 'absolute', inset: 0,
        background: `radial-gradient(120% 90% at 50% -10%, ${accent}1f, transparent 55%)`,
        animation: 'fb-atmo 4.5s ease-in-out infinite',
      }} />
      <div style={{
        position: 'absolute', inset: 0,
        boxShadow: `inset 0 0 160px rgba(0,0,0,0.7), inset 0 0 60px ${accent}10`,
      }} />
      <style>{`@keyframes fb-atmo { 0%,100% { opacity: 0.7; } 50% { opacity: 1; } }`}</style>
    </div>
  );
}

Object.assign(window, {
  BOSS_ACCENT, ZONE_BOSS, useFightSession,
  BossDiamond, BossKicker, BossPortrait, ChallengeButton, AutoFightToggle,
  ClearedSeal, BossHpBar, BossTrigger, BossBar, BossAffordanceSlot,
  VictoryOverlay, BossAtmosphere, RetreatButton,
});
