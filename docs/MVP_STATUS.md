# MVP readiness — 2026-09-15

Текущая оценка **production-ready offline-first MVP: 92%**.

Это не процент строк кода. Оценка взвешивает обязательные блоки продукта и отдельно учитывает то, что нельзя честно считать готовым без Unity Editor, production RuStore Console параметров, подписанного Android AAB и проверки на физическом устройстве.

Ключевое архитектурное решение: **для релиза не нужен developer-operated backend, сервер или собственная база данных**. `OptionalBackendBaseUrl` пустой, а production preflight и CI защищают это условие. Игра может обращаться к RuStore/Yandex как к внешним платформенным сервисам, но не требует нашего постоянно работающего API.

| Блок | Вес | Готовность | Состояние |
|---|---:|---:|---|
| Core gameplay + deterministic generator + score | 20% | 100% | Реализовано, pure C#, unit tests |
| Local Daily + replay recalculation | 15% | 99% | UTC-date seed, local replay verification, Remote Config 1–3 routes + exact timing cache; нужен device/golden-seed test |
| Social/referral/duel viral loop | 12% | 100% | L3 self-contained token фиксирует date/seed/version/routeCount/display-times/score; L1/L2 backward compatibility |
| Save/local analytics/Remote Config policies | 10% | 99% | SaveData v10, migrations, bounded local analytics, RuStore Remote Config cache/default fallback |
| Economy + RuStore Pay | 12% | 90% | Pay 11.1.0 adapter, SDK price, store/restore, offline-first entitlement flow; нужны Console credentials и device purchase tests |
| Ads monetization | 8% | 80% | Yandex Mobile Ads 8.x adapter, rewarded/interstitial lifecycle и caps готовы; нужны официальный package import, реальные IDs и device test |
| UI/meta/mobile lifecycle | 8% | 95% | Tutorial/Home/Daily/Training/Duel/statistics/store, dynamic route count, safe area, Back, pause/resume, local notifications |
| RuStore platform services | 7% | 94% | Review/Update/Install Referrer/Remote Config готовы; Push не нужен для MVP, Daily reminder локальный |
| Production/release verification | 8% | 62% | Static validator + signed-AAB preflight + CI guards готовы; ещё нет реального Unity compile/AAB/device smoke test |

Взвешенная оценка округлена консервативно до **92%**. Feature/code completeness — примерно **98%**, но её нельзя использовать как «готово к публикации» до реальной Unity/Android проверки.

## Уже закрытые release-critical требования

- Unity `6000.3.24f1`, portrait, IL2CPP/ARM64 foundation.
- `UnityPlayerActivity` для Pay; static validator и production preflight защищают от `GameActivity`.
- Pay `11.1.0`, Install Referrer `10.6.1`, Update/Review `10.5.1`, Remote Config `10.5.1` закреплены конкретными версиями.
- Unity Mobile Notifications `2.4.3` используется для локального Daily reminder без push-сервера.
- Актуальный RuStore npm registry и CI guard против старых repository/BillingClient references.
- Offline-first release: `OptionalBackendBaseUrl = ""`; собственный сервер и БД не нужны.
- Детерминированный Daily: UTC-дата + `generatorVersion` → одинаковая geometry при одинаковом seed/version.
- Remote Config меняет display-time и число Daily-маршрутов 1–3 без изменения детерминированной геометрии.
- Gameplay Daily keys должны быть глобальными в RuStore Console без audience targeting/A-B, чтобы пользователи одного дня получали одинаковые session-параметры.
- Score пересчитывается из replay локальным pure C# алгоритмом.
- Challenge token **L3** несёт date/seed/generatorVersion/routeCount/exact display-time profile/inviterScore; Duel не зависит от текущего Remote Config получателя.
- L1 и L2 challenge tokens продолжают декодироваться с безопасным legacy/default timing.
- Reshare L3 сохраняет исходный seed/version/routeCount/display-times и меняет только score нового отправителя.
- Share содержит deeplink и RuStore install URL с тем же self-contained token.
- SaveData v10 хранит routeCount + display-time profile cached Daily и удаляет legacy sync queue; новые Daily/Duel не создают `PendingAttempts`.
- GameBootstrap не показывает пользователю «синхронизацию»/«отправим позже» в offline-first build.
- Каталог и цены магазина приходят из RuStore Pay SDK; non-consumables восстанавливаются через `GetPurchases`.
- Consumable reward защищён сохранённым `purchaseId` от повторной локальной выдачи.
- Yandex rewarded выдаёт награду только после reward callback; interstitial учитывается как показанный только после успешного show result.
- `remove_ads`/`starter_pack` подавляют interstitial.
- RuStore Remote Config работает через один shared provider, локальный cache/default fallback и валидирует управляемые диапазоны.
- Review запускается только после positive event; Update — только из safe Home state.
- Android 13 notification permission запрашивается после value prompt и первого завершённого Daily.
- Daily reminder планируется полностью локально на устройстве; RuStore Push для MVP не требуется.
- Safe area, Android Back и pause/resume policy реализованы; системный share sheet не уничтожает Result screen.
- `LocalCrashLog` и `LocalAnalyticsService` не требуют нашего сервера.
- Production preflight блокирует release без custom keystore/alias, AAB, IL2CPP, ARM64, положительного versionCode и обязательных production IDs/settings.
- L3 code head `cc199bca56509b0004d64790d3996bd0942487c1`: pure C# tests, optional backend tests, offline-mode guard, RuStore dependency guard и Unity static validation — все зелёные.

## Осознанные ограничения полностью локальной архитектуры

1. Daily использует UTC системных часов телефона. Пользователь с модифицированным устройством может менять дату; абсолютный authoritative server-time без внешнего сервиса невозможен.
2. Remote Config gameplay-ключи должны быть глобальными и обновляться синхронно на стороне RuStore. Сегментация этих ключей запрещена release checklist-ом, иначе условия Daily могут отличаться между пользователями.
3. Без server-side purchase verification модифицированный клиент защищён слабее, чем online-схема. Для MVP ownership non-consumables восстанавливается через RuStore `GetPurchases`, consumable grants защищены локальной idempotency по purchaseId.
4. Глобального честного leaderboard в offline-first release нет; вместо него локальная статистика.

## Что блокирует 100%

1. Открыть проект в Unity `6000.3.24f1`, дождаться UPM resolve/compile и прогнать EditMode tests; исправить возможные package/API compile issues, которые статический validator увидеть не может.
2. Заменить `.dev` package name на точное значение из RuStore Console, настроить production signing и PayClient (`consoleApplicationId`, deeplink/manifest patch/verify).
3. Указать реальный RuStore Remote Config App ID, создать ключи в Console и убедиться, что четыре Daily gameplay key настроены глобально без audience segmentation.
4. Импортировать официально скачанный Yandex Mobile Ads Unity package, включить `YANDEX_MOBILE_ADS` и указать реальные rewarded/interstitial block IDs.
5. Прогнать RuStore Pay purchase/cancel/error/restore на физическом Android-устройстве.
6. Собрать подписанный release AAB, пройти `Tools/НЕ СБЕЙСЯ!/Validate Production Release` и установить сборку минимум на API 24 и Android 13+.
7. Выполнить device smoke/regression: Daily 1/2/3 routes, exact display times, Training, L1/L2/L3 Duel/deeplink, Install Referrer, Review, Update, ads, local notification, background/resume, Android Back, share sheet, safe area и несколько DPI/aspect ratios.
8. Повторно сверить версии SDK и Android/RuStore требования непосредственно перед production-сборкой.

Опциональный `server/` остаётся в репозитории только как задел для будущего глобального leaderboard/authoritative online mode. Релизная сборка от него не зависит.

PR #1 остаётся Draft до выполнения внешних release blockers.
