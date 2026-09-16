# Архитектура offline-first MVP

## Основное решение

Release-клиент **не требует собственного backend, сервера или базы данных**. Gameplay, Campaign, Daily, Training, friend challenge, прогресс, экономика и сохранения работают локально на устройстве.

Внешние сервисы подключаются через интерфейсы/адаптеры: RuStore Pay/Review/Update, production-gated Install Referrer/Remote Config и рекламный provider. Их недоступность не должна ломать базовый игровой цикл.

Папка `server/` сохранена только как необязательный фундамент будущего online-mode: глобального leaderboard, authoritative server-time, server-side anti-cheat и усиленной проверки покупок.

## Правило зависимостей

Чистые игровые правила не знают об Android, RuStore, рекламе, HTTP или MonoBehaviour.

Упрощённо:

`Core <- Gameplay <- Daily/Social/Economy/Monetization`

`Services <- Platform adapters`

`Presentation -> gameplay/application services + interfaces`

`Network` в release-клиенте остаётся compatibility/provider facade. При пустом `OptionalBackendBaseUrl` `UnityGameApi` работает через локальный `OfflineGameApi`; собственный HTTP не входит в обязательный runtime path.

## Модули

| Модуль | Ответственность |
|---|---|
| Core | fixed-point, RNG, SaveData v13, migrations/repair, settings/reminder policy |
| Gameplay | RouteGenerator, ScoreCalculator, Campaign catalog/progression rules, DailyChallengeFactory, runtime tuning |
| Daily | DailySessionService, streak, review policy, per-Daily best |
| Social | OfflineGameApi, L1/L2/L3 challenge codec, referral parser, DuelSessionService |
| Economy | StoreService, campaign/local rewards, cosmetics, entitlements, idempotent grants |
| Monetization | rewarded/interstitial policies и provider boundary |
| Analytics | bounded device-only event journal |
| Network | compatibility/provider facade; HTTP только для optional future mode |
| Platform/RuStore | Pay, Review, Update и reflection-isolated Install Referrer/Remote Config adapters |
| Platform/Android | notification permission и local Daily scheduler |
| Presentation | Unity UI/input/lifecycle, Campaign/Training/Home/meta/result coordinators, safe area и runtime wiring |
| Editor | production preflight, portrait Game View, local QA/reset tools |

## Детерминизм маршрута

1. Seed и `generatorVersion` полностью определяют reference geometry.
2. `generatorVersion` входит в RNG-state.
3. RNG и генератор используют целочисленные/fixed-point операции (`0..1_000_000`).
4. Bezier samples вычисляются с фиксированным количеством точек.
5. Один `seed + generatorVersion` должен создавать ту же геометрию на разных устройствах.
6. При изменении алгоритма повышается `generatorVersion`; старую стратегию нельзя тихо переписывать.

Remote Config не участвует в геометрии. Он может менять только session/presentation параметры, например display time и число маршрутов Daily.

## Score

Score полностью локальный и не зависит от SDK или сети:

- 70% — среднее расстояние пользовательской траектории до reference polyline;
- 18% — завершённость;
- 8% — попадание в END;
- 4% — штраф за грубые отклонения.

При прохождении менее 50% маршрута применяется дополнительный penalty. Итог округляется до 0,1%. `OfflineGameApi` повторно рассчитывает результат из replay вместо доверия UI score.

## Campaign

Campaign — полностью локальный pure-C# контентный слой:

- 60 уровней;
- 6 глав по 10 уровней;
- фиксированные seed, generatorVersion, difficulty и display time;
- 60% / 80% / 95% дают 1 / 2 / 3 звезды;
- следующий уровень открывается после первой звезды;
- лучший score и максимум звёзд не ухудшаются повторным прохождением;
- новые звёзды дают монеты один раз;
- завершение главы даёт одноразовый bonus hint;
- `30 coins -> 1 hint` — локальный sink;
- уровень 60 завершает кампанию без выхода за каталог.

Presentation переиспользует drawing/scoring loop `GameBootstrap`, но `CampaignRuntimeCoordinator` хранит отдельную identity уровня и result/share flow. PNG share-card кампании подписывается `КАМПАНИЯ / УРОВЕНЬ N`.

## Daily

Локальный Daily формирует `challengeId` из UTC-даты, deterministic seed из даты + generatorVersion, routeCount 1–3 и exact display-time profile. `DailyCacheData` сохраняет routeCount и времена Easy/Medium/Hard, поэтому уже созданное испытание не меняется при смене tuning.

Ограничение offline-варианта: системную дату устройства можно изменить вручную, поэтому строгой server-time anti-cheat защиты нет.

## Training

Training бесконечный и локальный. Перед запуском пользователь выбирает **Лёгкая / Средняя / Сложная / Случайная**. Difficulty selection не меняет Campaign/Daily state.

## Friend challenge / viral loop

Вызов другу не требует lookup в нашей БД. Self-contained **L3 token** хранит дату, seed, generatorVersion, routeCount, exact display times и score отправителя.

Share содержит:

1. `nesbeisya://challenge/<token>` для установленной игры;
2. официальный RuStore install URL `https://www.rustore.ru/catalog/app/<package>?referrerId=<token>`.

L1/L2 decoder сохранён для обратной совместимости. L3 фиксирует routeCount и timing, поэтому изменение Remote Config на устройстве друга не меняет условия Duel.

## Сохранения и единый runtime state

Основной прогресс хранится JSON-файлом через `JsonFileSaveRepository`, не только в PlayerPrefs. Текущий формат — **SaveData v13**.

`JsonFileSaveRepository` держит process-wide shared `SaveData` для одного save path, поэтому Campaign, Daily, магазин, rewarded, settings, cosmetics и hints работают с одной актуальной object model. На `SubsystemRegistration` shared cache очищается, чтобы новая runtime/Editor Play session перечитывала persisted file.

Запись failure-safe: сначала создаётся temp, затем known-good backup основного файла, после чего primary заменяется. Если replacement не удался, repository пытается восстановить primary из backup; temp удаляется best-effort.

`SaveMigrator` выполняет post-load repair даже для уже текущего v13:

- отрицательные currency/counters → 0;
- score clamp `0..100`, NaN/Infinity → 0;
- inventory/entitlements/purchase IDs dedupe + trim;
- Daily cache routeCount/timing/version normalization;
- duplicate Daily bests схлопываются по max score, история ограничена 60;
- invalid/duplicate level progress удаляется/объединяется, stars clamp 0..3;
- HighestUnlockedLevel восстанавливается из заработанных звёзд;
- obsolete `PendingAttempts` всегда очищается.

Без сети работают Tutorial, Campaign, Training, Daily, уже полученный Duel, settings, statistics, cosmetics и local economy.

## Покупки и экономика

Gameplay зависит от `IPaymentService`, а не от RuStore Pay напрямую.

- каталог и price label приходят из RuStore Pay SDK;
- consumable выдаётся после completed purchase result с непустым purchaseId;
- `ProcessedPurchaseIds` предотвращает повторную локальную выдачу;
- non-consumable ownership подтверждается/восстанавливается через `GetPurchases`;
- `remove_ads`/`starter_pack` подавляют interstitial;
- Neon/Retro/Gold inventory доступен через cosmetic selection;
- `hints_10` увеличивает hint balance.

Без server-side verification защита от модифицированного клиента слабее — осознанный компромисс локального MVP.

## Реклама

Gameplay знает только `IAdService`/policy classes. Rewarded на Home выдаёт `+1 hint` только после reward callback. Interstitial допускается только между сессиями и не показывается во время route display/drawing/result/share/store/purchase. `remove_ads` и `starter_pack` отключают interstitial.

Production требует официальный Yandex Mobile Ads Unity package, реальные block IDs и device test.

## Analytics

`LocalAnalyticsService` пишет максимум 500 событий в device-only JSON и ничего не загружает на наш сервер. Campaign start/complete, local coin spend, Daily, rounds, share, ads, store и purchases используют зарегистрированные event names.

## Remote Config

`RuStoreRemoteConfigService` reflection-isolated и имеет cache/default fallback. Текущий production target — **Remote Config Unity 10.5.1**. Editor manifest намеренно не содержит проблемную package integration после Unity 6.3 compile regression; production preflight требует реально загруженный `RuStoreRemoteConfigClient` и production AppId.

Основной gameplay не зависит от наличия SDK.

## Install Referrer

`RuStoreInstallReferrerService` reflection-isolated. Production target — **Install Referrer Unity 10.6.1**. Перед release обязателен официальный package + install → first launch → token recovery test на Android. `referrerId` сохраняется сразу после успешного one-shot чтения.

## Notifications

Daily reminder планируется локально через Unity Mobile Notifications, без RuStore Push и собственного push-server. На Android 13+ `POST_NOTIFICATIONS` запрашивается только после понятного value prompt.

## Review / Update

Review и Update запускаются только из safe Home state. Review не просит конкретное число звёзд; Update использует RuStore flow и version policy.

## UI / mobile lifecycle

Runtime UI programmatic, с единым visual language: dark surface, cyan/violet accents, rounded buttons/cards, disabled states и safe-area correction.

Home содержит Campaign / Daily / Training плюс statistics/store. Campaign/Training/Meta overlays имеют публичные state/close contracts для Android Back, поэтому mobile coordinator не читает их private state через reflection. Pause во время активного gesture безопасно отменяет незавершённый раунд.

Editor QA menu позволяет открыть `persistentDataPath` и сбросить local save/backup/analytics/crash/config cache только вне Play Mode.

## Production release preflight

`ProductionReleaseValidator` блокирует non-development Android build при нарушении production-инвариантов:

- package name не должен оставаться `.dev`;
- ProjectConfigurator не перезаписывает вручную настроенный production package;
- `UnityPlayerActivity`;
- min API 25 и текущий target API baseline;
- Pay 11.1.0 / Update 10.5.1 / Review 10.5.1;
- реально загруженные Install Referrer и Remote Config Unity client types;
- Install Referrer target 10.6.1 / Remote Config target 10.5.1;
- пустой developer backend URL;
- Remote Config production App ID;
- Yandex production define и реальные ad IDs;
- только необходимые Android permissions;
- app-scoped FileProvider для PNG share;
- custom keystore/alias;
- AAB;
- IL2CPP + ARM64;
- положительный versionCode.

Credentials, keystore и build artifacts защищены `.gitignore`.

## CI

На PR выполняются **6 независимых проверок**:

- `pure-csharp-tests`;
- `backend-tests` — только optional future backend;
- `offline-mode-guard`;
- `product-flow-guard`;
- `rustore-dependency-guard`;
- `unity-static-validation`.

Guards защищают отсутствие обязательного собственного backend, L3 fairness, Campaign/economy/Training/shared-save invariants, Android manifest/share contracts, production preflight и текущие RuStore targets.

Static/CI проверки не заменяют финальный Unity compile, signed AAB и физический Android/RuStore smoke-test.
