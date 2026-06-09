/* Shared bits for tooltip variants — sample data + helpers + primitives. */

const ITEM_CATEGORIES = {
  1: 'Helm', 2: 'Chest', 3: 'Leg', 4: 'Boot', 5: 'Weapon', 6: 'Accessory',
};

const CATEGORY_ACCENT = {
  1: '#a1c2f7', 2: '#a1c2f7', 3: '#a1c2f7', 4: '#a1c2f7',
  5: '#e08778', 6: '#e8c878',
};

const MOD_TYPE_LABEL = { 1: 'Component', 2: 'Prefix', 3: 'Suffix' };
const MOD_TYPE_ACCENT = { 1: 'rgba(240,240,240,0.55)', 2: '#9bc7d9', 3: '#c0a8e6' };

const SAMPLE_ITEM = {
  name: 'Aegis-Forged Saber',
  itemCategoryId: 5, // Weapon
  attributes: [
    { name: 'Strength',         value: 18 },
    { name: 'Dexterity',        value: 12 },
    { name: 'Critical Chance',  value: 5, suffix: '%' },
    { name: 'Defense',          value: -2 },
  ],
  appliedMods: [
    { name: 'Honed',     description: '+15% damage to first hit each fight.',  modType: 2 },
    { name: 'Vampiric',  description: 'Heal 8% of damage dealt.',              modType: 3 },
    { name: 'Iron Core', description: 'Reduce durability loss by 20%.',        modType: 1 },
  ],
  description: 'A blade forged in the Aegis foundry. Whispers when drawn.',
  equipped: true,
};

const SAMPLE_SKILL = {
  name: 'Cleave',
  cooldownMs: 2400,
  remainingCdMs: 800,
  baseDamage: 25,
  damageMultipliers: [
    { attribute: 'Strength',  multiplier: 1.5, value: 27 },
    { attribute: 'Dexterity', multiplier: 0.5, value: 6 },
  ],
  enemyDefense: 12,
  cdMultiplier: 1.2,
  description: 'A wide arcing slash that strikes the target.',
};

function skillStats(s) {
  const adjustedCd = (s.cooldownMs / 1000) / s.cdMultiplier;
  const remainingCd = s.remainingCdMs / 1000;
  const multiplierTotal = s.damageMultipliers.reduce((a, m) => a + m.value, 0);
  const preDefense = s.baseDamage + multiplierTotal;
  const total = Math.max(0, preDefense - s.enemyDefense);
  const dps = total / adjustedCd;
  return { adjustedCd, remainingCd, multiplierTotal, preDefense, total, dps };
}

function fmt(n) { return Number.isInteger(n) ? n.toString() : (+n).toFixed(2); }
function statColor(v) {
  if (v > 0) return '#bde0b4';
  if (v < 0) return '#f0a094';
  return 'rgba(240,240,240,0.7)';
}
function statSign(v, suffix = '') {
  const s = v > 0 ? '+' : '';
  return `${s}${v}${suffix}`;
}

/* A subtle "where the tooltip came from" indicator — a faded slot. Used in
   each artboard so the tooltip reads as a hovering popup rather than a card. */
function HoverContext({ children, slotIcon, kind = 'item' }) {
  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      position: 'relative',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      overflow: 'hidden',
    }}>
      {/* ghost slot at top-left as a faint origin marker */}
      <div style={{
        position: 'absolute', top: 28, left: 28,
        width: 48, height: 48,
        background: 'rgba(255,255,255,0.03)',
        border: '1px dashed rgba(255,255,255,0.18)',
        borderRadius: 3,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        {slotIcon}
      </div>
      <div style={{
        position: 'absolute', top: 76, left: 52,
        width: 70, height: 1,
        background: 'linear-gradient(to right, rgba(255,255,255,0.22), transparent)',
        transform: 'rotate(30deg)', transformOrigin: 'left center',
      }} />
      <div style={{
        position: 'absolute', top: 28, left: 28,
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        letterSpacing: 1.5, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.4)',
        transform: 'translateY(-18px)',
        whiteSpace: 'nowrap',
      }}>{kind === 'skill' ? 'Hovered skill' : 'Hovered item'}</div>
      {children}
    </div>
  );
}

/* small icons — only used inside hover-context placeholder */
function SwordGlyph({ color = 'rgba(240,240,240,0.65)' }) {
  return <svg width="22" height="22" viewBox="0 0 16 16" fill="none" stroke={color} strokeWidth="1.3" strokeLinejoin="round" strokeLinecap="round"><path d="M11 1.5l3.5 3.5-7.5 7.5-3.5-3.5z M3 12l1.7 1.7" /></svg>;
}
function BoltGlyph({ color = 'rgba(240,240,240,0.65)' }) {
  return <svg width="22" height="22" viewBox="0 0 16 16" fill="none" stroke={color} strokeWidth="1.3" strokeLinejoin="round"><path d="M9 1.5L3 9h4l-1 5.5L13 7H9z" /></svg>;
}

Object.assign(window, {
  ITEM_CATEGORIES, CATEGORY_ACCENT, MOD_TYPE_LABEL, MOD_TYPE_ACCENT,
  SAMPLE_ITEM, SAMPLE_SKILL,
  skillStats, fmt, statColor, statSign,
  HoverContext, SwordGlyph, BoltGlyph,
});
