# НЕ СБЕЙСЯ!

Мобильная hypercasual-игра для Android / RuStore: игрок несколько секунд запоминает маршрут, затем одним движением повторяет его по памяти и получает точность 0–100%.

## Главное: свой сервер не нужен

Текущий MVP — **offline-first**. Для обычной работы игры не требуется VPS, собственная база данных, домен или постоянно работающий backend.

На телефоне пользователя локально работают:

- обучение, Daily Challenge и Training;
- генерация маршрутов и пересчёт score;
- streak, лучший результат, статистика, настройки и косметика;
- friend duel: challenge-код содержит дату, `seed`, `generatorVersion` и результат друга;
- сохранения в versioned `SaveData`;
- локальная очередь аналитических событий (без обязательной отправки на наш сервер).

Daily одинаковый на устройствах благодаря детерминированному seed из UTC-даты и `generatorVersion`. Ограничение offline-варианта: пользователь теоретически может изменить системную дату устройства, поэтому абсолютной античит-защиты без внешнего источника времени нет.

Для вызова другу сервер тоже не нужен. Share содержит deeplink для установленной игры и RuStore install URL с тем же компактным `referrerId`. После новой установки RuStore Install Referrer возвращает этот токен приложению, и тот же challenge восстанавливается локально.

## Что реализовано

- Unity 6.3 LTS проект, зафиксированный на `6000.3.24f1`, portrait и `UnityPlayerActivity`.
- Детерминированный fixed-point generator: `seed + generatorVersion` воспроизводит тот же маршрут.
- Pure C# scoring: среднее отклонение, завершённость, попадание в END и грубые ошибки.
- Полный цикл: показать маршрут → скрыть → нарисовать → показать обе траектории → score.
- Daily из 3 маршрутов и бесконечный Training.
- Offline friend duel без собственной БД.
- Локальная статистика вместо обязательного глобального leaderboard.
- Versioned save + migrations, без хранения основного прогресса только в PlayerPrefs.
- RuStore Pay, Install Referrer, Review и Update за интерфейсами/адаптерами.
- Магазин получает цену только из RuStore Pay SDK.
- Rewarded/interstitial policy отделена от конкретного рекламного provider.
- Production preflight блокирует неверные Activity/package/deeplink/RuStore dependency pins.

## Быстрый запуск

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть `UnityProject`.
3. Дождаться Package Manager/compile.
4. Скрипт `ProjectConfigurator` создаст `Assets/Scenes/Main.unity`, если сцены ещё нет, и выставит Android-настройки.
5. Открыть `Main.unity` и нажать Play.

Для production необходимо заменить `.dev` package name на точное значение из RuStore Console и настроить RuStore PayClient. Собственный backend URL задавать **не требуется**.

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

## RuStore

Перед production-сборкой пройти `docs/RUSTORE_RELEASE_CHECKLIST.md`. Pay SDK и корректная Android Activity/deeplink остаются release blocker. Значения `consoleApplicationId`, signing credentials и другие секреты не хранятся в репозитории.

## Структура

- `UnityProject/Assets/Game/Core` — save/model/fixed-point.
- `UnityProject/Assets/Game/Gameplay` — generator, score, Daily definitions.
- `UnityProject/Assets/Game/Daily` — Daily flow, streak/review policy.
- `UnityProject/Assets/Game/Social` — offline challenge codec, duel/referral flow.
- `UnityProject/Assets/Game/Economy` — товары и entitlements.
- `UnityProject/Assets/Game/Monetization` — ad policies.
- `UnityProject/Assets/Game/Services` — интерфейсы внешних сервисов.
- `UnityProject/Assets/Game/Presentation` — UI/input/runtime wiring.
- `UnityProject/Assets/Game/Platform/RuStore` — RuStore adapters.
- `server` — **опциональный**, не требуется текущему приложению.
- `docs` — архитектура, SDK matrix, статус и release checklist.
