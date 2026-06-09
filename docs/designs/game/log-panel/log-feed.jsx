/* Log B · Streaming Feed
   Traditional chronological feed. Newest at the bottom (combat-log
   convention). Each row: timestamp + kind glyph + colored text + optional
   numeric callout on the right. New entries fade in from below; older ones
   reduce in opacity as they age out the top. */

function LogFeed() {
  const entries = useLogStream({ maxEntries: 7 });
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
        padding: '4px 0',
        maskImage: 'linear-gradient(to bottom, transparent 0%, black 12%, black 100%)',
        WebkitMaskImage: 'linear-gradient(to bottom, transparent 0%, black 12%, black 100%)',
      }}>
        {entries.map((e, i) => {
          const age = entries.length - 1 - i;
          const opacity = Math.max(0.35, 1 - age * 0.085);
          return <FeedRow key={e.id} entry={e} opacity={opacity} />;
        })}
      </div>

      <style>{`
        @keyframes lf-row-in {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: var(--target-opacity, 1); transform: translateY(0); }
        }
      `}</style>
    </div>
  );
}

function FeedRow({ entry, opacity }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '4px 14px',
      animation: 'lf-row-in 280ms ease-out backwards',
      '--target-opacity': opacity,
      opacity,
      transition: 'opacity 240ms',
    }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
        color: 'rgba(240,240,240,0.4)', letterSpacing: 0.4,
        minWidth: 38,
      }}>{entry.time}</span>

      <div style={{
        width: 18, height: 18,
        background: `${KIND_COLOR[entry.kind]}15`,
        border: `1px solid ${KIND_COLOR[entry.kind]}55`,
        borderRadius: 2,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        flexShrink: 0,
      }}>
        <KindGlyph kind={entry.kind} size={11} />
      </div>

      <div style={{
        flex: 1, fontSize: 12.5,
        color: 'rgba(240,240,240,0.85)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>
        <FeedMessage entry={entry} />
      </div>

      {entry.value != null && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
          letterSpacing: 0.3, fontWeight: 500,
          color: KIND_COLOR[entry.kind],
          minWidth: 42, textAlign: 'right',
        }}>
          {entry.kind === 'enemy' ? '−' : ''}{entry.value}
          {entry.isCrit && (
            <span style={{
              marginLeft: 4,
              fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
              letterSpacing: 1, textTransform: 'uppercase',
              color: '#f0d28a',
            }}>crit</span>
          )}
        </span>
      )}
      {entry.count != null && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
          letterSpacing: 0.3, color: KIND_COLOR.loot,
          minWidth: 42, textAlign: 'right',
        }}>×{entry.count}</span>
      )}
    </div>
  );
}

function FeedMessage({ entry }) {
  if (entry.kind === 'hit' || entry.kind === 'crit') {
    return <>
      <Actor>{entry.actor}</Actor> cast <Skill>{entry.skill}</Skill> on <Target enemy>{entry.target}</Target>
    </>;
  }
  if (entry.kind === 'enemy') {
    return <>
      <Actor enemy>{entry.actor}</Actor> cast <Skill enemy>{entry.skill}</Skill> on <Target>{entry.target}</Target>
    </>;
  }
  if (entry.kind === 'loot') {
    return <>Picked up <span style={{ color: KIND_COLOR.loot }}>{entry.item}</span></>;
  }
  return <span style={{ color: 'rgba(240,240,240,0.65)' }}>{entry.text}</span>;
}

function Actor({ children, enemy }) {
  return <span style={{ color: enemy ? '#e8b6a6' : '#c0d8ff', fontWeight: 500 }}>{children}</span>;
}
function Target({ children, enemy }) {
  return <span style={{ color: enemy ? '#e8b6a6' : '#c0d8ff' }}>{children}</span>;
}
function Skill({ children, enemy }) {
  return <span style={{
    color: enemy ? '#e8b6a6' : '#c0d8ff',
    fontStyle: 'italic',
  }}>{children}</span>;
}

window.LogFeed = LogFeed;
