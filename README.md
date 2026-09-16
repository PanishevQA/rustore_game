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

Сервер не нужен. L3 challenge token содержит date, seed, generatorVersion, routeCount, exact display-time profile и score отправителя. L1/L2 остаются backward-compatible. Share содержит `nesbeisya://challenge/<token>` и официальный RuStore install URL `https://www.rustore.ru/catalog/app/<package>?referrerId=<token>`.

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

Production требует официальный Yandex Unity plugin, define `YANDEX_MOBILE_ADS` и реальные block IDs.

## Android / RuStore baseline

Проект зафиксирован на Unity `6000.3.24f1`, portrait, `UnityPlayerActivity`, IL2CPP/ARM64 release baseline.

Последняя сверка RuStore targets — 2026-09-16:

- Pay Unity `11.1.0`;
- Install Referrer Unity `10.6.1`;
- Update Unity `10.5.1`;
- Review Unity `10.5.1`;
- Remote Config Unity `10.5.1`;
- GameCenter Unity `10.5.2` — optional, не critical path;
- RuStore Push не используется: Daily reminders локальные;
- target API baseline 34 / highest installed;
- minSdk проекта 25, потому что Unity 6.3 уже не поддерживает API 24 как рабочий baseline.

Install Referrer и Remote Config adapters SDK-isolated через reflection/fallback. Их packages временно отсутствуют в Editor manifest, потому что конкретная package integration дала compile errors на Unity 6.3. Перед production они являются отдельным Android integration gate: официальный package → resolve/compile → physical-device test. Production preflight требует реально загруженные Unity client types, а не только наличие adapter-файлов.

## Remote Config

Gameplay не зависит напрямую от SDK. `RemoteGameplayTuningCoordinator` переводит валидированные параметры в pure C# runtime tuning.

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

Текущий UI — функциональный финальный кандидат для общей приёмки. Финальная авторская типографика/иконки/иллюстрации могут быть заменены после визуальной приёмки без изменения gameplay architecture.

## QA в Unity Editor

Меню `Tools → НЕ СБЕЙСЯ! → QA` содержит:

- `Open Local Data Folder`;
- `Reset Local Progress and QA Logs` — доступно только вне Play Mode.

Это позволяет проводить полный clean-state regression без ручного поиска `persistentDataPath`.

## Быстрый запуск

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть `UnityProject`.
3. Дождаться UPM resolve/compile.
4. Открыть `Assets/Scenes/Main.unity` (ProjectConfigurator создаст её при необходимости).
5. Нажать Play.

Production-сборка дополнительно требует точный package name из RuStore Console, production signing, PayClient settings, официальный Install Referrer/Remote Config integration, Yandex package/IDs и device tests. Собственный backend URL задавать не требуется.

## CI

На PR запускаются шесть checks:

- `pure-csharp-tests` — generator/scoring/Daily/Duel/Economy/Campaign/save repair rules;
- `backend-tests` — только optional future backend;
- `offline-mode-guard` — offline/L3/config/store invariants;
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
- `UnityProject/Assets/Game/Platform/RuStore` — RuStore adapters.
- `UnityProject/Assets/Game/Platform/Android` — notifications/Android helpers.
- `server` — optional future online mode, not required by release client.
- `docs` — architecture, SDK matrix, project status and production checklist.

Перед production обязательно пройти `docs/RUSTORE_RELEASE_CHECKLIST.md`.
