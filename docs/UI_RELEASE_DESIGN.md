# Release UI design contract — «НЕ СБЕЙСЯ!»

Approved visual direction is based on the release mockups supplied on 2026-09-19. Runtime UI must feel like one product, not a set of independently styled debug panels.

## Visual language

- Portrait, one-handed layout.
- Deep navy / dark blue atmospheric background.
- Cyan → blue → violet primary accents with restrained glow.
- Rounded glass-like cards, high contrast, no flat developer-grey panels.
- Gold for reward/progression, mint for success, coral/red for errors.
- Large, obvious primary CTA; secondary actions use outlined/dark surfaces.
- 8 px spacing rhythm; 24 px conceptual outer margins; large blocks separated by 16–24 px.
- Use the shared `ReleaseUiComponents` system before adding one-off visual code.

Core palette:
- cyan `#00E5FF`
- blue `#38B2F6`
- violet `#8B5CF6`
- pink `#FF4D9E`
- gold `#FACC15`
- success mint `#42F5C5`
- danger `#FF5C7A`

## Screen hierarchy

### Home
Logo/tagline and coin balance → Daily hero → Campaign preview → four live local stats → Training / Statistics / Store cards → opt-in rewarded action.

Home must display real save values only. Do not advertise a fixed Daily coin reward unless economy code actually grants it.

### Daily
Home CTA opens a dedicated Daily intro. It explains the shared challenge and goal before gameplay. Copy must remain route-count agnostic because Daily supports configurable 1–3 routes.

### Gameplay
The gameplay loop stays: show route → automatic hide/countdown → one continuous memory gesture. Presentation uses a compact release header, real route board, cyan start, gold finish and an instruction card. Do not change scoring/input semantics for visual parity.

### Result
Large score/celebration → visible route comparison → real scorer component tiles → release action sheet. Stat values come from `ScoreBreakdown`; never use decorative sample values from mockups.

### Training
Three explicit difficulty choices with selected state and one primary “start training” CTA. Display-time labels must match actual runtime values.

### Campaign
Atmospheric chapter header + progress + 10-level path. Completed/current/locked states must map to real local Campaign progress.

### Statistics
Use only persisted local metrics. Do not invent charts/history that SaveData does not contain.

### Store
Use real RuStore catalog products and `PriceLabel` values. Current MVP sections are premium, skins and hints. Do not add a crystal economy or coin packs unless Economy gains those products first.

### Settings
Use real supported settings only: sound, haptics, notification state, local progress status, app version and purchase restore. Unsupported language/account/cloud controls are not decorative placeholders.

### Ranking
No fake global leaderboard. A global ranking screen stays out of the offline-first release until a real trusted ranking source exists.

## Ownership rules

- `HomeDashboardCoordinator` owns only unobstructed plain Home.
- `DailyIntroCoordinator`, Training, Campaign, Meta and referral surfaces are mutually exclusive Home overlays.
- `GameplayHudCoordinator` owns only Showing/Drawing.
- `ResultEnhancementCoordinator` owns Result chrome while the underlying route comparison remains visible.
- Ads, review/update UI and notification prompts may launch only when no release overlay owns Home.
- Android Back closes the topmost release overlay before navigating or quitting.

## Implementation rule

The approved mockups are visual reference, not permission to fake data or change product mechanics. When a mockup conflicts with current gameplay/economy/offline architecture, preserve the architecture and reproduce the visual hierarchy with truthful live data.
