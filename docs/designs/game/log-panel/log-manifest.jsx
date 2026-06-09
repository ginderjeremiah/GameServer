/* Log A · Sliding Manifest
   Windowed list with newest entry at the TOP (like D). Each new event slides
   the stack downward with smooth motion; the new row fades in through the top
   mask while the oldest visible row fades out through the bottom. Rows use the
   structured formatting from B — timestamp + kind chip + colored message +
   numeric callout. */

function LogManifest() {
  const entries = useLogStream({ maxEntries: 14 });
  const rowHeight = 34;
  const visibleRows = 5;
  const ordered = [...entries].reverse(); // newest first
  const newestId = entries[entries.length - 1]?.id;

  const scrollRef = React.useRef(null);
  const atTopRef = React.useRef(true); // is the user pinned to the top (reading live)?

  const onScroll = () => {
    const el = scrollRef.current;
    if (el) atTopRef.current = el.scrollTop <= 4;
  };

  // When a new entry arrives: if the user is at the top, keep them pinned there
  // and let the new row animate in (the stack visibly slides down). If they've
  // scrolled back to read history, nudge scrollTop by one row so their view
  // stays put as content grows above — no yanking, no entrance animation.
  React.useLayoutEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    if (atTopRef.current) el.scrollTop = 0;
    else el.scrollTop += rowHeight;
  }, [newestId]);

  return (
    <div style={shellDark()}>
      <LogHeader label="Combat Log" entries={newestId ?? 0} />
      <div
        ref={scrollRef}
        onScroll={onScroll}
        className="lm-scroll"
        style={{
          position: 'relative',
          height: rowHeight * visibleRows,
          overflowY: 'auto', overflowX: 'hidden',
          maskImage: 'linear-gradient(to bottom, black 0%, black 78%, transparent 100%)',
          WebkitMaskImage: 'linear-gradient(to bottom, black 0%, black 78%, transparent 100%)',
        }}>
        {ordered.map((e, i) => (
          <ManifestRow
            key={e.id}
            entry={e}
            rowHeight={rowHeight}
            index={i}
            isLatest={i === 0}
            animateIn={i === 0 && atTopRef.current}
          />
        ))}
      </div>

      <style>{`
        .lm-scroll { scrollbar-width: thin; scrollbar-color: rgba(255,255,255,0.16) transparent; }
        .lm-scroll::-webkit-scrollbar { width: 6px; }
        .lm-scroll::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.16); border-radius: 3px; }
        .lm-scroll::-webkit-scrollbar-track { background: transparent; }
        @keyframes lm-row-in {
          from { height: 0px; opacity: 0; }
          to   { height: ${rowHeight}px; opacity: 1; }
        }
      `}</style>
    </div>
  );
}

function ManifestRow({ entry, rowHeight, index, isLatest, animateIn }) {
  const accent = KIND_COLOR[entry.kind];
  // Newest row is fully prominent; older rows fall off gently so the top of the
  // list always reads as "now".
  const rowOpacity = isLatest ? 1 : Math.max(0.4, 0.9 - index * 0.1);
  const textColor = isLatest ? 'rgba(240,240,240,0.97)' : 'rgba(240,240,240,0.82)';
  return (
    <div style={{
      height: rowHeight,
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '0 14px',
      opacity: rowOpacity,
      overflow: 'hidden',
      background: isLatest
        ? `linear-gradient(90deg, ${accent}1c, ${accent}08 38%, transparent 72%)`
        : undefined,
      boxShadow: isLatest ? `inset 2px 0 0 ${accent}` : undefined,
      animation: animateIn ? 'lm-row-in 360ms cubic-bezier(.22,.61,.36,1)' : undefined,
    }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
        color: isLatest ? 'rgba(240,240,240,0.55)' : 'rgba(240,240,240,0.4)',
        letterSpacing: 0.4, minWidth: 38,
      }}>{entry.time}</span>

      <div style={{
        width: 18, height: 18,
        background: `${accent}${isLatest ? '22' : '15'}`,
        border: `1px solid ${accent}${isLatest ? '77' : '55'}`,
        borderRadius: 2,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        flexShrink: 0,
      }}>
        <KindGlyph kind={entry.kind} size={11} />
      </div>

      <div style={{
        flex: 1, fontSize: 12.5,
        color: textColor,
        fontWeight: isLatest ? 500 : 400,
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>
        <ManifestMessage entry={entry} />
      </div>

      {entry.value != null && (
        <span style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
          letterSpacing: 0.3, fontWeight: 500,
          color: accent, minWidth: 42, textAlign: 'right',
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

function ManifestMessage({ entry }) {
  if (entry.kind === 'hit' || entry.kind === 'crit') {
    return <>
      <MfActor>{entry.actor}</MfActor> cast <MfSkill>{entry.skill}</MfSkill> on <MfTarget enemy>{entry.target}</MfTarget>
    </>;
  }
  if (entry.kind === 'enemy') {
    return <>
      <MfActor enemy>{entry.actor}</MfActor> cast <MfSkill enemy>{entry.skill}</MfSkill> on <MfTarget>{entry.target}</MfTarget>
    </>;
  }
  if (entry.kind === 'loot') {
    return <>Picked up <span style={{ color: KIND_COLOR.loot }}>{entry.item}</span></>;
  }
  return <span style={{ color: 'rgba(240,240,240,0.65)' }}>{entry.text}</span>;
}

function MfActor({ children, enemy }) {
  return <span style={{ color: enemy ? '#e8b6a6' : '#c0d8ff', fontWeight: 500 }}>{children}</span>;
}
function MfTarget({ children, enemy }) {
  return <span style={{ color: enemy ? '#e8b6a6' : '#c0d8ff' }}>{children}</span>;
}
function MfSkill({ children, enemy }) {
  return <span style={{ color: enemy ? '#e8b6a6' : '#c0d8ff', fontStyle: 'italic' }}>{children}</span>;
}

/* ─── shared chrome ────────────────────────────────────────────── */

function shellDark() {
  return {
    width: '100%', height: '100%',
    background: 'rgba(0,0,0,0.4)',
    border: '1px solid rgba(255,255,255,0.06)',
    borderRadius: 3,
    padding: '10px 4px 4px',
    boxSizing: 'border-box',
    display: 'flex', flexDirection: 'column',
  };
}

function LogHeader({ label, entries }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '0 12px 8px',
    }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        letterSpacing: 1.5, textTransform: 'uppercase',
        color: 'rgba(192,216,255,0.7)',
      }}>{label}</span>
      <div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.07)' }} />
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        color: 'rgba(240,240,240,0.45)', letterSpacing: 0.5,
      }}>{entries} events</span>
    </div>
  );
}

window.LogManifest = LogManifest;
window.shellDark = shellDark;
window.LogHeader = LogHeader;
