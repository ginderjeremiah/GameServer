/* Fight Variant A · Mirror Cards
   Player on the left, enemy on the right, facing each other across a center
   versus indicator. Player accent blue, enemy accent red.

   Boss-aware: when the session is in boss mode the enemy side becomes a
   heightened gold "boss card" and the atmosphere intensifies. The boss
   affordance (challenge / boss bar) sits between the zone nav and the arena. */

function FightMirror({ cfg }) {
  const S = useFightSession(cfg);
  const boss = S.mode === 'boss';
  return (
    <div style={fightBg()}>
      {boss && S.heightened && <BossAtmosphere accent={S.accent} />}

      {/* zone nav header */}
      <div style={{ position: 'relative', zIndex: 1, padding: '18px 32px 0', display: 'flex', justifyContent: 'center' }}>
        <FightZoneNav />
      </div>

      {/* boss affordance slot */}
      <div style={{ position: 'relative', zIndex: 2, padding: '12px 32px 0', display: 'flex', justifyContent: 'center' }}>
        <div style={{ width: '100%', maxWidth: 720 }}>
          <BossAffordanceSlot session={S} />
        </div>
      </div>

      {/* combatants */}
      <div style={{
        flex: 1, position: 'relative', zIndex: 1,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        gap: boss ? 30 : 40, padding: '16px 36px 36px', minHeight: 0,
      }}>
        <MirrorCard battler={S.player} accent="#a1c2f7" skills={S.player.skills} elapsed={S.elapsed} align="left" />

        {boss ? <BossVersus accent={S.accent} /> : <VersusBadge />}

        {boss
          ? <BossMirrorCard session={S} />
          : <MirrorCard battler={S.enemy} accent="#e08778" skills={S.enemy.skills} elapsed={S.elapsed} align="right" />}

        {S.damages.map((d) => (
          <DamageBubble key={d.id} value={d.value} isCrit={d.isCrit} side={d.side}
            jitter={d.jitter} x={d.side === 'player' ? '22%' : '78%'} y="46%" />
        ))}

        <VictoryOverlay session={S} />
      </div>
    </div>
  );
}

function MirrorCard({ battler, accent, skills, elapsed, align }) {
  const isLeft = align === 'left';
  return (
    <div style={{
      background: 'rgba(255,255,255,0.03)',
      border: '1px solid rgba(255,255,255,0.14)',
      borderLeft:  isLeft ? `3px solid ${accent}` : '1px solid rgba(255,255,255,0.14)',
      borderRight: !isLeft ? `3px solid ${accent}` : '1px solid rgba(255,255,255,0.14)',
      borderRadius: 3, padding: '18px 20px', color: '#f0f0f0',
      width: 340, flexShrink: 0,
      boxShadow: `0 0 0 1px rgba(0,0,0,0.4), ${isLeft ? -4 : 4}px 0 18px ${accent}1a`,
    }}>
      <div style={{
        display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
        marginBottom: 14, flexDirection: isLeft ? 'row' : 'row-reverse',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9, flexDirection: isLeft ? 'row' : 'row-reverse' }}>
          <div style={{ width: 4, height: 18, background: accent, boxShadow: `0 0 8px ${accent}80` }} />
          <span style={{ fontSize: 18, fontWeight: 500, letterSpacing: -0.1 }}>{battler.name}</span>
        </div>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10.5, color: 'rgba(240,240,240,0.6)', letterSpacing: 0.6 }}>LV · {battler.lvl}</span>
      </div>

      <HpBar hp={battler.hp} dispHp={battler.dispHp} maxHp={battler.maxHp} accent="#7fc28b" height={20} />

      <div style={{ marginTop: 16, display: 'flex', gap: 10, justifyContent: isLeft ? 'flex-start' : 'flex-end', flexWrap: 'wrap' }}>
        {skills.map((sk, i) => (
          <FightSkillSlot key={sk.name} skill={sk} progress={skillProgress(elapsed, sk.cdMs, i * 250)} accent={accent} size={46} showLabel />
        ))}
      </div>
    </div>
  );
}

/* heightened gold boss card on the enemy side */
function BossMirrorCard({ session }) {
  const { enemy, elapsed, accent } = session;
  return (
    <div style={{
      position: 'relative', width: 392, flexShrink: 0, color: '#f0f0f0',
      background: `linear-gradient(180deg, ${accent}10, rgba(255,255,255,0.02) 40%)`,
      border: `1px solid ${accent}66`, borderRight: `3px solid ${accent}`,
      borderRadius: 3, padding: '16px 20px 18px',
      boxShadow: `0 0 0 1px rgba(0,0,0,0.45), 0 0 34px ${accent}26`,
    }}>
      {/* boss ribbon */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 9, marginBottom: 12 }}>
        <BossKicker accent={accent}>Zone Boss</BossKicker>
        <BossDiamond size={8} accent={accent} />
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 14, flexDirection: 'row-reverse', marginBottom: 14 }}>
        <BossPortrait accent={accent} w={56} h={56} />
        <div style={{ textAlign: 'right', flex: 1 }}>
          <div style={{ fontSize: 21, fontWeight: 600, letterSpacing: -0.2 }}>{enemy.name}</div>
          <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: 'rgba(240,240,240,0.55)', letterSpacing: 0.6 }}>LV · {enemy.lvl}</div>
        </div>
      </div>

      <BossHpBar hp={enemy.hp} dispHp={enemy.dispHp} maxHp={enemy.maxHp} accent={accent} height={22} />

      <div style={{ marginTop: 16, display: 'flex', gap: 9, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
        {enemy.skills.map((sk, i) => (
          <FightSkillSlot key={sk.name} skill={sk} progress={skillProgress(elapsed, sk.cdMs, i * 190)} accent={accent} size={42} showLabel />
        ))}
      </div>
    </div>
  );
}

function BossVersus({ accent }) {
  return (
    <div style={{ width: 56, height: 56, position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <div style={{ position: 'absolute', inset: 0, border: `1px solid ${accent}55`, transform: 'rotate(45deg)', background: 'rgba(20,21,27,0.6)' }} />
      <div style={{ position: 'absolute', inset: 12, border: `1px solid ${accent}88`, transform: 'rotate(45deg)', boxShadow: `inset 0 0 12px ${accent}33` }} />
      <span style={{ position: 'relative', fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 1.6, textTransform: 'uppercase', color: accent }}>vs</span>
    </div>
  );
}

function VersusBadge() {
  return (
    <div style={{ width: 56, height: 56, position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <div style={{ position: 'absolute', inset: 0, border: '1px solid rgba(255,255,255,0.18)', transform: 'rotate(45deg)', background: 'rgba(20,21,27,0.6)' }} />
      <div style={{ position: 'absolute', inset: 14, background: 'rgba(255,255,255,0.04)', transform: 'rotate(45deg)', boxShadow: 'inset 0 0 12px rgba(255,255,255,0.08)' }} />
      <span style={{ position: 'relative', fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 1.6, textTransform: 'uppercase', color: 'rgba(240,240,240,0.85)' }}>vs</span>
    </div>
  );
}

function fightBg() {
  return {
    width: '100%', height: '100%',
    background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
    color: '#f0f0f0', fontFamily: 'Geist, Arial, Helvetica, sans-serif',
    display: 'flex', flexDirection: 'column', overflow: 'hidden',
    position: 'relative',
  };
}

window.FightMirror = FightMirror;
window.fightBg = fightBg;
