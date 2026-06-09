/* Log D · Latest + History
   Spotlight for the most recent event (large readable callout) + compact
   history list below. New events slide into the spotlight; the previous
   spotlight occupant rolls into the top of the history. Best when only the
   most recent event matters at a glance. */

function LogSpotlight() {
  const entries = useLogStream({ maxEntries: 10, intervalMs: 1300 });
  const latest = entries[entries.length - 1];
  const history = entries.slice(0, -1).slice(-4).reverse(); // 4 most recent before latest, newest first

  return (
    <div style={shellDark()}>
      <LogHeader label="Combat Log" entries={latest?.id ?? 0} />

      {/* spotlight */}
      <div key={latest?.id} style={{
        margin: '0 8px 10px',
        padding: '12px 14px',
        background: `${KIND_COLOR[latest?.kind ?? 'system']}10`,
        border: `1px solid ${KIND_COLOR[latest?.kind ?? 'system']}55`,
        borderRadius: 3,
        position: 'relative', overflow: 'hidden',
        animation: 'ls-spot-in 320ms cubic-bezier(.2,.7,.2,1)',
        minHeight: 64,
      }}>
        {/* flash overlay */}
        <div style={{
          position: 'absolute', inset: 0,
          background: `linear-gradient(90deg, ${KIND_COLOR[latest?.kind ?? 'system']}18, transparent 60%)`,
          animation: 'ls-flash 600ms ease-out',
          pointerEvents: 'none',
        }} />
        {latest && <SpotlightContent entry={latest} />}
      </div>

      {/* history */}
      <div style={{
        flex: 1, overflow: 'hidden', padding: '0 8px',
        display: 'flex', flexDirection: 'column', gap: 2,
      }}>
        {history.map((e, i) => (
          <HistoryRow key={e.id} entry={e} dim={i * 0.18} />
        ))}
      </div>

      <style>{`
        @keyframes ls-spot-in {
          from { opacity: 0; transform: translateY(8px) scale(0.99); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes ls-flash {
          from { opacity: 1; }
          to   { opacity: 0; }
        }
      `}</style>
    </div>
  );
}

function SpotlightContent({ entry }) {
  const accent = KIND_COLOR[entry.kind];
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14,
      position: 'relative', zIndex: 1,
    }}>
      <div style={{
        width: 36, height: 36, flexShrink: 0,
        background: `${accent}1a`,
        border: `1px solid ${accent}66`,
        borderRadius: 3,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <KindGlyph kind={entry.kind} size={18} />
      </div>

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display: 'flex', alignItems: 'baseline', gap: 8,
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
          letterSpacing: 1.5, textTransform: 'uppercase',
          color: accent, marginBottom: 3,
        }}>
          {KIND_LABEL[entry.kind]}
          {entry.isCrit && <span style={{ color: '#f0d28a' }}>· Critical</span>}
          <span style={{ color: 'rgba(240,240,240,0.45)' }}>· {entry.time}</span>
        </div>
        <div style={{
          fontSize: 15, color: '#f0f0f0', letterSpacing: -0.1,
          whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
        }}>
          <SpotlightMessage entry={entry} />
        </div>
      </div>

      <SpotlightValue entry={entry} accent={accent} />
    </div>
  );
}

function SpotlightMessage({ entry }) {
  if (entry.kind === 'hit' || entry.kind === 'crit') {
    return <>
      <strong style={{ color: '#c0d8ff', fontWeight: 500 }}>{entry.actor}</strong>
      <span style={{ color: 'rgba(240,240,240,0.62)' }}> cast </span>
      <strong style={{ color: '#f0f0f0', fontStyle: 'italic', fontWeight: 500 }}>{entry.skill}</strong>
      <span style={{ color: 'rgba(240,240,240,0.62)' }}> on </span>
      <strong style={{ color: '#e8b6a6', fontWeight: 500 }}>{entry.target}</strong>
    </>;
  }
  if (entry.kind === 'enemy') {
    return <>
      <strong style={{ color: '#e8b6a6', fontWeight: 500 }}>{entry.actor}</strong>
      <span style={{ color: 'rgba(240,240,240,0.62)' }}> cast </span>
      <strong style={{ color: '#f0f0f0', fontStyle: 'italic', fontWeight: 500 }}>{entry.skill}</strong>
      <span style={{ color: 'rgba(240,240,240,0.62)' }}> on </span>
      <strong style={{ color: '#c0d8ff', fontWeight: 500 }}>{entry.target}</strong>
    </>;
  }
  if (entry.kind === 'loot') {
    return <>
      <strong style={{ color: '#c0d8ff', fontWeight: 500 }}>{PLAYER_NAME}</strong>
      <span style={{ color: 'rgba(240,240,240,0.62)' }}> picked up </span>
      <strong style={{ color: '#bde0b4', fontWeight: 500 }}>{entry.item}</strong>
    </>;
  }
  return <span style={{ color: 'rgba(240,240,240,0.78)' }}>{entry.text}</span>;
}

function SpotlightValue({ entry, accent }) {
  if (entry.value != null) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end' }}>
        <span style={{
          fontFamily: 'Geist, sans-serif', fontWeight: 600,
          fontSize: entry.isCrit ? 28 : 22, color: accent,
          letterSpacing: -0.4, lineHeight: 1,
          textShadow: `0 0 14px ${accent}55`,
        }}>{entry.kind === 'enemy' ? '−' : ''}{entry.value}</span>
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
          letterSpacing: 1.1, textTransform: 'uppercase',
          color: 'rgba(240,240,240,0.5)', marginTop: 3,
        }}>damage</span>
      </div>
    );
  }
  if (entry.count != null) {
    return (
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 18, color: accent,
        letterSpacing: 0.3,
      }}>×{entry.count}</span>
    );
  }
  return null;
}

function HistoryRow({ entry, dim }) {
  const opacity = Math.max(0.32, 0.85 - dim);
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '3px 8px',
      fontSize: 11.5, opacity, transition: 'opacity 240ms',
    }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10,
        color: 'rgba(240,240,240,0.4)', letterSpacing: 0.4,
        minWidth: 36,
      }}>{entry.time}</span>
      <KindGlyph kind={entry.kind} size={10} />
      <span style={{
        flex: 1,
        color: 'rgba(240,240,240,0.72)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>{plainText(entry)}</span>
      {entry.value != null && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
          color: KIND_COLOR[entry.kind], letterSpacing: 0.3,
        }}>{entry.kind === 'enemy' ? '−' : ''}{entry.value}</span>
      )}
    </div>
  );
}

window.LogSpotlight = LogSpotlight;
