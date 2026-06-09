# Tooltips

_Extracted from the Login Form Modernization session, 2026-05-25 UTC. Same design session as game/login, game/loading, game/game-screen, game/fight, and game/log-panel chat-1._

---

## User

For now, let's not add a keyboard shortcut. I think we're almost ready to move on to the main game screen, but there's one thing we should probably handle first, which is the tooltips. Right now, tooltips are used for items (in the inventory screen) and skills (in the fight screen). I'd like to modernize these first before doing the game screens, as they are possibly the most complicated components right now.

## Assistant

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_grep]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: write_file]_

I have what I need. The current tooltips use the off-white card look and show:
- **Items**: title, stat list, applied-mods list, description, "Equipped" badge
- **Skills**: title with remaining cooldown, damage breakdown (base + per-attribute multipliers − enemy defense), total, cooldown, DPS

Plan: 3 modernized directions, each rendering both item AND skill tooltips so you can evaluate them together. Canvas sections: Item tooltips (Current + A/B/C) and Skill tooltips (Current + A/B/C).

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Three modernized tooltip directions on the canvas, each rendering both item and skill versions.

Shared upgrades across all three:
- **Dark glass shell** matching login/loading/nav (`rgba(20,21,27,0.96)`, soft shadow, blur)
- **Geist + Geist Mono** type pairing
- **Color-coded values**: positive `+` green, negative `−` red, neutral light
- **Mod-type accent** color per Component/Prefix/Suffix
- **Real category labels** (Weapon, Helm, etc.) shown as small mono caps
- **Item Equipped** treatment promoted to a small status pill rather than a footer

**A · Refined Card** — closest to current shape, just modernized. Title row with category caption + name, mods rendered as left-bordered tiles, damage breakdown as a calculation list ending in a divider + total.

**B · Data Sheet** — header strip with mono name + ID, content in 2-column tables with right-aligned mono numbers. Skill version has a cooldown-pip strip on the right. Reads like an inspection panel.

**C · Layered Panel** — left accent strip colored by category (weapon = orange, armor = blue, accessory = gold; skill = blue). Larger 18px title, sectioned with mono header dividers. Damage breakdown lands on a big total number. Cooldown rendered as a progress pill that fills toward "READY".

Standing by for verifier and your call on direction. A few open questions:

1. Do you want item rarity / tier shown anywhere (your data doesn't seem to model it yet — just asking)?
2. For skills, currently I show **remaining seconds** in the cooldown indicator. Want a different format (% ready, or absolute "0.8s / 2.0s")?
3. Should I add tooltip "tail" / arrow pointing back to the hovered element, or keep them cleanly detached like the current implementation?

## User

Verifier agent check completed

## Assistant

Verifier passed. Ready for your direction on which tooltip style to take forward.

## User

Wow, these are all pretty good. For Inventory items specifically, I'd like to do C with some aspects of A. I like the left accent strips for the entire card and would like to also use those strips and boxes for the applied mods, like in A. Also, I'd like to switch to using the EQUIPPED badge from option A. For the skills, I like the overall styling of option C, except for the total being a big number. I like the way the total is displayed in options B/C where the total text and value are bolded and just slightly larger than the other text. I'm also not sure whether I like the cooldown strip pips or the progress pill. Could you show me what those would look like when animated?

## Assistant

_[tool: write_file]_

Building the final mix and an animated cooldown comparison.

_[tool: str_replace_edit]_

_[tool: str_replace_edit]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Final picks section added with three artboards:

- **Item · final** — C base with left accent strip, A's left-bordered mod tiles for Honed / Vampiric / Iron Core, and A's pill-style EQUIPPED badge
- **Skill · cooldown pill** — C base with the smaller bolded total row; cooldown shown as a horizontal progress pill that fills toward "READY"
- **Skill · cooldown pips** — same skill design but with B's 8-pip vertical-bar strip; each pip fills bottom-up smoothly (not just on/off, so the motion reads continuously)

Both cooldown variants animate the same skill on a loop: 2.0s cooldown → ~1.1s "READY" hold → reset. Watch them side-by-side and tell me which feels right.

## User

Verifier agent check completed

## Assistant

Verifier confirmed both cooldown indicators animate as designed. Ready when you've picked between pill and pips.

## User

Awesome, I'd like to go with the cooldown pill here.

## Assistant

Locked in: cooldown pill. Tooltips are done — ready for the Fight screen when you are.
