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

Remote Config **не входит** в детерминированную геометрию. Он меняет только presentation/session параметры: время показа и число маршрутов Daily.

## Runtime tuning / Remote Config

`RuStoreRemoteConfigService` — один shared provider с локальным cache/default fallback. SDK остаётся внутри `Platform/RuStore`.

`RemoteGameplayTuningCoordinator` переводит валидированные значения в pure C# `RouteRuntimeTuning`.

Поддерживаемые gameplay/session настройки:

- `route_display_time_easy_ms`;
- `route_display_time_medium_ms`;
- `route_display_time_hard_ms`;
- `daily_route_count` — только 1..3.

Допустимое время показа ограничено pure C# policy диапазоном 750–10000 мс. Некорректный Remote Config value отклоняется в пользу безопасного default.

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
- `routeCount` из валидированного runtime tuning;
- exact display-time profile для маршрутов текущего challenge.

`DailyChallengeFactory` создаёт 1–3 маршрута в порядке Easy → Medium → Hard. Итоговый Daily score — среднее завершённых маршрутов.

`DailySessionService` хранит последний challenge в `DailyCacheData`, включая `routeCount` и времена показа Easy/Medium/Hard. Поэтому fallback не может изменить уже созданный Daily после смены Remote Config.

Ограничение полностью локального варианта: системную дату можно изменить вручную, поэтому строгой server-time anti-cheat защиты нет. Это осознанный trade-off отсутствия собственного сервера.

## Friend challenge / viral loop

Вызов другу не требует lookup в нашей БД.

Текущий **L3 token** содержит:

- дату;
- seed;
- generatorVersion;
- routeCount;
- exact display time каждого используемого маршрута;
- score отправителя.

Для 1/2/3 маршрутов token остаётся коротким и пригодным для deeplink/referrer. Для максимального трёхмаршрутного challenge длина составляет 36 символов.

Share содержит:

1. `nesbeisya://challenge/<token>` для установленной игры;
2. RuStore install URL с тем же token в `referrerId`.

После установки RuStore Install Referrer отдаёт token один раз; приложение сохраняет его до обработки и локально восстанавливает тот же challenge.

Fairness-инвариант: изменение Remote Config после отправки ссылки **не меняет условия Duel**. `DuelSessionService` строит challenge по профилю из L3, а не по текущим временам устройства получателя.

Backward compatibility:

- L1 — 3 маршрута с default display times;
- L2 — routeCount из token, display times берутся из defaults;
- L3 — routeCount и exact display-time profile полностью зафиксированы в token.

## Сохранения

Основной прогресс хранится в JSON-файле через `JsonFileSaveRepository`, не только в PlayerPrefs.

Текущий `SaveData` — **v10**, versioned и мигрируемый. `DailyCacheData` сохраняет `RouteCount` и exact display times. Миграция v9 → v10 нормализует старый cache безопасными default timing. Legacy очередь server-sync очищается более ранней миграцией; release runtime не создаёт новые pending server attempts.

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

## Production release preflight

`ProductionReleaseValidator` блокирует non-development Android build, если нарушены обязательные release-инварианты. В частности проверяются:

- production package name вместо `.dev`;
- `UnityPlayerActivity`;
- Android SDK baseline;
- pinned RuStore packages/registry;
- пустой developer backend URL;
- RuStore Remote Config App ID;
- Yandex define и реальные ad unit IDs;
- custom Android keystore/alias;
- AAB;
- IL2CPP + ARM64;
- положительный Android versionCode.

Credentials/keystore passwords не хранятся в репозитории.

## Проверки

CI выполняет пять независимых проверок:

- pure C# tests;
- optional backend tests;
- offline-mode guard;
- RuStore dependency guard;
- Unity static validation.

`offline-mode-guard` дополнительно защищает L3 fairness-инварианты: routeCount/display-time profile должны оставаться self-contained и legacy L1/L2 decoder нельзя удалить случайным рефакторингом.

Static validation проверяет структуру, manifest, asmdef и pinned dependencies, но **не заменяет** открытие проекта в Unity, EditMode tests, Android build и device smoke-test.
