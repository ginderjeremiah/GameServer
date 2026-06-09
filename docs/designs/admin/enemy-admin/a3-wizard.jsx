// ─────────────────────────────────────────────────────────────────────────
//  DIRECTION 3 — WIZARD (guided "New Enemy" stepper)
//  A focused setup flow over the catalogue. Vertical stepper on the left with
//  live progress; one step's editor on the right; a Review step commits once.
// ─────────────────────────────────────────────────────────────────────────
function Wizard() {
	const store = window.useEnemyStore();
	const patch = store.patchEnemy;
	// Draft enemy lives at the top of the working store.
	const [draftId] = React.useState(() => store.addEnemy(''));
	const draft = store.enemies.find((e) => e.id === draftId) || store.enemies[0];

	const STEPS = [...window.SECTIONS, { key: 'review', label: 'Review', glyph: 'check', desc: 'Confirm & create' }];
	const [step, setStep] = React.useState(0);
	const cur = STEPS[step];
	const last = step === STEPS.length - 1;

	const stepDone = (key) => {
		if (key === 'review') return false;
		return window.completeness(draft)[key];
	};
	const reachedMax = React.useRef(0);
	if (step > reachedMax.current) reachedMax.current = step;

	const blurbs = {
		identity: 'Give the enemy a name and decide whether it’s a standard foe or a boss encounter.',
		attrs: 'Define how this enemy’s stats scale. Base is the level-1 value; per-level is added each level.',
		skills: 'Choose the skills this enemy can use in battle. An enemy with no skills can’t attack.',
		spawns: 'Pick the zones this enemy spawns in. Weight controls how often it appears versus others.'
	};

	return (
		<div style={{ display: 'flex', height: '100%', position: 'relative' }}>
			{/* dimmed catalogue context */}
			<div style={{ position: 'absolute', inset: 0, padding: '20px 40px', opacity: 0.18, pointerEvents: 'none', filter: 'blur(1px)' }}>
				{store.enemies.slice(1, 9).map((e) => (
					<div key={e.id} style={{ padding: '12px 0', borderBottom: '1px solid var(--border-subtle)', display: 'flex', gap: 12 }}>
						<span style={{ fontSize: 14 }}>{e.name}</span><MetaLine e={e} />
					</div>
				))}
			</div>

			{/* wizard surface */}
			<div style={{ position: 'relative', zIndex: 2, margin: 'auto', width: 'min(920px, calc(100% - 60px))', height: 'calc(100% - 48px)', display: 'flex', background: 'var(--panel)', border: '1px solid var(--border-light)', borderRadius: 10, boxShadow: '0 24px 64px rgba(0,0,0,.55)', overflow: 'hidden' }}>
				{/* stepper rail */}
				<div style={{ width: 252, flexShrink: 0, background: 'var(--surface)', borderRight: '1px solid var(--border-subtle)', padding: '24px 20px', display: 'flex', flexDirection: 'column' }}>
					<div className="eyebrow">New Enemy</div>
					<div style={{ fontSize: 16, fontWeight: 500, marginBottom: 22 }}>Setup</div>
					<div style={{ display: 'flex', flexDirection: 'column', gap: 2, flex: 1 }}>
						{STEPS.map((s, i) => {
							const active = i === step;
							const done = stepDone(s.key);
							const reachable = i <= reachedMax.current;
							return (
								<button key={s.key} disabled={!reachable && i > reachedMax.current + 0} onClick={() => reachable && setStep(i)}
									style={{ display: 'flex', alignItems: 'center', gap: 12, background: active ? 'rgba(161,194,247,.08)' : 'transparent', border: 'none', borderRadius: 6, padding: '11px 12px', cursor: reachable ? 'pointer' : 'default', textAlign: 'left', opacity: reachable ? 1 : 0.4 }}>
									<span style={{
										width: 24, height: 24, borderRadius: '50%', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
										fontFamily: 'var(--mono)', fontSize: 11,
										border: '1px solid ' + (active ? 'var(--accent)' : done ? 'var(--success)' : 'var(--border-light)'),
										background: active ? 'rgba(161,194,247,.15)' : done ? 'rgba(189,224,180,.12)' : 'transparent',
										color: active ? 'var(--accent-light)' : done ? 'var(--success)' : 'var(--text-muted)'
									}}>
										{done && !active ? <Icon k="check" size={12} sw={1.8} /> : i + 1}
									</span>
									<span style={{ flex: 1 }}>
										<span style={{ display: 'block', fontSize: 13, color: active ? 'var(--text-primary)' : 'var(--text-secondary)' }}>{s.label}</span>
										<span style={{ display: 'block', fontFamily: 'var(--mono)', fontSize: 9.5, letterSpacing: '.3px', color: 'var(--text-muted)', marginTop: 2 }}>{s.desc}</span>
									</span>
								</button>
							);
						})}
					</div>
					<div style={{ marginTop: 16 }}>
						<CompletenessMeter e={draft} />
						<div style={{ fontFamily: 'var(--mono)', fontSize: 10, color: 'var(--text-muted)', marginTop: 8 }}>{window.completeCount(draft)} of 4 sections ready</div>
					</div>
				</div>

				{/* step body */}
				<div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
					<div style={{ flex: 1, overflowY: 'auto', padding: '30px 34px' }}>
						<div className="eyebrow">Step {step + 1} of {STEPS.length}</div>
						<h2 style={{ margin: '0 0 7px', fontSize: 22, fontWeight: 500 }}>{cur.key === 'review' ? 'Review & create' : cur.label}</h2>
						<p style={{ margin: '0 0 26px', color: 'var(--text-tertiary)', fontSize: 13, lineHeight: 1.5, maxWidth: 540 }}>
							{cur.key === 'review' ? 'Confirm everything looks right. You can still go back to any step. Warnings won’t block creation, but they flag an unfinished enemy.' : blurbs[cur.key]}
						</p>

						{cur.key === 'review' ? (
							<div style={{ maxWidth: 540 }}>
								<div className="card" style={{ padding: 20, marginBottom: 16 }}>
									<div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
										<h3 style={{ margin: 0, fontSize: 18, fontWeight: 500 }}>{draft.name || 'Unnamed enemy'}</h3>
										{draft.isBoss && <BossTag />}
									</div>
									<Checklist e={draft} />
								</div>
								{window.enemyWarnings(draft).length > 0 && (
									<div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
										{window.enemyWarnings(draft).map((w) => <WarnChip key={w} text={w} />)}
									</div>
								)}
							</div>
						) : (
							<div style={{ maxWidth: 600 }}><SectionEditor which={cur.key} e={draft} patch={patch} /></div>
						)}
					</div>

					{/* footer nav */}
					<div className="save-bar">
						<button className="btn" disabled={step === 0} onClick={() => setStep((s) => s - 1)}>Back</button>
						<div className="save-actions">
							{!last && cur.key !== 'identity' && (
								<button className="btn" onClick={() => setStep((s) => s + 1)}>Skip for now</button>
							)}
							{last ? (
								<button className="btn primary" onClick={store.save}><Icon k="check" size={13} sw={1.7} />Create Enemy</button>
							) : (
								<button className="btn primary" onClick={() => setStep((s) => s + 1)}>Continue<Icon k="chevR" size={12} /></button>
							)}
						</div>
					</div>
				</div>
			</div>
		</div>
	);
}

window.Wizard = Wizard;
