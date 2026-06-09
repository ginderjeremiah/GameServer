/* Admin workspace — the TableEditor + Save flow from the Svelte admin tools,
   rebuilt in the design-system aesthetic. Row state is DERIVED by comparing
   each cell to its original value (mirroring TableRow's modified check), so:
   editing → modified, reverting every cell → clean, and undo-after-delete
   restores the correct modified/clean state. New rows are 'added'. */

let __uid = 1000;
const nextUid = () => ++__uid;

/* Type-aware equality so "8" (typed) === 8 (original number), etc. */
function valuesEqual(col, a, b) {
  if (col.type === 'number') {
    const na = a === '' || a == null ? null : Number(a);
    const nb = b === '' || b == null ? null : Number(b);
    return na === nb;
  }
  if (col.type === 'bool') return !!a === !!b;
  return String(a ?? '') === String(b ?? '');
}

/* ── buttons ─────────────────────────────────────────────────────── */
function AdminButton({ children, onClick, variant = 'ghost', disabled }) {
  const [hover, setHover] = React.useState(false);
  const palette = {
    primary: { bg: 'rgba(161,194,247,0.12)', bd: '#a1c2f7', fg: '#c0d8ff', glow: 'rgba(161,194,247,0.5)' },
    ghost:   { bg: 'transparent', bd: 'rgba(255,255,255,0.18)', fg: 'rgba(240,240,240,0.85)', glow: 'rgba(161,194,247,0.45)' },
    danger:  { bg: 'transparent', bd: 'rgba(224,138,120,0.45)', fg: '#e8b6a6', glow: 'rgba(224,138,120,0.5)' },
  }[variant];
  return (
    <button
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      disabled={disabled}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 7,
        background: palette.bg,
        border: `1px solid ${disabled ? 'rgba(255,255,255,0.08)' : (hover || variant === 'primary') ? palette.bd : 'rgba(255,255,255,0.14)'}`,
        color: disabled ? 'rgba(240,240,240,0.3)' : palette.fg,
        fontFamily: 'Geist Mono, monospace', fontSize: 11.5, letterSpacing: 0.6,
        textTransform: 'uppercase', padding: '8px 16px', borderRadius: 3,
        cursor: disabled ? 'not-allowed' : 'pointer',
        boxShadow: hover && !disabled ? `0 0 10px ${palette.glow}` : 'none',
        transition: 'all 140ms ease', whiteSpace: 'nowrap',
      }}>
      {children}
    </button>
  );
}

/* ── cell editors ────────────────────────────────────────────────── */
function CellInput({ value, onChange, mono, disabled, dirty, align = 'left', numeric }) {
  // number columns reject anything that isn't a valid number — digits, an
  // optional leading minus, and a single decimal point (empty allowed).
  const handleChange = (e) => {
    const v = e.target.value;
    if (numeric && v !== '' && !/^-?\d*\.?\d*$/.test(v)) return;
    onChange(v);
  };
  return (
    <input
      className={'adm-input' + (dirty ? ' dirty' : '')}
      type="text"
      inputMode={numeric ? 'decimal' : undefined}
      value={value}
      disabled={disabled}
      onChange={handleChange}
      style={{
        fontFamily: mono ? 'Geist Mono, monospace' : 'inherit',
        textAlign: align,
      }} />
  );
}

function CellSelect({ value, options, onChange, disabled, dirty }) {
  return (
    <div style={{ position: 'relative', width: '100%' }}>
      <select
        className={'adm-select' + (dirty ? ' dirty' : '')}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}>
        {options.map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
      <svg width="9" height="9" viewBox="0 0 10 10" fill="none"
        stroke="rgba(240,240,240,0.5)" strokeWidth="1.4"
        style={{ position: 'absolute', right: 9, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }}>
        <path d="M2 3.5L5 6.5L8 3.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </div>
  );
}

function CellBool({ value, onChange, disabled, dirty }) {
  const bd = dirty ? '#f0d28a' : value ? '#a1c2f7' : 'rgba(255,255,255,0.22)';
  return (
    <div style={{ display: 'flex', justifyContent: 'center' }}>
      <button
        onClick={disabled ? undefined : () => onChange(!value)}
        disabled={disabled}
        style={{
          width: 18, height: 18, borderRadius: 3,
          border: `1px solid ${bd}`,
          background: value ? 'rgba(161,194,247,0.18)' : dirty ? 'rgba(240,210,138,0.06)' : 'transparent',
          cursor: disabled ? 'default' : 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          padding: 0, transition: 'all 140ms ease',
        }}>
        {value && (
          <svg width="11" height="11" viewBox="0 0 12 12" fill="none" stroke="#c0d8ff" strokeWidth="1.8">
            <path d="M2.5 6.2L4.8 8.5L9.5 3.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        )}
      </button>
    </div>
  );
}

function Cell({ col, value, onChange, deleted, dirty }) {
  if (col.type === 'id') {
    const isNew = value == null || value === '';
    return (
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 12,
        color: isNew ? '#a1c2f7' : 'rgba(240,240,240,0.45)',
        letterSpacing: 0.5,
      }}>{isNew ? 'new' : value}</span>
    );
  }

  let control;
  if (col.type === 'bool') control = <CellBool value={!!value} onChange={onChange} disabled={deleted} dirty={dirty} />;
  else if (col.type === 'select') control = <CellSelect value={value} options={col.options} onChange={onChange} disabled={deleted} dirty={dirty} />;
  else {
    const mono = col.type === 'mono' || col.type === 'number';
    control = <CellInput value={value ?? ''} onChange={onChange} mono={mono} numeric={col.type === 'number'} align={col.type === 'number' ? 'right' : 'left'} disabled={deleted} dirty={dirty} />;
  }

  return (
    <div style={{ position: 'relative' }}>
      {control}
      {dirty && (
        <span title="Changed from saved value" style={{
          position: 'absolute', top: -3, right: -3, width: 6, height: 6,
          borderRadius: '50%', background: '#f0d28a',
          boxShadow: '0 0 6px rgba(240,210,138,0.9)', pointerEvents: 'none',
        }} />
      )}
    </div>
  );
}

/* ── status edge color per row state ─────────────────────────────── */
const EDGE = { added: '#a1c2f7', modified: '#f0d28a', deleted: '#e08a78', clean: 'transparent' };

/* ── table ───────────────────────────────────────────────────────── */
function AdminTable({ config, onDirty }) {
  const editableCols = config.columns.filter((c) => c.type !== 'id');

  const blankRow = () => {
    const d = {};
    config.columns.forEach((c) => {
      if (c.type === 'id') d[c.key] = null;
      else if (c.type === 'bool') d[c.key] = false;
      else if (c.type === 'number') d[c.key] = 0;
      else if (c.type === 'select') d[c.key] = c.options[0];
      else d[c.key] = '';
    });
    return d;
  };

  const freshRows = () => config.rows.map((r) => ({
    uid: nextUid(), data: { ...r }, original: { ...r }, added: false, deleted: false,
  }));

  const [rows, setRows] = React.useState(freshRows);
  const [savedFlash, setSavedFlash] = React.useState(false);

  // derived per-row state
  const isRowDirty = (r) => editableCols.some((c) => !valuesEqual(c, r.data[c.key], r.original?.[c.key]));
  const rowState = (r) => r.deleted ? 'deleted' : r.added ? 'added' : isRowDirty(r) ? 'modified' : 'clean';
  const cellDirty = (r, c) => !r.added && !r.deleted && c.type !== 'id' && !valuesEqual(c, r.data[c.key], r.original?.[c.key]);

  const pending = rows.filter((r) => rowState(r) !== 'clean');
  const counts = {
    added: rows.filter((r) => !r.deleted && r.added).length,
    modified: rows.filter((r) => !r.deleted && !r.added && isRowDirty(r)).length,
    deleted: rows.filter((r) => r.deleted).length,
  };

  React.useEffect(() => { onDirty?.(pending.length); });

  const editCell = (uid, key, val) => setRows((rs) => rs.map((r) =>
    r.uid === uid ? { ...r, data: { ...r.data, [key]: val } } : r));

  const removeRow = (uid) => setRows((rs) => rs.flatMap((r) => {
    if (r.uid !== uid) return [r];
    if (r.added) return [];          // a newly-added row just disappears
    return [{ ...r, deleted: true }];
  }));

  const undoRow = (uid) => setRows((rs) => rs.map((r) =>
    r.uid === uid ? { ...r, deleted: false } : r));   // state re-derives (clean OR modified)

  const resetRow = (uid) => setRows((rs) => rs.map((r) =>   // revert edits to original
    r.uid === uid && r.original ? { ...r, data: { ...r.original } } : r));

  const addRow = () => setRows((rs) => [...rs, { uid: nextUid(), data: blankRow(), original: null, added: true, deleted: false }]);

  const save = () => {
    setRows((rs) => rs.filter((r) => !r.deleted).map((r) => ({
      ...r, original: { ...r.data }, added: false, deleted: false,
    })));
    setSavedFlash(true);
    setTimeout(() => setSavedFlash(false), 1700);
  };
  const discard = () => setRows(freshRows());

  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: 0, flex: 1 }}>
      {/* toolbar */}
      <div style={{
        display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between',
        gap: 16, marginBottom: 18,
      }}>
        <div>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 2,
            textTransform: 'uppercase', color: 'rgba(161,194,247,0.7)', marginBottom: 6,
          }}>Admin · Tools</div>
          <h1 style={{
            margin: 0, padding: 0, fontSize: 26, fontWeight: 500,
            color: '#f0f0f0', letterSpacing: -0.2,
          }}>{config.title}</h1>
        </div>
        <AdminButton variant="ghost" onClick={addRow}>
          <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round">
            <path d="M6 2v8M2 6h8" />
          </svg>
          Add Row
        </AdminButton>
      </div>

      {/* gate select */}
      {config.gate && <GateSelect gate={config.gate} />}

      {/* table */}
      <div style={{
        flex: 1, minHeight: 0, overflow: 'auto',
        border: '1px solid rgba(255,255,255,0.08)', borderRadius: 4,
        background: 'rgba(255,255,255,0.015)',
      }}>
        <table style={{ borderCollapse: 'collapse', width: '100%', minWidth: 'max-content' }}>
          <thead>
            <tr>
              <th style={thStyle(28)}></th>
              {config.columns.map((c) => (
                <th key={c.key} style={{ ...thStyle(c.w), textAlign: c.type === 'number' || c.type === 'bool' ? 'center' : 'left' }}>
                  {c.label}
                </th>
              ))}
              <th style={{ ...thStyle(150), textAlign: 'right', paddingRight: 16 }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const st = rowState(r);
              return (
                <tr key={r.uid} style={{ background: st === 'deleted' ? 'rgba(224,138,120,0.05)' : 'transparent' }}>
                  <td style={{ ...tdStyle, padding: 0, width: 28 }}>
                    <div style={{ width: 3, height: 30, background: EDGE[st], margin: '0 auto',
                      boxShadow: st !== 'clean' ? `0 0 8px ${EDGE[st]}` : 'none' }} />
                  </td>
                  {config.columns.map((c) => (
                    <td key={c.key} style={{ ...tdStyle, opacity: st === 'deleted' ? 0.4 : 1 }}>
                      <Cell col={c} value={r.data[c.key]} deleted={r.deleted} dirty={cellDirty(r, c)}
                        onChange={(v) => editCell(r.uid, c.key, v)} />
                    </td>
                  ))}
                  <td style={{ ...tdStyle, paddingRight: 16 }}>
                    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                      {r.deleted ? (
                        <RowAction kind="restore" onClick={() => undoRow(r.uid)} />
                      ) : (
                        <>
                          {st === 'modified' && <RowAction kind="reset" onClick={() => resetRow(r.uid)} />}
                          <RowAction kind="delete" onClick={() => removeRow(r.uid)} />
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* footer save bar */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        marginTop: 16, paddingTop: 16, borderTop: '1px solid rgba(255,255,255,0.06)',
      }}>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 16,
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5, letterSpacing: 0.4,
          color: 'rgba(240,240,240,0.55)',
        }}>
          {savedFlash ? (
            <span style={{ color: '#bde0b4', display: 'inline-flex', alignItems: 'center', gap: 7 }}>
              <svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.6">
                <path d="M3 7.3L6 10.2L11 4" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              Changes saved
            </span>
          ) : pending.length === 0 ? (
            <span>No unsaved changes</span>
          ) : (
            <>
              <span style={{ color: 'rgba(240,240,240,0.78)' }}>{pending.length} unsaved {pending.length === 1 ? 'change' : 'changes'}</span>
              <ChangePips counts={counts} />
            </>
          )}
        </div>
        <div style={{ display: 'flex', gap: 10 }}>
          <AdminButton variant="ghost" onClick={discard} disabled={pending.length === 0}>Discard</AdminButton>
          <AdminButton variant="primary" onClick={save} disabled={pending.length === 0}>Save Changes</AdminButton>
        </div>
      </div>
    </div>
  );
}

function ChangePips({ counts }) {
  const items = [
    { n: counts.added, c: '#a1c2f7', label: 'added' },
    { n: counts.modified, c: '#f0d28a', label: 'edited' },
    { n: counts.deleted, c: '#e08a78', label: 'removed' },
  ].filter((i) => i.n > 0);
  return (
    <span style={{ display: 'inline-flex', gap: 12 }}>
      {items.map((i) => (
        <span key={i.label} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: i.c }}>
          <span style={{ width: 6, height: 6, borderRadius: '50%', background: i.c, boxShadow: `0 0 6px ${i.c}` }} />
          {i.n} {i.label}
        </span>
      ))}
    </span>
  );
}

const ROW_ACTION = {
  delete:  { label: 'Delete',  color: '#e08a78', hoverBg: 'rgba(224,138,120,0.12)' },
  restore: { label: 'Restore', color: '#a1c2f7', hoverBg: 'rgba(161,194,247,0.12)' },
  reset:   { label: 'Reset',   color: '#f0d28a', hoverBg: 'rgba(240,210,138,0.12)' },
};

function RowAction({ kind, onClick }) {
  const [hover, setHover] = React.useState(false);
  const meta = ROW_ACTION[kind];
  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: hover ? meta.hoverBg : 'transparent',
        border: `1px solid ${hover ? meta.color : 'rgba(255,255,255,0.12)'}`,
        color: hover ? meta.color : 'rgba(240,240,240,0.55)',
        fontFamily: 'Geist Mono, monospace', fontSize: 10.5, letterSpacing: 0.5,
        textTransform: 'uppercase', padding: '4px 10px', borderRadius: 3,
        cursor: 'pointer', transition: 'all 130ms ease',
      }}>
      {meta.label}
    </button>
  );
}

function GateSelect({ gate }) {
  const [value, setValue] = React.useState(gate.value);
  return (
    <div style={{ marginBottom: 18, display: 'flex', alignItems: 'center', gap: 14 }}>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 2,
        textTransform: 'uppercase', color: 'rgba(240,240,240,0.5)',
      }}>{gate.label}</span>
      <div style={{ position: 'relative', width: 240 }}>
        <select className="adm-select adm-select--lg" value={value} onChange={(e) => setValue(e.target.value)}>
          {gate.options.map((o) => <option key={o} value={o}>{o}</option>)}
        </select>
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="rgba(240,240,240,0.6)" strokeWidth="1.4"
          style={{ position: 'absolute', right: 11, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }}>
          <path d="M2 3.5L5 6.5L8 3.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
    </div>
  );
}

const thStyle = (w) => ({
  fontFamily: 'Geist Mono, monospace', fontSize: 9.5, fontWeight: 400,
  letterSpacing: 1.4, textTransform: 'uppercase', color: 'rgba(240,240,240,0.5)',
  textAlign: 'left', padding: '11px 10px', width: w,
  borderBottom: '1px solid rgba(255,255,255,0.1)',
  background: 'rgba(255,255,255,0.025)',
  position: 'sticky', top: 0, zIndex: 1, whiteSpace: 'nowrap',
});

const tdStyle = {
  padding: '6px 10px',
  borderBottom: '1px solid rgba(255,255,255,0.05)',
  verticalAlign: 'middle',
};

Object.assign(window, { AdminTable, AdminButton });
