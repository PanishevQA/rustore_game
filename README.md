# НЕ СБЕЙСЯ!

Мобильная hypercasual-игра для Android / RuStore: игрок несколько секунд запоминает маршрут, затем одним движением повторяет его по памяти и получает точность 0–100%.

## Что уже реализовано в этом репозитории

- Unity 6.3 LTS проект (зафиксирован на `6000.3.24f1`) с portrait-настройкой и `UnityPlayerActivity`.
- Детерминированный генератор маршрутов на fixed-point координатах: `seed + generatorVersion` воспроизводит один и тот же polyline.
- Pure C# scoring без зависимости от Unity: среднее отклонение, завершённость, попадание в END и грубые ошибки.
- Runtime-прототип игрового цикла: показать маршрут → скрыть → нарисовать → показать обе траектории → score.
- Daily из 3 маршрутов и Endless/Training.
- Сервисные интерфейсы для рекламы, платежей, аналитики, Remote Config, review/update/referrer и API.
- Изолированный слой `Game.Platform.RuStore` и таблица зафиксированных SDK-версий.
- Минимальный FastAPI backend: bootstrap config, Daily, серверная верификация replay, leaderboard, challenge/referral и landing page.
- Автотест backend/game-rules, включая валидацию 1000 процедурных маршрутов.

## Быстрый запуск клиента

1. Установить Unity `6000.3.24f1` с Android Build Support.
2. Открыть папку `UnityProject`.
3. Дождаться Package Manager/compile. Скрипт `ProjectConfigurator` создаст `Assets/Scenes/Main.unity`, добавит её в Build Settings и выставит Android-настройки.
4. Открыть `Main.unity` и нажать Play.
5. Для Android сборки задать реальный package name в `ProjectConfigurator.PackageName` до публикации и сверить его с RuStore Console.

Проект запускается без backend и без RuStore: Daily использует локальный fallback, Training полностью offline. Production Daily должен получать seed и серверное время из backend.

## Backend

```bash
cd server
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\\Scripts\\activate
pip install -e .[dev]
pytest
uvicorn app.main:app --reload
```

По умолчанию SQLite хранится в `server/data/game.db`. Настройки задаются переменными окружения, см. `server/app/main.py`.

## RuStore

Перед production-сборкой обязательно пройти `docs/RUSTORE_RELEASE_CHECKLIST.md`. Pay SDK и Activity/deeplink — релизный blocker. Значения `consoleApplicationId`, Pay deeplink scheme, Push project ID и Remote Config app ID намеренно не хранятся в репозитории.

## Структура

- `UnityProject/Assets/Game/Core` — fixed-point типы и детерминированный RNG.
- `UnityProject/Assets/Game/Gameplay` — generator, score, Daily.
- `UnityProject/Assets/Game/Services` — интерфейсы внешних сервисов и безопасные fallback-реализации.
- `UnityProject/Assets/Game/Presentation` — ввод, UI, route renderer, игровой flow.
- `UnityProject/Assets/Game/Platform/RuStore` — граница RuStore-интеграций.
- `server` — MVP API и server-side score verification.
- `docs` — архитектура, SDK matrix и release checklist.
