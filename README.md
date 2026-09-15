# НЕ СБЕЙСЯ!

Мобильная hypercasual-игра для Android / RuStore: игрок несколько секунд запоминает маршрут, затем одним движением повторяет его по памяти и получает точность 0–100%.

## Главное: свой сервер не нужен

Текущий MVP — **offline-first**. Для обычной работы игры не требуется VPS, собственная база данных, домен или постоянно работающий backend.

На телефоне пользователя локально работают:

- обучение, Daily Challenge и Training;
- генерация маршрутов и пересчёт score;
- streak, лучший результат, статистика, настройки и косметика;
- friend duel: challenge-код содержит дату, `seed`, `generatorVersion` и результат друга;
- versioned `SaveData` + migrations;
- локальный bounded analytics log и crash log;
- локальное Daily-напоминание через Unity Mobile Notifications.

Daily одинаковый на устройствах благодаря детерминированному seed из UTC-даты и `generatorVersion`. Ограничение offline-варианта: пользователь теоретически может изменить системную дату устройства, поэтому абсолютной античит-защиты без внешнего источника времени нет.

Для вызова другу сервер тоже не нужен. Share содержит deeplink для установленной игры и RuStore install URL с тем же компактным `referrerId`. После новой установки RuStore Install Referrer возвращает этот токен приложению, и тот же challenge восстанавливается локально.

## Что реализовано

- Unity 6.3 LTS проект, зафиксированный на `6000.3.24f1`, portrait и `UnityPlayerActivity`.
- Детерминированный fixed-point generator: `seed + generatorVersion` воспроизводит ту же геометрию маршрута.
- Pure C# scoring: среднее отклонение, завершённость, попадание в END и грубые ошибки.
- Полный цикл: показать маршрут → скрыть → нарисовать → показать обе траектории → score.
- Daily из 3 маршрутов и бесконечный Training.
- Offline friend duel без собственной БД.
- Локальная статистика вместо обязательного глобального leaderboard.
- Versioned save + migrations, без хранения основного прогресса только в PlayerPrefs.
- RuStore Pay, Install Referrer, Review, Update и Remote Config за интерфейсами/адаптерами.
- RuStore Remote Config использует общий runtime snapshot + cache/default fallback; время показа маршрутов и рекламные caps могут обновляться без изменения deterministic geometry.
- Магазин получает цену только из RuStore Pay SDK; non-consumables восстанавливаются через `GetPurchases`.
- Yandex Mobile Ads adapter для rewarded/interstitial; gameplay не зависит от рекламного SDK.
- Rewarded выдаёт награду только после reward callback; interstitial разрешён только между сессиями и учитывает `remove_ads`.
- Android 13 notification permission запрашивается после value prompt; Daily reminder планируется локально, поэтому Push-сервер не нужен.
- Safe area, Android Back и pause/resume policy реализованы для runtime-created UI.
- Production preflight блокирует неверные Activity/package/deeplink/RuStore pins/Remote Config App ID/ad IDs.
- GitHub Actions проверяют pure C# rules, optional backend, offline invariants, RuStore dependencies и структуру Unity-проекта.

## Быстрый запуск

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть `UnityProject`.
3. Дождаться Package Manager/compile.
4. Скрипт `ProjectConfigurator` создаст `Assets/Scenes/Main.unity`, если сцены ещё нет, и выставит Android-настройки.
5. Открыть `Main.unity` и нажать Play.

Перед production необходимо заменить `.dev` package name на точное значение из RuStore Console, настроить PayClient, Remote Config App ID, signing и production ad unit IDs. Собственный backend URL задавать **не требуется**.

## Production blockers

Оставшиеся блокеры связаны с внешней release-конфигурацией и реальной сборкой, а не с обязательным сервером:

- production Package Name/signing/RuStore Console IDs;
- PayClient Patch/Verify Manifest и purchase/restore test на устройстве;
- RuStore Remote Config App ID и значения ключей;
- импорт официального Yandex Mobile Ads Unity package + реальные block IDs;
- открытие/compile/EditMode tests в Unity `6000.3.24f1`;
- signed AAB + Android device smoke/regression.

Подробно: `docs/MVP_STATUS.md` и `docs/RUSTORE_RELEASE_CHECKLIST.md`.

## Опциональный backend

Папка `server/` сохранена как необязательный задел для будущего онлайн-режима: глобального leaderboard, authoritative server-time Daily, усиленной античит-проверки и server-side invoice verification. Текущая Android-сборка от этого кода не зависит.

Если когда-нибудь понадобится онлайн-режим, backend можно запустить отдельно:

```bash
cd server
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\\Scripts\\activate
pip install -e .[dev]
pytest
uvicorn app.main:app --reload
```

## RuStore

Перед каждой production-сборкой пройти `docs/RUSTORE_RELEASE_CHECKLIST.md` и повторно сверить версии SDK/Android requirements с официальной документацией RuStore. `consoleApplicationId`, signing credentials и другие production credentials не хранятся в репозитории.

## Структура

- `UnityProject/Assets/Game/Core` — save/model/fixed-point.
- `UnityProject/Assets/Game/Gameplay` — generator, score, Daily definitions, pure runtime tuning.
- `UnityProject/Assets/Game/Daily` — Daily flow, streak/review policy.
- `UnityProject/Assets/Game/Social` — offline challenge codec, duel/referral flow.
- `UnityProject/Assets/Game/Economy` — товары и entitlements.
- `UnityProject/Assets/Game/Monetization` — ad policies + optional provider adapter.
- `UnityProject/Assets/Game/Services` — интерфейсы внешних сервисов.
- `UnityProject/Assets/Game/Presentation` — UI/input/runtime wiring.
- `UnityProject/Assets/Game/Platform/RuStore` — RuStore adapters.
- `UnityProject/Assets/Game/Platform/Android` — notification permission/local scheduling.
- `server` — **опциональный**, не требуется текущему приложению.
- `scripts` — static release/project validation helpers.
- `docs` — архитектура, SDK matrix, статус и release checklist.
