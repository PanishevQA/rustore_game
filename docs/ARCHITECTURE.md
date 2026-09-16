# Архитектура offline-first MVP

## Основное решение

Release-клиент **не требует собственного backend, сервера или базы данных**. Gameplay, Campaign, Daily, Training, friend challenge, прогресс, экономика и сохранения работают локально на устройстве.

Внешние сервисы подключаются только через адаптеры: RuStore Pay/Review/Update, опциональные Install Referrer/Remote Config и рекламный provider. Их отсутствие не должно ломать базовый игровой цикл.

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
| Core | fixed-point, RNG, SaveData v13, migrations, settings/reminder policy |
| Gameplay | RouteGenerator, ScoreCalculator, Campaign catalog/progression rules, DailyChallengeFactory, runtime tuning |
| Daily | DailySessionService, streak, review policy, per-Daily best |
| Social | OfflineGameApi, L1/L2/L3 challenge codec, referral parser, DuelSessionService |
| Economy | StoreService, campaign/local rewards, cosmetics, entitlements, idempotent grants |
| Monetization | rewarded/interstitial policies и provider boundary |
| Analytics | bounded device-only event journal |
| Network | compatibility/provider facade; HTTP только для optional future mode |
| Platform/RuStore | Pay, Review, Update и reflection-isolated optional Install Referrer/Remote Config adapters |
| Platform/Android | notification permission и local Daily scheduler |
| Presentation | Unity UI/input/lifecycle, Campaign/Training/Home/meta/result coordinators, safe area и runtime wiring |

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
- каждый уровень имеет фиксированные seed, generatorVersion, difficulty и display time;
- 60% / 80% / 95% дают 1 / 2 / 3 звезды;
- следующий уровень открывается после первой звезды;
- лучший score и максимум звёзд никогда не ухудшаются повторным прохождением;
- новые звёзды дают монеты только один раз;
- завершение главы даёт одноразовый bonus hint;
- монеты имеют локальный sink: `30 coins -> 1 hint`;
- уровень 60 завершает кампанию без выхода за каталог.

Presentation переиспользует проверенный drawing/scoring loop `GameBootstrap`, но `CampaignRuntimeCoordinator` хранит отдельную identity уровня и отдельный result/share flow. PNG share-card кампании подписывается `КАМПАНИЯ / УРОВЕНЬ N`, а не `Training`.

## Daily

Локальный Daily формирует:

- `challengeId` из UTC-даты;
- deterministic seed из даты + `generatorVersion`;
- routeCount 1–3;
- exact display-time profile текущего challenge.

`DailyCacheData` сохраняет routeCount и времена Easy/Medium/Hard, поэтому уже созданное испытание не меняется при смене tuning. Ограничение offline-варианта: системную дату устройства можно изменить вручную, поэтому строгой server-time anti-cheat защиты нет.

## Training

Training бесконечный и полностью локальный. Перед запуском пользователь выбирает **Лёгкая / Средняя / Сложная / Случайная**. Difficulty selection влияет только на генерацию тренировочного раунда и не меняет Campaign/Daily state.

## Friend challenge / viral loop

Вызов другу не требует lookup в нашей БД. Self-contained **L3 token** хранит дату, seed, generatorVersion, routeCount, exact display times и score отправителя.

Share содержит:

1. `nesbeisya://challenge/<token>` для установленной игры;
2. RuStore install URL с тем же token в `referrerId` для новой установки.

L1/L2 decoder сохранён для обратной совместимости. L3 фиксирует routeCount и timing, поэтому изменение Remote Config на устройстве друга не меняет условия Duel.

## Сохранения и единый runtime state

Основной прогресс хранится JSON-файлом через `JsonFileSaveRepository`, не только в PlayerPrefs. Текущий формат — **SaveData v13** с миграциями.

Чтобы разные runtime-сервисы не перезаписывали изменения устаревшими копиями, `JsonFileSaveRepository` держит process-wide shared `SaveData` для одного save path. Campaign, Daily, магазин, rewarded, settings, cosmetics и hints получают одну актуальную object model; запись по-прежнему идёт атомарно через temp + backup.

В v13 входят campaign progress, highest unlocked level, local resources, purchases/entitlements, Daily state, settings и остальные offline данные.

Без сети работают Tutorial, Campaign, Training, Daily, уже полученный Duel, settings, statistics, cosmetics и local economy.

## Покупки и экономика

Gameplay зависит от `IPaymentService`, а не от RuStore Pay напрямую.

Offline-first схема:

- каталог и price label приходят из RuStore Pay SDK;
- consumable выдаётся только после completed purchase result с непустым purchaseId;
- `ProcessedPurchaseIds` предотвращает повторную локальную выдачу;
- non-consumable ownership подтверждается/восстанавливается через `GetPurchases`;
- `remove_ads` и `starter_pack` реально подавляют interstitial;
- Neon/Retro/Gold inventory реально доступен через cosmetic selection;
- `hints_10` увеличивает расходуемый hint balance.

Без server-side verification защита от модифицированного клиента слабее — это осознанный компромисс полностью локального MVP.

## Реклама

Gameplay знает только `IAdService`/policy classes. Rewarded на Home выдаёт `+1 hint` только после reward callback. Interstitial допускается только между сессиями и не показывается во время route display/drawing/result/share/store/purchase. Entitlement `remove_ads` и `starter_pack` отключает interstitial.

Production требует официальный Yandex Mobile Ads Unity package, реальные block IDs и device test; adapter/policies уже изолированы от gameplay.

## Analytics

`LocalAnalyticsService` пишет максимум 500 событий в device-only JSON и ничего не загружает на наш сервер. Campaign start/complete, local coin spend, Daily, rounds, share, ads, store и purchases используют зарегистрированные event names. В будущем сервис можно заменить другим adapter без изменения gameplay.

## Remote Config

`RuStoreRemoteConfigService` reflection-isolated и имеет cache/default fallback. На текущем Editor manifest проблемный Remote Config package намеренно не установлен после Unity 6.3 compile regression; release integration должна быть повторно выполнена официальным package/tarball и проверена на Android.

Текущий target в matrix — Remote Config Unity 10.5.0. Основной gameplay не зависит от его наличия.

## Install Referrer

`RuStoreInstallReferrerService` также reflection-isolated. Текущий production target — Install Referrer Unity 10.6.1. Package временно отсутствует в Editor manifest; перед релизом его нужно подключить официальным способом и проверить install -> first launch -> token recovery на Android.

## Notifications

Daily reminder планируется локально через Unity Mobile Notifications, без собственного push-server. На Android 13+ `POST_NOTIFICATIONS` запрашивается только после понятного value prompt и положительного игрового события.

## Review / Update

Review и Update запускаются только из safe Home state. Gameplay от них не зависит. Review не просит конкретное число звёзд; Update использует RuStore flow и version policy.

## UI / mobile lifecycle

Runtime UI остаётся programmatic, но имеет единый visual-language слой: dark surface, cyan/violet accents, rounded buttons/cards, disabled states и safe-area correction.

Home содержит Campaign / Daily / Training плюс statistics/store. Campaign level selector, Training difficulty selector, meta/store/settings и result overlays стилизуются согласованно. Android Back сначала закрывает активные overlays, затем возвращает из раунда на Home и только на idle Home завершает приложение. Pause во время активного жеста безопасно отменяет незавершённый раунд.

## Production release preflight

`ProductionReleaseValidator` блокирует release Android build при нарушении production-инвариантов:

- package name не должен оставаться `.dev`;
- `UnityPlayerActivity`;
- min API 25 для Unity 6.3 и текущий target API baseline;
- pinned Pay/Update/Review и RuStore registry;
- пустой developer backend URL;
- Remote Config production App ID;
- Yandex production define и реальные ad IDs;
- custom keystore/alias;
- AAB;
- IL2CPP + ARM64;
- положительный versionCode.

Credentials и keystore passwords не хранятся в репозитории.

## CI

На PR выполняются **6 независимых проверок**:

- `pure-csharp-tests`;
- `backend-tests` — только optional future backend;
- `offline-mode-guard`;
- `product-flow-guard`;
- `rustore-dependency-guard`;
- `unity-static-validation`.

`offline-mode-guard` защищает отсутствие обязательного собственного backend и L3 fairness invariants. `product-flow-guard` защищает Campaign 60 levels, star/reward economy, coin-to-hint sink, Training selector и shared runtime save invariants.

Static/CI проверки не заменяют финальный Unity compile, signed AAB и физический Android/RuStore smoke-test.