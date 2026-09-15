# Архитектура offline-first MVP

## Основное решение

Release-сборка **не требует собственного backend, сервера или базы данных**. Gameplay, Daily, friend challenge, streak, статистика, сохранения и replay verification выполняются на устройстве.

Внешние платформенные сервисы остаются допустимыми: RuStore Pay/Review/Update/Install Referrer/Remote Config и рекламный provider. Они подключаются через интерфейсы и не являются обязательными для базового gameplay.

Папка `server/` остаётся только как задел для будущего online-mode: глобального leaderboard, authoritative server-time, server-side anti-cheat и усиленной проверки покупок.

## Правило зависимостей

Ключевой принцип: чистые игровые правила не знают об Android/RuStore/рекламе/HTTP/MonoBehaviour.

Упрощённо:

`Core <- Gameplay <- Daily/Social/Economy/Monetization`

`Services <- Platform adapters`

`Presentation -> gameplay/application services + interfaces`

`Network` в текущем релизе содержит compatibility facade и `UnityGameApi`, который при пустом `OptionalBackendBaseUrl` делегирует в локальный `OfflineGameApi`. Собственный HTTP не является обязательным runtime path.

## Модули

| Модуль | Ответственность |
|---|---|
| Core | fixed-point, RNG, SaveData, migrations, reminder policy |
| Gameplay | RouteGenerator, ScoreCalculator, DailyChallengeFactory, RouteRuntimeTuning |
| Daily | DailySessionService, streak, review policy |
| Social | OfflineGameApi, challenge codec, referral parser, DuelSessionService |
| Economy | StoreService, entitlements, idempotent purchase grants |
| Monetization | ad policies/caps и `IAdService`-совместимый provider |
| Analytics | локальный bounded event journal |
| Network | compatibility/provider facade; HTTP — только опциональный future mode |
| Platform/RuStore | Pay, Review, Update, Install Referrer, Remote Config adapters |
| Platform/Android | notification permission и local Daily scheduler |
| Presentation | Unity UI, input, lifecycle, safe-area и runtime coordination |

## Детерминизм маршрута

1. Daily seed вычисляется локально из UTC-даты и `generatorVersion`.
2. `generatorVersion` участвует в создании RNG-state.
3. RNG и генератор маршрута используют целочисленные операции/fixed-point (`0..1_000_000`).
4. Bezier samples вычисляются целочисленно с фиксированным количеством точек.
5. Один `seed + generatorVersion` создаёт ту же геометрию на разных устройствах.
6. При изменении алгоритма повышается `generatorVersion`; старую стратегию нельзя тихо переписывать.

Remote Config **не входит** в детерминированную геометрию. Он может менять presentation/session параметры, например время показа и число маршрутов Daily.

## Runtime tuning / Remote Config

`RuStoreRemoteConfigService` — один shared provider с локальным cache/default fallback. SDK остаётся внутри `Platform/RuStore`.

`RemoteGameplayTuningCoordinator` переводит валидированные значения в pure C# `RouteRuntimeTuning`.

Поддерживаемые gameplay/session настройки:

- `route_display_time_easy_ms`;
- `route_display_time_medium_ms`;
- `route_display_time_hard_ms`;
- `daily_route_count` — только 1..3.

Изменение display time не изменяет reference points. Это покрыто pure C# тестом.

## Score

Score не зависит от SDK или сети. Состав:

- 70% — среднее расстояние пользовательской траектории до reference polyline;
- 18% — завершённость маршрута;
- 8% — попадание в END;
- 4% — штраф за грубые отклонения.

При прохождении менее 50% маршрута применяется дополнительный penalty. Итог округляется до 0,1%.

Перед фиксацией результата `OfflineGameApi` повторно рассчитывает score из replay, а не доверяет UI/client score.

## Daily

`OfflineDaily` создаёт:

- `challengeId` из UTC-даты;
- deterministic `seed` из даты + `generatorVersion`;
- текущий `generatorVersion`;
- `routeCount` из валидированного runtime tuning.

`DailyChallengeFactory` создаёт 1–3 маршрута в порядке Easy → Medium → Hard. Итоговый Daily score — среднее завершённых маршрутов.

`DailySessionService` хранит последний challenge в `DailyCacheData`, включая `routeCount`, чтобы fallback не мог изменить уже созданный Daily при другой Remote Config конфигурации.

Ограничение полностью локального варианта: системную дату можно изменить вручную, поэтому строгой server-time anti-cheat защиты нет. Это осознанный trade-off отсутствия собственного сервера.

## Friend challenge / viral loop

Вызов другу не требует lookup в нашей БД.

Текущий L2 token содержит:

- дату;
- seed;
- generatorVersion;
- routeCount;
- score отправителя.

Share содержит:

1. `nesbeisya://challenge/<token>` для установленной игры;
2. RuStore install URL с тем же token в `referrerId`.

После установки RuStore Install Referrer отдаёт token один раз; приложение сохраняет его до обработки и локально восстанавливает тот же challenge.

Для совместимости старый L1 token продолжает декодироваться как challenge из 3 маршрутов.

## Сохранения

Основной прогресс хранится в JSON-файле через `JsonFileSaveRepository`, не только в PlayerPrefs.

Текущий `SaveData` — versioned и мигрируемый. На v9 в `DailyCacheData` сохраняется `RouteCount`. Legacy очередь server-sync очищается миграцией; release runtime не создаёт новые pending server attempts.

Без сети работают:

- tutorial;
- Training;
- Daily;
- Duel после получения token;
- настройки;
- статистика;
- косметика;
- локальные сохранения.

## Покупки

Gameplay не знает о RuStore Pay. Economy зависит от `IPaymentService`.

Offline-first схема:

- каталог и price label приходят из RuStore Pay SDK;
- consumable выдаётся только после completed purchase result с непустым `purchaseId`; local `ProcessedPurchaseIds` предотвращает повторную выдачу;
- non-consumable ownership подтверждается/восстанавливается через `GetPurchases`;
- отсутствие RuStore/сети не блокирует игровой цикл.

Без server-side verification защита от модифицированного клиента слабее — это осознанный компромисс локальной архитектуры.

## Реклама

Gameplay зависит только от `IAdService`/policy classes.

Rewarded выдаёт награду только после reward callback. Interstitial допускается только между игровыми сессиями и не показывается в route display/drawing/result/share/store/purchase состояниях. `remove_ads`/`starter_pack` отключают interstitial.

## Notifications

Daily reminder планируется локально через Unity Mobile Notifications. Собственный push-server и RuStore Push для MVP не нужны.

На Android 13+ системное `POST_NOTIFICATIONS` запрашивается только после завершённого Daily и отдельного value prompt.

## Review / Update

Review и Update запускаются только из safe Home state. Gameplay от них не зависит.

- Review — после положительного события, без просьбы о конкретной оценке.
- Update — по `min_supported_version` / `recommended_version`; mandatory update может блокировать старую версию на Home.

## Проверки

CI выполняет пять независимых проверок:

- pure C# tests;
- optional backend tests;
- offline-mode guard;
- RuStore dependency guard;
- Unity static validation.

Static validation проверяет структуру, manifest, asmdef и pinned dependencies, но **не заменяет** открытие проекта в Unity, EditMode tests, Android build и device smoke-test.
