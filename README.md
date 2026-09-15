# НЕ СБЕЙСЯ!

Мобильная hypercasual-игра для Android / RuStore: игрок несколько секунд запоминает маршрут, затем одним движением повторяет его по памяти и получает точность 0–100%.

## Главное: свой сервер не нужен

Текущий MVP — **offline-first**. Для обычной работы игры не требуется VPS, собственная база данных, домен или постоянно работающий backend.

На телефоне пользователя локально работают:

- обучение, Daily Challenge и Training;
- генерация маршрутов и пересчёт score;
- streak, лучший результат, статистика, настройки и косметика;
- friend duel: challenge-код содержит дату, `seed`, `generatorVersion`, число маршрутов, точное время показа каждого маршрута и результат друга;
- сохранения в versioned `SaveData`;
- локальный журнал аналитических событий без обязательной отправки на наш сервер;
- локальные Daily-напоминания без push-сервера.

Daily одинаковый на устройствах благодаря детерминированному seed из UTC-даты и `generatorVersion`. RuStore Remote Config может менять время показа маршрута и количество Daily-маршрутов от 1 до 3, не меняя геометрию уже заданного `seed + generatorVersion`. Ограничение offline-варианта: пользователь теоретически может изменить системную дату устройства, поэтому абсолютной античит-защиты без внешнего источника времени нет.

Для вызова другу сервер тоже не нужен. Share содержит deeplink для установленной игры и RuStore install URL с тем же компактным `referrerId`. Новый **L3 challenge-token** содержит дату, seed, generatorVersion, routeCount, exact display-time profile и score. Поэтому друг получает то же испытание даже если Remote Config между отправкой и открытием ссылки уже изменился. Старые L1/L2 tokens продолжают открываться с безопасным legacy/default timing. После новой установки RuStore Install Referrer возвращает этот токен приложению, и challenge восстанавливается локально.

## Что реализовано

- Unity 6.3 LTS проект, зафиксированный на `6000.3.24f1`, portrait и `UnityPlayerActivity`.
- Детерминированный fixed-point generator: `seed + generatorVersion` воспроизводит ту же геометрию маршрута.
- Pure C# scoring: среднее отклонение, завершённость, попадание в END и грубые ошибки.
- Полный цикл: показать маршрут → скрыть → нарисовать → показать обе траектории → score.
- Daily из 1–3 маршрутов через Remote Config и бесконечный Training.
- Offline friend duel без собственной БД, сохраняющий routeCount и exact display times исходного challenge.
- Локальная статистика вместо обязательного глобального leaderboard.
- Versioned SaveData v10 + migrations, без хранения основного прогресса только в PlayerPrefs.
- RuStore Pay, Install Referrer, Review, Update и Remote Config за интерфейсами/адаптерами.
- Магазин получает цену только из RuStore Pay SDK.
- Yandex Mobile Ads adapter за `IAdService`: rewarded/interstitial не зависят от gameplay-кода.
- Unity Mobile Notifications для локального Daily reminder.
- Safe-area/Back/pause-resume policy и локальный crash log.
- Production preflight блокирует неверные Activity/package/deeplink/RuStore dependency pins, пустой Remote Config App ID, пустые/demo ad IDs, отсутствие custom keystore, AAB, IL2CPP/ARM64 или валидного versionCode.
- CI проверяет pure C# rules, optional backend, offline invariants, RuStore dependency pins и статическую структуру Unity-проекта.

## Быстрый запуск

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть `UnityProject`.
3. Дождаться Package Manager/compile.
4. Скрипт `ProjectConfigurator` создаст `Assets/Scenes/Main.unity`, если сцены ещё нет, и выставит Android-настройки.
5. Открыть `Main.unity` и нажать Play.

Для production необходимо заменить `.dev` package name на точное значение из RuStore Console, настроить RuStore PayClient, Remote Config App ID, production signing и рекламные block IDs. Собственный backend URL задавать **не требуется**.

## Remote Config

Runtime использует один shared `RuStoreRemoteConfigService` с локальным cache/default fallback. Gameplay не зависит от RuStore SDK: `RemoteGameplayTuningCoordinator` переводит валидированные параметры в pure C# `RouteRuntimeTuning`.

Ключи MVP:

- `route_display_time_easy_ms`, `route_display_time_medium_ms`, `route_display_time_hard_ms`;
- `daily_route_count` — 1..3;
- `rewarded_enabled`, `interstitial_enabled`, `interstitial_min_rounds`, `interstitial_cooldown_sec`;
- `review_min_sessions`;
- `local_daily_reminder_enabled`, `daily_reminder_hour`;
- `share_copy_variant`, `store_offer_variant`;
- `min_supported_version`, `recommended_version`.

Remote Config применяется при создании нового challenge. Уже созданный/расшаренный challenge фиксирует свой routeCount/display-time profile, поэтому изменение config не меняет условия вызова задним числом.

## Реклама

В коде есть `YandexMobileAdsService` и runtime wiring для rewarded/interstitial. Gameplay продолжает работать без рекламного SDK. Для production требуется импортировать официальный Unity package, включить define `YANDEX_MOBILE_ADS` и задать реальные rewarded/interstitial block IDs; preflight намеренно блокирует release без этих настроек. Подробности — `docs/ADS_INTEGRATION.md`.

## Локальные уведомления

Daily reminder планируется через `com.unity.mobile.notifications` и не требует RuStore Push или нашего сервера. На Android 13+ `POST_NOTIFICATIONS` запрашивается только после первого завершённого Daily и отдельного value prompt. Отказ не блокирует игру.

## Опциональный backend

Папка `server/` сохранена как необязательный задел для будущего онлайн-режима: глобального leaderboard, server-time Daily, усиленной античит-проверки и server-side invoice verification. Текущая Android-сборка от этого кода не зависит.

Если когда-нибудь понадобится онлайн-режим, backend можно запустить отдельно:

```bash
cd server
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\\Scripts\\activate
pip install -e .[dev]
pytest
uvicorn app.main:app --reload
```

## Проверки

На Pull Request запускаются:

- `pure-csharp-tests` — generator/scoring/Daily/Duel/Economy/ad policies/offline codec/runtime tuning;
- `backend-tests` — только опциональный future-online backend;
- `offline-mode-guard` — запрещает вернуть обязательный backend/server-sync UX и защищает L3 fairness invariants;
- `rustore-dependency-guard` — защищает pins/registry/UnityPlayerActivity-инварианты;
- `unity-static-validation` — проверяет manifest, asmdef, UPM pins и обязательные runtime-файлы.

Статические CI-проверки **не заменяют** реальный compile в Unity и Android device tests.

## RuStore

Перед production-сборкой пройти `docs/RUSTORE_RELEASE_CHECKLIST.md`. Pay SDK и корректная Android Activity/deeplink остаются release blocker. Значения `consoleApplicationId`, signing credentials и другие production-параметры не хранятся в репозитории.

## Структура

- `UnityProject/Assets/Game/Core` — save/model/fixed-point.
- `UnityProject/Assets/Game/Gameplay` — generator, score, Daily definitions и pure runtime tuning.
- `UnityProject/Assets/Game/Daily` — Daily flow, streak/review policy.
- `UnityProject/Assets/Game/Social` — offline challenge codec, duel/referral flow.
- `UnityProject/Assets/Game/Economy` — товары и entitlements.
- `UnityProject/Assets/Game/Monetization` — ad policies + Yandex adapter.
- `UnityProject/Assets/Game/Services` — интерфейсы внешних сервисов.
- `UnityProject/Assets/Game/Presentation` — UI/input/runtime wiring.
- `UnityProject/Assets/Game/Platform/RuStore` — RuStore adapters.
- `UnityProject/Assets/Game/Platform/Android` — notification permission/local scheduler.
- `server` — **опциональный**, не требуется текущему приложению.
- `docs` — архитектура, SDK matrix, статус и release checklist.
