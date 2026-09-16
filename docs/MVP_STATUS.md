# Состояние проекта — 2026-09-16

> Проект больше не оценивается одним процентом «готовности». Техническое ядро, видимый игровой продукт и внешняя Android/RuStore интеграция имеют разную степень проверяемости.

## Видимый игровой продукт — реализовано в коде

### Основной gameplay

- детерминированная генерация маршрутов `seed + generatorVersion`;
- запись координат и timestamps;
- score `0–100%` с точностью до `0,1%`;
- tutorial первого запуска;
- reference/player trajectory на результате;
- medals + Perfect effect;
- hints и selectable cosmetics;
- Sound/Haptics settings;
- portrait layout, safe area, Android Back и pause/resume policy.

### Campaign

- **60 локальных уровней**;
- **6 глав по 10 уровней**: «Первые шаги», «Ритм», «Повороты», «Давление», «Эксперт», «Без ошибок»;
- фиксированные seed/version/difficulty/display-time профили;
- постепенный рост сложности;
- `60% = ★`, `80% = ★★`, `95% = ★★★`;
- unlock следующего уровня только после минимум одной звезды;
- лучший score/максимум stars не ухудшаются повторным прохождением;
- chapter/level selector с locks, stars и best score;
- финал 60-го уровня показывает завершение кампании и общий star progress;
- Campaign PNG result card подписывается как Campaign/Level, а не internal Training mode.

### Campaign progression / economy

- каждая **новая** звезда даёт `5 coins`;
- улучшение 1→3 stars выдаёт только разницу, повторный результат нельзя фармить;
- первое прохождение финала каждой главы (10/20/.../60) даёт `+1 hint` один раз;
- `30 coins → +1 hint` — локальный обмен, не связанный с ценами RuStore;
- Home и Statistics показывают campaign progress/stars/resources;
- все presentation-сервисы используют одну runtime identity `SaveData`, поэтому purchase/reward/coin exchange не могут быть затёрты поздним сохранением старой копии.

### Daily / Training / Social

- local UTC Daily Challenge, 1–3 маршрута;
- exact display-time cache/profile;
- streak, personal best и per-Daily best;
- Training с явным выбором **Easy / Medium / Hard / Random**;
- friend Duel;
- self-contained L3 challenge token (date/seed/version/routeCount/display-times/score);
- backward compatibility L1/L2;
- rematch;
- text share + PNG result card;
- deeplink/referrer adapters без нашего backend.

### Store / monetization

- RuStore Pay adapter, SDK catalog/prices, purchase/restore flow;
- `remove_ads` реально подавляет interstitial;
- `hints_10` выдаёт расходуемые hints;
- Neon/Retro/Gold cosmetics реально выбираются и влияют на Drawing visuals;
- starter pack выдаёт свои non-consumables и отключает interstitial;
- rewarded — только opt-in, выдаёт +1 hint после reward callback;
- rewarded Home surface отделён от основных CTA;
- interstitial запрещён во время active round/result/share/store/purchase и ограничен policy/cooldown.

### Local state / analytics

- SaveData **v13** + migrations;
- JSON save + backup/temp write;
- bounded local analytics/logs, без нашего сервера;
- campaign `level_start/level_complete` входят в analytics allow-list;
- Daily/share/duel/hints/settings/rewarded/store/purchase events реализованы.

## Offline-first architecture

Release-клиент не требует developer-operated backend или собственной БД. Campaign, Daily, Training, progress, economy, settings и cosmetics работают локально. `server/` остаётся только как optional foundation для возможного будущего online leaderboard/authoritative mode.

## UI state

Собран единый portrait presentation-layer: dark surface, cyan/violet accents, rounded CTA/cards, Home hero, campaign selector, Training selector, meta/settings/store/result overlays и safe-area обработка. Устранены конфликтующие layout/theme owners и Home flicker.

Это **final functional UI candidate**, который будет оцениваться целиком после завершения кодового этапа. Авторская типографика/иллюстрации/иконки могут быть заменены после общей визуальной приёмки, но для этого не требуется менять gameplay architecture.

## Автоматические проверки

В PR теперь шесть checks:

1. `pure-csharp-tests`;
2. `backend-tests` — только optional future backend;
3. `offline-mode-guard`;
4. `rustore-dependency-guard`;
5. `unity-static-validation`;
6. `product-flow-guard` — Campaign/rewards/local economy/shared save/Training invariants.

Pure suite покрывает deterministic routes, scoring, Daily, save migrations, campaign 60-level catalog, star thresholds, unlocks, reward idempotency и coin→hint exchange.

## RuStore / Android release target

Последняя сверка официальной документации на 2026-09-16:

- Pay Unity target: `11.1.0`;
- Install Referrer Unity target: `10.6.1`;
- Update Unity: `10.5.1`;
- Review Unity: `10.5.1`;
- Remote Config adapter target: `10.5.0`;
- targetSdk baseline: `34` / highest installed;
- проект использует minSdk `25`, потому что Unity 6000.3 уже не поддерживает API 24.

Install Referrer и Remote Config UPM packages, которые дали compile errors внутри package source на Unity 6.3, **не маскируются как готовая интеграция**: reflection adapters/fallback сохранены, а официальный package integration вынесен в обязательный Android release gate.

## Что нельзя честно завершить только изменениями в GitHub

Это не недостающий gameplay, а внешняя финальная приёмка/конфигурация:

- production package name из RuStore Console;
- production keystore/key alias;
- реальные PayClient/RuStore Console параметры;
- официальные Install Referrer / Remote Config packages и physical-device smoke test;
- официальный Yandex Mobile Ads Unity plugin + реальные block IDs;
- Pay success/cancel/error/restore через реальный RuStore;
- deeplink + Install Referrer после реальной установки;
- Review / Update на устройстве с RuStore;
- Android 13 notification permission/local notification;
- PNG FileProvider/system share на устройстве;
- signed AAB;
- multi-device DPI/safe-area/lifecycle regression.

PR #1 остаётся Draft до этой общей финальной приёмки. Промежуточный ручной тест больше не используется как условие продолжения разработки.
