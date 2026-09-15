# MVP readiness — 2026-09-15

Текущая оценка **production-ready offline-first MVP: 85%**.

Это не процент строк кода. Оценка взвешивает обязательные блоки продукта и отдельно учитывает то, что нельзя считать готовым без Unity/Android/RuStore device-проверки.

Ключевое архитектурное решение: **для релиза не нужен developer-operated backend, сервер или собственная база данных**. `OptionalBackendBaseUrl` пустой, а production preflight и CI защищают это условие. Каталог/покупки/Review/Update/Install Referrer могут обращаться к RuStore как к платформенному сервису, но игра не требует нашего API.

| Блок | Вес | Готовность | Состояние |
|---|---:|---:|---|
| Core gameplay + deterministic generator + score | 20% | 100% | Реализовано, pure C#, есть unit tests |
| Local Daily + replay recalculation | 15% | 98% | Daily детерминирован из UTC-даты + generatorVersion, score повторно считается локально; нужен device/golden-seed тест |
| Social/referral/duel viral loop | 12% | 95% | Challenge-token сам содержит дату/seed/version/score; deeplink + RuStore Install Referrer восстанавливают тот же duel без БД |
| Save/local analytics/local config policies | 10% | 95% | Versioned SaveData, migrations, локальная ограниченная analytics queue, безопасные defaults; server upload не обязателен |
| Economy + RuStore Pay | 12% | 86% | Pay 11.1.0 adapter, SDK price, store/restore, локальная идемпотентная выдача; нужны Console credentials и purchase/restore device tests |
| Ads monetization | 8% | 45% | Rewarded/interstitial policy и caps готовы; конкретный ad-network provider ещё не подключён |
| UI/meta screens | 8% | 84% | Tutorial, Home, Daily, Training, Duel, local statistics, store; требуется mobile UX/safe-area/back-button polish |
| RuStore platform services | 7% | 72% | Review, Update, Referrer готовы; Push permission feature-flagged, delivery SDK ещё не включён |
| Production/release verification | 8% | 50% | Manifest/preflight/SDK/offline guards готовы; нет production package/signing/AAB/device smoke test/crash reporting |

Взвешенная оценка получается около **85%**. Feature/code completeness выше — примерно **91%**, но её нельзя использовать как «готово к публикации».

## Уже закрытые release-critical требования

- Unity `6000.3.24f1`, portrait, IL2CPP/ARM64 foundation.
- `UnityPlayerActivity` для Pay и production preflight против `GameActivity`.
- Pay SDK `11.1.0`, Install Referrer `10.6.1`, Update/Review `10.5.1`.
- Актуальный RuStore npm registry и CI guard против старых repository/BillingClient references.
- Offline-first release: `OptionalBackendBaseUrl = ""`; собственный сервер и БД не нужны.
- Детерминированный Daily: дата + `generatorVersion` → одинаковый seed/route.
- Score пересчитывается из replay локальным pure C# алгоритмом; UI score не является отдельным источником истины.
- Challenge token несёт `date + seed + generatorVersion + inviterScore`, поэтому duel восстанавливается без lookup в БД.
- SaveData migrations, offline Training/settings/economy/statistics.
- Каталог и цены магазина приходят из RuStore Pay SDK; restore идёт через RuStore.
- Review после positive event и Update/minSupportedVersion только из safe Home state.
- Android 13 notification permission показывается только после Daily и только при `push_enabled=true`.
- `.github/workflows/offline-mode-guard.yml` блокирует возврат обязательного backend в runtime.

## Что блокирует 100%

1. Подставить production package name, signing key и реальные RuStore Console application IDs/deeplink settings.
2. Прогнать Pay sandbox/реальную покупку и restore на Android; в offline-варианте защита покупок слабее server verification — это осознанный trade-off отсутствия собственного backend.
3. Подключить конкретный rewarded/interstitial provider и проверить lifecycle/callbacks на устройстве.
4. При необходимости включить RuStore Push Unity после проверки совместимости Activity и протестировать delivery/tap/deeplink; сама игра от Push не зависит.
5. Добавить production crash reporting без требования собственного сервера приложения.
6. Открыть проект в Unity `6000.3.24f1`, прогнать EditMode tests и устранить возможные compile/API проблемы packages.
7. Собрать signed AAB, пройти production preflight, установить на реальные Android 7/13+/целевые устройства и выполнить smoke/regression checklist.
8. Финальный UX/polish: safe areas, разные DPI/aspect ratios, accessibility/readability, back button и interrupted app lifecycle.

Опциональный `server/` остаётся в репозитории только как задел для будущего глобального leaderboard/authoritative online mode. Релизная сборка от него не зависит.

PR #1 остаётся Draft до выполнения release blockers.
