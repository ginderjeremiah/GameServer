/* Log C · Event Cards
   Each entry is a small horizontal card with icon left, message middle,
   numeric callout right. Cards have subtle kind-tinted backgrounds and
   left accent stripes — feels like an "ESPN scorecard" stream. New cards
   fade + slide in from the bottom. */

function LogCards() {
  const entries = useLogStream({ maxEntries: 5 });
  const containerRef = React.useRef(null);

  React.useEffect(() => {
    if (containerRef.current) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight;
    }
  }, [entries.length]);

  return (
    <div style={shellDark()}>
      <LogHeader label="Combat Log" entries={entries[entries.length - 1]?.id ?? 0} />
      <div ref={containerRef} style={{
        flex: 1, overflow: 'hidden',
        padding: '4px 10px 0',
        display: 'flex', flexDirection: 'column', gap: 5,
        maskImage: 'linear-gradient(to bottom, transparent 0%, black 12%, black 100%)',
        WebkitMaskImage: 'linear-gradient(to bottom, transparent 0%, black 12%, black 100%)',
      }}>
        {entries.map((e, i) => {
          const age = entries.length - 1 - i;
          const opacity = Math.max(0.5, 1 - age * 0.13);
          return <EventCard key={e.id} entry={e} opacity={opacity} />;
        })}
      </div>

      <style>{`
        @keyframes lc-card-in {
          from { opacity: 0; transform: translateY(10px) scale(0.985); }
          to   { opacity: var(--target-opacity, 1); transform: translateY(0) scale(1); }
        }
      `}</style>
    </div>
  );
}

function EventCard({ entry, opacity }) {
  const accent = KIND_COLOR[entry.kind];
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12,
      padding: '7px 10px 7px 12px',
      background: `${accent}0a`,
      borderLeft: `2px solid ${accent}aa`,
      borderRadius: '0 2px 2px 0',
      opacity,
      animation: 'lc-card-in 320ms ease-out backwards',
      '--target-opacity': opacity,
      transition: 'opacity 240ms',
    }}>
      <div style={{
        width: 22, height: 22, flexShrink: 0,
        background: `${accent}18`,
        border: `1px solid ${accent}55`,
        borderRadius: 2,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <KindGlyph kind={entry.kind} size={12} />
      </div>

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1, minWidth: 0 }}>
        <div style={{
          display: 'flex', alignItems: 'baseline', gap: 6,
          whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          fontSize: 12.5, color: 'rgba(240,240,240,0.9)',
        }}>
          <CardMessage entry={entry} />
        </div>
        <div style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
          letterSpacing: 0.5, color: 'rgba(240,240,240,0.42)',
        }}>
          {entry.time} · {KIND_LABEL[entry.kind].toLowerCase()}
        </div>
      </div>

      <CardValue entry={entry} accent={accent} />
    </div>
  );
}

function CardMessage({ entry }) {
  if (entry.kind === 'hit' || entry.kind === 'crit') {
    return <>
      <span style={{ color: '#c0d8ff', fontWeight: 500 }}>{entry.actor}</span>
      <span style={{ color: 'rgba(240,240,240,0.55)' }}>·</span>
      <span style={{ color: '#f0f0f0', fontStyle: 'italic' }}>{entry.skill}</span>
      <span style={{ color: 'rgba(240,240,240,0.55)' }}>→</span>
      <span style={{ color: '#e8b6a6' }}>{entry.target}</span>
    </>;
  }
  if (entry.kind === 'enemy') {
    return <>
      <span style={{ color: '#e8b6a6', fontWeight: 500 }}>{entry.actor}</span>
      <span style={{ color: 'rgba(240,240,240,0.55)' }}>·</span>
      <span style={{ color: '#f0f0f0', fontStyle: 'italic' }}>{entry.skill}</span>
      <span style={{ color: 'rgba(240,240,240,0.55)' }}>→</span>
      <span style={{ color: '#c0d8ff' }}>{entry.target}</span>
    </>;
  }
  if (entry.kind === 'loot') {
    return <>
      <span style={{ color: '#c0d8ff' }}>Aelara</span>
      <span style={{ color: 'rgba(240,240,240,0.55)' }}>picked up</span>
      <span style={{ color: '#bde0b4', fontWeight: 500 }}>{entry.item}</span>
    </>;
  }
  return <span style={{ color: 'rgba(240,240,240,0.78)' }}>{entry.text}</span>;
}

function CardValue({ entry, accent }) {
  if (entry.value != null) {
    return (
      <div style={{
        display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 0,
      }}>
        <span style={{
          fontFamily: 'Geist, sans-serif', fontWeight: 600,
          fontSize: entry.isCrit ? 18 : 15,
          color: accent, letterSpacing: -0.2,
          lineHeight: 1,
        }}>
          {entry.kind === 'enemy' ? '−' : ''}{entry.value}
        </span>
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
          letterSpacing: 1, textTransform: 'uppercase',
          color: 'rgba(240,240,240,0.45)',
          marginTop: 1,
        }}>{entry.isCrit ? 'crit dmg' : 'dmg'}</span>
      </div>
    );
  }
  if (entry.count != null) {
    return (
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 13, color: accent,
        letterSpacing: 0.3,
      }}>×{entry.count}</span>
    );
  }
  return null;
}

window.LogCards = LogCards;
