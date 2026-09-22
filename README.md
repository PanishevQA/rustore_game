# НЕ СБЕЙСЯ!

Мобильная игра для Android / RuStore: игрок несколько секунд запоминает маршрут, затем одним движением повторяет его по памяти и получает точность 0–100%.

## Сервер не обязателен

Release-клиент — **offline-first**. Для Campaign, Daily, Training, прогресса, локальной экономики и friend challenge не требуется VPS, собственная база данных, домен или постоянно работающий backend.

На устройстве работают локально:

- Tutorial;
- Campaign — 60 уровней / 6 глав;
- Daily Challenge;
- Training с выбором сложности;
- генерация маршрутов и score recalculation;
- level stars/unlocks, streak, personal best и статистика;
- coins/hints/cosmetics/settings;
- friend Duel с self-contained challenge token;
- versioned SaveData v13;
- bounded local analytics/crash log;
- локальные Daily reminders.

Опциональный `server/` оставлен только как foundation для возможного будущего online leaderboard/server-time/stronger anti-cheat. Текущая игра от него не зависит.

## Игровые режимы

### Campaign

- 60 детерминированных уровней;
- 6 глав по 10 уровней;
- фиксированные `seed + generatorVersion + difficulty + displayTime`;
- `60% = ★`, `80% = ★★`, `95% = ★★★`;
- следующий уровень открывается с первой звездой;
- best score и stars не ухудшаются повторным прохождением;
- новая звезда даёт 5 coins;
- первое прохождение финала главы даёт +1 hint;
- 30 coins можно локально обменять на +1 hint;
- финал 60-го уровня показывает завершение кампании;
- chapter/level selector показывает locks, stars и рекорд.

### Daily

Daily строится из UTC-даты и `generatorVersion`. Remote Config может менять время показа и число маршрутов 1–3, не меняя геометрию заданного `seed + generatorVersion`. Условия уже созданного challenge фиксируются в cache/token.

Ограничение полностью локального режима: пользователь с модифицированным устройством может изменить системное время. Без внешнего authoritative time абсолютная защита от этого невозможна.

### Training

Бесконечная локальная тренировка с явным выбором Easy / Medium / Hard / Random.

### Friend Duel

Сервер не нужен. Текущий **L4 challenge token** содержит date, seed, generatorVersion, routeCount, exact display-time profile и score отправителя, а также 16-bit checksum для обнаружения повреждения/подмены payload. Legacy L1/L2/L3 остаются backward-compatible; при reshare старого L3 challenge identity сохраняется, а новая ссылка кодируется как L4. Share содержит `nesbeisya://challenge/<token>` и официальный RuStore install URL `https://www.rustore.ru/catalog/app/<package>?referrerId=<token>`.

## Результат и прогресс

- reference + player trajectory;
- score до 0,1%;
- Bronze/Silver/Gold/Perfect medals;
- Perfect pulse;
- per-Daily record;
- Campaign best/stars;
- PNG result cards;
- Campaign card подписана как Campaign/Level, а не внутренний Training reuse;
- Duel rematch.

## Save / локальная экономика

`SaveData` — v13 с миграциями и post-load repair. Основной прогресс не хранится целиком в PlayerPrefs.

`JsonFileSaveRepository` использует одну process-wide runtime identity `SaveData`, поэтому independently created presentation/store/reward services не могут затереть более свежие coins/hints/purchases/settings старой копией. Shared cache сбрасывается при новом runtime, а file replacement использует temp + known-good backup с recovery при ошибке.

Текущая v13 normalization также чинит отрицательные counters/currency, invalid scores, дубли purchase IDs, Daily bests и level progress, а obsolete backend pending queue удаляется при загрузке.

Campaign coins — только внутриигровая локальная награда. Они не заменяют реальные RuStore prices и расходуются только на hints.

## Store / ads

RuStore Pay находится за `IPaymentService`/`StoreService`. UI показывает цену только из SDK catalog.

MVP products:

- `remove_ads`;
- `starter_pack`;
- `skin_neon`;
- `skin_retro`;
- `hints_10`.

Non-consumables восстанавливаются через RuStore ownership. Consumable grant защищён локальной idempotency по `purchaseId`.

Yandex ads находятся за `IAdService`:

- rewarded — opt-in, +1 hint только после reward callback;
- interstitial — только в safe Home между сессиями;
- active route/result/share/store/purchase не прерываются;
- `remove_ads`/`starter_pack` подавляют interstitial;
- frequency/cooldown policy отделена от gameplay.

Yandex Mobile Ads Unity `8.4.0` закреплён в `Packages/manifest.json`; `Game.Monetization.asmdef` автоматически включает adapter define для проверенной 8.4.x линии. Production build сам запускает fail-closed EDM4U Force Resolve; снаружи остаются реальные `R-M-...` block IDs и signed-device ad smoke test.

## Android / RuStore baseline

Проект зафиксирован на Unity `6000.3.24f1`, portrait, `UnityPlayerActivity`, IL2CPP/ARM64 release baseline.

Последняя сверка RuStore targets — 2026-09-22:

- Pay Unity `11.1.0`;
- Install Referrer Unity `10.6.1`;
- Update Unity `10.5.1`;
- Review Unity `10.5.1`;
- Remote Config Unity `10.5.1`;
- GameCenter Unity `10.5.2` — optional, не critical path;
- RuStore Push не используется: Daily reminders локальные;
- target API baseline 34 / highest installed;
- minSdk проекта 25, потому что Unity 6.3 уже не поддерживает API 24 как рабочий baseline.

Compile-safe Editor baseline подключает через официальный registry `https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/` Pay `11.1.0`, Update `10.5.1` и Review `10.5.1`. Install Referrer `10.6.1` и Remote Config `10.5.1` остаются актуальными production targets, но временно исключены из `Packages/manifest.json` до успешных отдельных Unity 6000.3.24f1 package probes. Их adapters/cache/default остаются, а production preflight блокирует релиз, пока реальные SDK client types не загружены. `ru.rustore.core` напрямую не pin-ится.

## Remote Config

Gameplay не зависит напрямую от SDK. `RuStoreRemoteConfigRuntime` владеет одним shared provider/cache для runtime. `RemoteGameplayTuningCoordinator` читает его через `IRemoteConfigService`; реклама и platform policy используют тот же snapshot. Provider сбрасывается при `SubsystemRegistration`, поэтому Unity Play без Domain Reload не переиспользует stale static state.

Ключи:

- `route_display_time_easy_ms`, `route_display_time_medium_ms`, `route_display_time_hard_ms`;
- `daily_route_count` (1..3);
- `rewarded_enabled`, `interstitial_enabled`, `interstitial_min_rounds`, `interstitial_cooldown_sec`;
- `review_min_sessions`;
- `local_daily_reminder_enabled`, `daily_reminder_hour`;
- `share_copy_variant`, `store_offer_variant`;
- `min_supported_version`, `recommended_version`.

Daily gameplay keys должны быть глобальными без audience A/B, иначе условия одного дня могут различаться между пользователями.

## Notifications

Daily reminder планируется через `com.unity.mobile.notifications` и не требует push/backend. На Android 13+ `POST_NOTIFICATIONS` запрашивается контекстно после value event; отказ не блокирует игру.

## UI / mobile lifecycle

- строго portrait;
- Editor Game View preset 1080×1920;
- safe-area handling;
- Android Back для Training/Campaign/Meta overlays и active round;
- pause/background policy;
- единая dark/cyan/violet presentation theme;
- Home/Campaign/Training/Meta/Result surfaces;
- Campaign/Daily статистика находится прямо в Meta UI;
- Sound/Haptics имеют реальный feedback, а не декоративные toggles.

UI собран как единый release-style portrait layer: карточный Home dashboard, отдельный Daily CTA, campaign progress, high-fidelity Training selector, chapter/level browser, профиль/статистика/настройки/магазин, gameplay HUD, grid/glow игрового поля, единый result header/detail, брендированная PNG share-card и отдельный incoming friend-challenge экран. Устаревшие конкурирующие theme/Home pollers удалены, поэтому у каждого release surface один визуальный владелец.

## QA в Unity Editor

Меню `Tools → НЕ СБЕЙСЯ! → QA` содержит:

- `Open Local Data Folder`;
- `Reset Local Progress and QA Logs` — доступно только вне Play Mode.

Это позволяет проводить полный clean-state regression без ручного поиска `persistentDataPath`.

## Production identifiers

Зафиксированы production-идентификаторы:
- Android package: `ru.release.nesbeisya`;
- RuStore Console application ID / Pay: `2063758837`;
- RuStore Remote Config AppId: `4e0feafb-1ce7-4b71-966b-0122938b282a`;
- RuStore Pay deeplink scheme: `nesbeisyapay`;
- Yandex rewarded: `R-M-20071218-1`;
- Yandex interstitial: `R-M-20071218-2`;
- signing alias: `nesbeysya` (keystore file/passwords remain local and are never committed).

## Release-candidate проверка одной командой

На Windows с установленным через Unity Hub Editor закройте открытый Unity-проект и из корня репозитория запустите:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_release_candidate_checks.ps1
```

Скрипт сам читает версию Editor из `UnityProject/ProjectSettings/ProjectVersion.txt`, находит её в Unity Hub, запускает Unity compile + EditMode tests, затем выполняет Android release preparation и `Release Readiness Report`. Логи и результаты сохраняются в `artifacts/release-candidate`.

Если Unity установлен нестандартно, передайте `-UnityExe "C:\path\to\Unity.exe"` или задайте переменную `UNITY_EXE`.

## Быстрый запуск

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть `UnityProject`.
3. Дождаться UPM resolve/compile.
4. Открыть `Assets/Scenes/Main.unity` (ProjectConfigurator создаст её при необходимости).
5. Нажать Play.

Production-сборка дополнительно требует точный package name из RuStore Console, production signing, PayClient/Remote Config AppId настройки, реальные Yandex package/IDs и physical-device tests RuStore flows. Собственный backend URL задавать не требуется.

## CI

На PR запускаются шесть checks:

- `pure-csharp-tests` — generator/scoring/Daily/Duel/Economy/Campaign/save repair rules;
- `backend-tests` — только optional future backend;
- `offline-mode-guard` — offline/L4 checksum/config/store invariants и legacy L1–L3 compatibility;
- `rustore-dependency-guard` — RuStore targets/registry isolation;
- `unity-static-validation` — manifest/asmdef/project/release-preflight structure;
- `product-flow-guard` — Campaign, rewards, coin→hint, shared save, Training/overlay contracts.

Статические проверки не заменяют реальный Unity compile, signed AAB и physical-device RuStore tests.

## Структура

- `UnityProject/Assets/Game/Core` — save/model/fixed-point/settings.
- `UnityProject/Assets/Game/Gameplay` — route generator, scoring, Campaign catalog/progression.
- `UnityProject/Assets/Game/Daily` — Daily/streak/review policies.
- `UnityProject/Assets/Game/Social` — offline challenge codec, Duel/referral.
- `UnityProject/Assets/Game/Economy` — RuStore product grants, cosmetics, local reward economy.
- `UnityProject/Assets/Game/Monetization` — ad policies + Yandex adapter.
- `UnityProject/Assets/Game/Services` — external-service interfaces.
- `UnityProject/Assets/Game/Presentation` — runtime UI/input/coordinators.
- `UnityProject/Assets/Game/Platform/RuStore` — RuStore adapters and shared Remote Config runtime.
- `UnityProject/Assets/Game/Platform/Android` — notifications/Android helpers.
- `server` — optional future online mode, not required by release client.
- `docs` — architecture, SDK matrix, project status and production checklist.

Перед production обязательно пройти `docs/RUSTORE_RELEASE_CHECKLIST.md`.