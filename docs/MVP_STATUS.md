# MVP readiness — 2026-09-15

Текущая оценка **production-ready offline-first MVP: 90%**.

Это не процент строк кода. Оценка взвешивает обязательные блоки продукта и отдельно учитывает то, что нельзя честно считать готовым без Unity Editor, production RuStore Console параметров, подписанного Android AAB и проверки на физическом устройстве.

Ключевое архитектурное решение: **для релиза не нужен developer-operated backend, сервер или собственная база данных**. `OptionalBackendBaseUrl` пустой, а production preflight и CI защищают это условие. Игра может обращаться к RuStore/Yandex как к внешним платформенным сервисам, но не требует нашего постоянно работающего API.

| Блок | Вес | Готовность | Состояние |
|---|---:|---:|---|
| Core gameplay + deterministic generator + score | 20% | 100% | Реализовано, pure C#, unit tests |
| Local Daily + replay recalculation | 15% | 98% | Daily детерминирован из UTC-даты + generatorVersion, score повторно считается локально; нужен device/golden-seed test |
| Social/referral/duel viral loop | 12% | 97% | Самодостаточный challenge token; deeplink + RuStore Install Referrer восстанавливают duel без БД |
| Save/local analytics/Remote Config policies | 10% | 98% | SaveData v8, migrations, bounded local analytics, RuStore Remote Config cache/default fallback |
| Economy + RuStore Pay | 12% | 90% | Pay 11.1.0 adapter, SDK price, store/restore, offline-first entitlement flow; нужны Console credentials и device purchase tests |
| Ads monetization | 8% | 78% | Yandex Mobile Ads 8.x adapter, rewarded/interstitial lifecycle и caps готовы; нужны официальный package import, реальные IDs и device test |
| UI/meta/mobile lifecycle | 8% | 94% | Tutorial/Home/Daily/Training/Duel/statistics/store, safe area, Back, pause/resume, local notifications; нужен multi-device visual pass |
| RuStore platform services | 7% | 92% | Review/Update/Install Referrer/Remote Config готовы; Push не нужен для MVP, Daily reminder локальный |
| Production/release verification | 8% | 62% | Static Unity validator, manifest/preflight/SDK/offline guards готовы; ещё нет Unity compile, signing, AAB и device smoke test |

Взвешенная оценка получается около **90%**. Feature/code completeness выше — примерно **96%**, но её нельзя использовать как «готово к публикации» до реальной Unity/Android проверки.

## Уже закрытые release-critical требования

- Unity `6000.3.24f1`, portrait, IL2CPP/ARM64 foundation.
- `UnityPlayerActivity` для Pay; static validator и production preflight защищают от `GameActivity`.
- Pay `11.1.0`, Install Referrer `10.6.1`, Update/Review `10.5.1`, Remote Config `10.5.1` закреплены конкретными версиями.
- Unity Mobile Notifications `2.4.3` используется для локального Daily reminder без push-сервера.
- Актуальный RuStore npm registry и CI guard против старых repository/BillingClient references.
- Offline-first release: `OptionalBackendBaseUrl = ""`; собственный сервер и БД не нужны.
- Детерминированный Daily: UTC-дата + `generatorVersion` → одинаковый seed/route.
- Score пересчитывается из replay локальным pure C# алгоритмом.
- Challenge token несёт date/seed/generatorVersion/inviterScore; duel восстанавливается без lookup в БД.
- Share содержит deeplink и RuStore install URL с тем же self-contained token.
- SaveData v8 удаляет legacy sync queue; новые Daily/Duel не создают `PendingAttempts`.
- Каталог и цены магазина приходят из RuStore Pay SDK; non-consumables восстанавливаются через `GetPurchases`.
- Consumable reward защищён сохранённым `purchaseId` от повторной локальной выдачи.
- Yandex rewarded выдаёт награду только после reward callback; interstitial учитывается как показанный только после успешного show result.
- `remove_ads`/`starter_pack` подавляют interstitial.
- RuStore Remote Config работает через локальный cache/default fallback и валидирует управляемые диапазоны.
- Review запускается только после positive event; Update — только из safe Home state.
- Android 13 notification permission запрашивается после value prompt и первого завершённого Daily.
- Daily reminder планируется полностью локально на устройстве; RuStore Push для MVP не требуется.
- Safe area, Android Back и pause/resume policy реализованы; системный share sheet не уничтожает Result screen.
- `LocalCrashLog` и `LocalAnalyticsService` не требуют нашего сервера.
- CI: pure C# tests, optional backend tests, offline guard, RuStore dependency guard, Unity static validation — зелёные на `8c35f42182e417893ec7db42037f1a23927bcfe4`.

## Что блокирует 100%

1. Открыть проект в Unity `6000.3.24f1`, дождаться UPM resolve/compile и прогнать EditMode tests; исправить возможные package/API compile issues, которые статический validator увидеть не может.
2. Заменить `.dev` package name на точное значение из RuStore Console, настроить production signing и PayClient (`consoleApplicationId`, deeplink/manifest patch/verify).
3. Указать реальный RuStore Remote Config App ID и создать используемые ключи/значения в Console.
4. Импортировать официально скачанный Yandex Mobile Ads Unity package, включить `YANDEX_MOBILE_ADS` и указать реальные rewarded/interstitial block IDs.
5. Прогнать RuStore Pay purchase/cancel/error/restore на физическом Android-устройстве.
6. Собрать подписанный release AAB, пройти `Tools/НЕ СБЕЙСЯ!/Validate Production Release` и установить сборку минимум на API 24 и Android 13+.
7. Выполнить device smoke/regression: Daily/Training/Duel/deeplink/Install Referrer/Review/Update/ads/local notification, background/resume, Android Back, share sheet, safe area и несколько DPI/aspect ratios.
8. Повторно сверить версии SDK и Android/RuStore требования непосредственно перед production-сборкой.

Опциональный `server/` остаётся в репозитории только как задел для будущего глобального leaderboard/authoritative online mode. Релизная сборка от него не зависит.

PR #1 остаётся Draft до выполнения внешних release blockers.
