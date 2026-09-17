# Состояние проекта — 2026-09-17

> Проект не оценивается одним процентом «готовности»: repo-side функционал, визуальная приёмка и внешняя Android/RuStore интеграция проверяются разными способами.

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
- **6 глав по 10 уровней**;
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
- campaign statistics встроены непосредственно в Meta UI, без отдельного reflection-патча.

### Daily / Training / Social

- local UTC Daily Challenge, 1–3 маршрута;
- exact display-time cache/profile;
- streak, personal best и per-Daily best;
- Training с явным выбором **Easy / Medium / Hard / Random**;
- friend Duel;
- текущий self-contained **L4 challenge token**: date/seed/version/routeCount/display-times/score + 16-bit checksum;
- backward compatibility L1/L2/L3;
- legacy L3 reshare сохраняет identity и выпускает новый L4;
- битый L4 checksum отклоняется и codec, и `ReferralLinkParser`;
- rematch;
- text share + PNG result card;
- deeplink/referrer adapters без нашего backend;
- RuStore install URL использует официальный формат `https://www.rustore.ru/catalog/app/<package>?referrerId=<token>`.

### Store / monetization

- RuStore Pay adapter, SDK catalog/prices, purchase/restore flow;
- `remove_ads` реально подавляет interstitial;
- `hints_10` выдаёт расходуемые hints;
- Neon/Retro/Gold cosmetics реально выбираются и влияют на Drawing visuals;
- starter pack выдаёт свои non-consumables и отключает interstitial;
- rewarded — только opt-in, выдаёт +1 hint после reward callback;
- rewarded Home surface отделён от основных CTA;
- interstitial запрещён во время active round/result/share/store/purchase и ограничен policy/cooldown.

## Save / reliability

- SaveData **v13** + migrations;
- current-version post-load repair нормализует отрицательные currency/counters, invalid scores, дубли purchases/Daily bests/level progress и invalid campaign entries;
- obsolete backend `PendingAttempts` очищается даже в current-version save;
- все runtime writers используют одну shared `SaveData` identity на save path;
- shared cache сбрасывается на `SubsystemRegistration`, поэтому Editor Play без Domain Reload перечитывает persisted file;
- запись использует temp + known-good backup; при неудачной замене primary предпринимается recovery из backup;
- bounded local analytics/crash log, без нашего сервера;
- Editor menu `Tools → НЕ СБЕЙСЯ! → QA` позволяет открыть data folder и безопасно сбросить local progress/logs вне Play Mode.

## Offline-first architecture

Release-клиент не требует developer-operated backend или собственной БД. Campaign, Daily, Training, progress, economy, settings и cosmetics работают локально. `server/` остаётся только как optional foundation для возможного будущего online leaderboard/authoritative mode.

Remote Config runtime также не зависит от нашего backend: один `RuStoreRemoteConfigRuntime` provider/cache используется gameplay tuning, advertising и compatibility facade; при отсутствии рабочего RuStore runtime используются cache/defaults.

## UI state

Собран единый portrait presentation-layer: dark surface, cyan/violet accents, rounded CTA/cards, Home hero, campaign selector, Training selector, meta/settings/store/result overlays и safe-area обработка. Устранены конфликтующие layout/theme owners и Home flicker.

Meta/Training/Campaign overlays имеют публичные open/close state contracts для mobile Back вместо чтения их private state через reflection.

Это **functional final UI candidate** для общей приёмки. Авторская типографика/иллюстрации/иконки могут быть заменены после общей визуальной приёмки без изменения gameplay architecture.

## Автоматические проверки

В PR шесть checks:

1. `pure-csharp-tests`;
2. `backend-tests` — только optional future backend;
3. `offline-mode-guard`;
4. `rustore-dependency-guard`;
5. `unity-static-validation`;
6. `product-flow-guard`.

Pure suite покрывает deterministic routes, scoring, Daily, save migrations/repair, campaign 60-level catalog, star thresholds, unlocks, reward idempotency, coin→hint exchange и L4 checksum/legacy challenge compatibility. Guards защищают offline architecture, viral token fairness/checksum, Android manifest/share contracts, RuStore targets, shared save и видимые Campaign/Training flows.

## RuStore / Android release target

Последняя сверка RuStore targets на 2026-09-17:

- Pay Unity: `11.1.0`;
- Install Referrer Unity: `10.6.1`;
- Update Unity: `10.5.1`;
- Review Unity: `10.5.1`;
- Remote Config Unity: **`10.5.1`**;
- GameCenter Unity: `10.5.2` — optional;
- RuStore Push не используется: Daily reminder реализован локально;
- targetSdk baseline: `34` / highest installed;
- minSdk проекта: `25` из-за Unity 6000.3 baseline.

Required RuStore Unity packages теперь закреплены в `UnityProject/Packages/manifest.json`: `ru.rustore.pay` `11.1.0`, `ru.rustore.installreferrer` `10.6.1`, `ru.rustore.remoteconfig` `10.5.1`, `ru.rustore.update` `10.5.1` и `ru.rustore.review` `10.5.1`. Install Referrer/Remote Config adapters по-прежнему изолированы от gameplay и имеют safe fallback, а production preflight требует реально загруженные official Unity client types перед non-development AAB.

## Что нельзя честно завершить только изменениями в GitHub

Это не недостающий gameplay, а внешняя финальная приёмка/конфигурация:

- production package name из RuStore Console;
- production keystore/key alias;
- реальные PayClient/RuStore Console параметры;
- UPM resolve/Unity compile официальных RuStore packages на release-машине;
- Install Referrer 10.6.1 physical-device smoke test;
- Remote Config 10.5.1 AppId и physical-device fallback test;
- официальный Yandex Mobile Ads Unity plugin + реальные block IDs;
- Pay success/cancel/error/restore через реальный RuStore;
- deeplink + Install Referrer после реальной установки;
- Review / Update на устройстве с RuStore;
- Android 13 notification permission/local notification;
- PNG FileProvider/system share на устройстве;
- signed AAB;
- multi-device DPI/safe-area/lifecycle regression.

PR #1 остаётся Draft до общей финальной приёмки. Промежуточный ручной тест не используется как условие продолжения разработки.
