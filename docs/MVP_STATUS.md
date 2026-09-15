# MVP readiness — 2026-09-15

Текущая оценка **production-ready MVP: 82%**.

Это не процент строк кода. Оценка взвешивает обязательные блоки ТЗ и отдельно учитывает то, что невозможно считать готовым без Unity/Android/RuStore device-проверки.

| Блок | Вес | Готовность | Состояние |
|---|---:|---:|---|
| Core gameplay + deterministic generator + score | 20% | 100% | Реализовано, pure C#, есть unit tests |
| Daily + authoritative backend + offline sync | 15% | 95% | Server-time Daily, replay verification, cache/queue; нужен production deploy/load test |
| Social/referral/duel viral loop | 12% | 93% | Deeplink + Install Referrer + same seed/version duel; нужен реальный install-referrer device test |
| Save/analytics/Remote Config policies | 10% | 92% | Versioned save, migrations, offline analytics, bootstrap config; production config ещё не настроен |
| Economy + RuStore Pay | 12% | 85% | Pay 11.1.0 adapter, SDK price, store, restore, server invoice verification; нужны Console credentials и sandbox/real device purchase tests |
| Ads monetization | 8% | 45% | Rewarded/interstitial policy и caps готовы; конкретный ad-network provider ещё не подключён |
| UI/meta screens | 8% | 82% | Tutorial, Home, Daily, Training, Duel, leaderboard, store; требуется visual/UX polish на устройствах |
| RuStore platform services | 7% | 72% | Review, Update, Referrer готовы; Push permission flow feature-flagged, Push delivery SDK ещё не включён |
| Production/release verification | 8% | 42% | Manifest/preflight/SDK guard готовы; нет production package/backend/signing/AAB/device smoke test/crash reporting |

Взвешенная оценка получается около 82%. Для внутренней feature/code completeness оценка выше — примерно 89%, но её нельзя использовать как «готово к публикации».

## Уже закрытые release-critical требования

- Unity `6000.3.24f1`, portrait, IL2CPP/ARM64 foundation.
- `UnityPlayerActivity` для Pay и production preflight против `GameActivity`.
- Pay SDK `11.1.0`, Install Referrer `10.6.1`, Update/Review `10.5.1`.
- Актуальный RuStore npm registry и CI guard против старых repository/BillingClient references.
- Детерминированный `seed + generatorVersion` и server-side score recalculation.
- Anonymous player, Daily leaderboard, challenge/referral и восстановление same challenge.
- SaveData migrations, offline Daily queue, offline Training/settings/economy state.
- Fail-closed покупка: entitlement выдаётся только после server verification RuStore invoice; повторный invoice/purchase нельзя присвоить другому player.
- Review после positive event и Update/minSupportedVersion только из safe Home state.
- Android 13 notification permission отделён интерфейсом, показывается только после Daily и только при `push_enabled=true`.

## Что блокирует 100%

1. Подставить production package name, HTTPS backend URL и RuStore Console application IDs.
2. Настроить `RUSTORE_PUBLIC_TOKEN` и `RUSTORE_APP_ID`; прогнать Pay sandbox и реальную покупку/restore на Android.
3. Подключить конкретный rewarded/interstitial provider и проверить lifecycle/callbacks на устройстве.
4. Подключить RuStore Push Unity после проверки `RuStoreUnityActivity` + `UnityPlayerActivity`; проверить tap/deeplink/Pay совместно. Push Unity на дату проверки документации — `6.3.0`, не Kotlin/Java `7.4.0`.
5. Добавить production crash reporting.
6. Открыть проект в Unity `6000.3.24f1`, прогнать EditMode tests и устранить возможные compile/API проблемы packages.
7. Собрать signed AAB, пройти production preflight, установить на реальные Android 7/13+/целевые устройства и выполнить smoke/regression checklist.
8. Финальный UX/polish: safe areas, разные DPI/aspect ratios, accessibility/readability, плохая сеть, back button, interrupted app lifecycle.

PR #1 остаётся Draft до выполнения release blockers.
