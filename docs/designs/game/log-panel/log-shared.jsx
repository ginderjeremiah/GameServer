/* Shared log model — generates an evolving stream of combat events.

   Entry shapes:
     hit    — { kind:'hit',    actor, skill, target, value, isCrit:false }
     crit   — { kind:'crit',   actor, skill, target, value, isCrit:true }
     enemy  — { kind:'enemy',  actor, skill, target, value }   // damage to player
     loot   — { kind:'loot',   item, count }
     system — { kind:'system', text }
     kill   — { kind:'kill',   target }
*/

const PLAYER_NAME = 'Aelara';
const ENEMY_NAME = 'Skeleton Mage';
const PLAYER_SKILL_NAMES = ['Cleave', 'Surge', 'Mend', 'Guard'];
const ENEMY_SKILL_NAMES = ['Hex', 'Drain'];
const LOOT_ITEMS = ['Sapphire Shard', 'Bone Dust', 'Tarnished Coin', 'Iron Ore', 'Faded Map'];
const SYSTEM_MESSAGES = [
  'Surge is ready.',
  'Mend is ready.',
  'Cleave is ready.',
  'Aelara entered Forgotten Catacombs.',
];

function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
function rand(lo, hi) { return Math.floor(lo + Math.random() * (hi - lo)); }
function fmtTime(sec) {
  const m = Math.floor(sec / 60).toString().padStart(2, '0');
  const s = Math.floor(sec % 60).toString().padStart(2, '0');
  return `${m}:${s}`;
}

function genEntry(seq, totalSec) {
  const r = Math.random();
  const ts = fmtTime(totalSec);
  if (r < 0.40) {
    return {
      id: seq, time: ts, kind: 'hit',
      actor: PLAYER_NAME, skill: pick(PLAYER_SKILL_NAMES), target: ENEMY_NAME,
      value: rand(20, 55), isCrit: false,
    };
  }
  if (r < 0.50) {
    return {
      id: seq, time: ts, kind: 'crit',
      actor: PLAYER_NAME, skill: pick(PLAYER_SKILL_NAMES), target: ENEMY_NAME,
      value: rand(60, 98), isCrit: true,
    };
  }
  if (r < 0.78) {
    return {
      id: seq, time: ts, kind: 'enemy',
      actor: ENEMY_NAME, skill: pick(ENEMY_SKILL_NAMES), target: PLAYER_NAME,
      value: rand(8, 26),
    };
  }
  if (r < 0.92) {
    return {
      id: seq, time: ts, kind: 'loot',
      item: pick(LOOT_ITEMS), count: rand(1, 3),
    };
  }
  return { id: seq, time: ts, kind: 'system', text: pick(SYSTEM_MESSAGES) };
}

function useLogStream({ maxEntries = 24, intervalMs = 1100, initialBacklog = 6 } = {}) {
  const [entries, setEntries] = React.useState(() => {
    // seed with backlog so first render isn't empty
    const seeded = [];
    let seq = 0;
    let t = -intervalMs / 1000 * initialBacklog;
    for (let i = 0; i < initialBacklog; i++) {
      t += intervalMs / 1000 + (Math.random() * 0.4 - 0.2);
      seeded.push({ ...genEntry(++seq, Math.max(0, Math.floor(t))), spawn: performance.now() - (initialBacklog - i) * intervalMs });
    }
    return seeded;
  });
  const seqRef = React.useRef(initialBacklog);
  const timeRef = React.useRef(initialBacklog * (intervalMs / 1000));

  React.useEffect(() => {
    const id = setInterval(() => {
      seqRef.current += 1;
      timeRef.current += intervalMs / 1000 + (Math.random() * 0.4 - 0.2);
      const entry = { ...genEntry(seqRef.current, Math.max(0, Math.floor(timeRef.current))), spawn: performance.now() };
      setEntries((curr) => [...curr.slice(-(maxEntries - 1)), entry]);
    }, intervalMs);
    return () => clearInterval(id);
  }, [intervalMs, maxEntries]);

  return entries;
}

/* ─── presentation helpers ──────────────────────────────────────── */

const KIND_COLOR = {
  hit:    '#c0d8ff',
  crit:   '#f0d28a',
  enemy:  '#e8b6a6',
  loot:   '#bde0b4',
  system: 'rgba(240,240,240,0.7)',
  kill:   '#bde0b4',
};
const KIND_DIM = {
  hit:    'rgba(192,216,255,0.45)',
  crit:   'rgba(240,210,138,0.5)',
  enemy:  'rgba(232,182,166,0.4)',
  loot:   'rgba(189,224,180,0.42)',
  system: 'rgba(240,240,240,0.35)',
  kill:   'rgba(189,224,180,0.42)',
};
const KIND_LABEL = {
  hit:    'Hit',
  crit:   'Crit',
  enemy:  'Hurt',
  loot:   'Loot',
  system: 'Info',
  kill:   'Kill',
};

function plainText(e) {
  if (e.kind === 'hit' || e.kind === 'crit') {
    const c = e.isCrit ? 'crit ' : '';
    return `${e.actor} cast ${e.skill} on ${e.target} — ${c}${e.value} damage.`;
  }
  if (e.kind === 'enemy') {
    return `${e.actor} cast ${e.skill} on ${e.target} — ${e.value} damage.`;
  }
  if (e.kind === 'loot') {
    return `Picked up ${e.item} ×${e.count}.`;
  }
  if (e.kind === 'kill') {
    return `${e.target} defeated.`;
  }
  return e.text;
}

function KindGlyph({ kind, size = 12, color }) {
  const c = color ?? KIND_COLOR[kind];
  const s = { width: size, height: size, fill: 'none', stroke: c, strokeWidth: 1.4, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (kind === 'hit')    return <svg {...s} viewBox="0 0 14 14"><path d="M10 1.5l2.5 2.5-7 7-2.5-2.5z M3 11l1.5 1.5" /></svg>;
  if (kind === 'crit')   return <svg {...s} viewBox="0 0 14 14"><path d="M7 1.5l1.6 3.3 3.6.5-2.6 2.6.6 3.6L7 9.7 3.8 11.5l.6-3.6-2.6-2.6 3.6-.5z" /></svg>;
  if (kind === 'enemy')  return <svg {...s} viewBox="0 0 14 14"><path d="M3 11L11 3M11 11L3 3" /></svg>;
  if (kind === 'loot')   return <svg {...s} viewBox="0 0 14 14"><path d="M2.5 4.5h9v8h-9z M5 4.5V3a2 2 0 014 0v1.5" /></svg>;
  if (kind === 'system') return <svg {...s} viewBox="0 0 14 14"><circle cx="7" cy="7" r="4.5" /><path d="M7 5v3M7 9.5v0.01" /></svg>;
  if (kind === 'kill')   return <svg {...s} viewBox="0 0 14 14"><path d="M7 2v10M2 7h10" /></svg>;
  return null;
}

/* shared "log frame" wrapper for the variant artboards */
function LogStage({ children, height = 220, label }) {
  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #2c2c2c 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0',
      padding: 20, boxSizing: 'border-box',
      display: 'flex', flexDirection: 'column',
    }}>
      <div style={{
        flex: 1, minHeight: 0,
        display: 'flex', alignItems: 'flex-end',
      }}>
        <div style={{ width: '100%', height, position: 'relative' }}>
          {children}
        </div>
      </div>
    </div>
  );
}

Object.assign(window, {
  useLogStream, plainText, KindGlyph,
  KIND_COLOR, KIND_DIM, KIND_LABEL,
  LogStage,
  PLAYER_NAME, ENEMY_NAME,
});
