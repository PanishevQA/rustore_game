# Production release checklist

## SDK / Android

- [ ] Повторно сверены версии всех RuStore SDK с официальной документацией.
- [ ] Unity Editor — актуальный патч 6.3 LTS после smoke/regression теста.
- [ ] `PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity`.
- [ ] Pay работает через `com.unity3d.player.UnityPlayerActivity`, не GameActivity.
- [ ] minSdk = 24; targetSdk перепроверен по текущим требованиям RuStore/Android.
- [ ] Реальный Package Name полностью совпадает с приложением в RuStore Console.
- [ ] PayClient Settings содержит корректные `consoleApplicationId` и уникальный deeplink scheme.
- [ ] Выполнены PayClient `Patch Manifest` и `Verify Manifest`.
- [ ] Цена в UI берётся только из каталога Pay SDK.
- [ ] BillingClient packages отсутствуют.
- [ ] Старые Maven/Artifactory/NPM URL отсутствуют.
- [ ] Production preflight проходит без placeholder package/backend значений.

## Pay / economy

- [ ] Backend настроен с `RUSTORE_PUBLIC_TOKEN` и числовым `RUSTORE_APP_ID`; секреты не лежат в git/client build.
- [ ] Для sandbox явно выставлен `RUSTORE_PAY_SANDBOX=true`, для production — false/отсутствует.
- [ ] Entitlement не выдаётся по одному client callback: backend повторно проверяет RuStore invoice по `invoiceId`.
- [ ] Проверяются `appId`, `invoiceStatus == CONFIRMED`, `order.itemCode`, `purchaseId`.
- [ ] Повторный invoice/purchase идемпотентен для того же player и отклоняется для другого player/product.
- [ ] Протестированы success/cancel/error, повторный callback, потеря сети после оплаты и restore после reinstall.
- [ ] Consumable `hints_10` не выдаётся дважды для одного `purchaseId`.

## Viral loop

- [ ] Link `/c/{referralId}` открывает challenge в установленной игре.
- [ ] Без игры landing ведёт на RuStore URL с `referrerId`.
- [ ] Install Referrer читается на первом запуске и сохраняется до успешной обработки.
- [ ] После установки открывается исходный duel/challenge, а не просто home.
- [ ] Seed + generatorVersion приглашённого challenge совпадают с challenge отправителя.

## Gameplay / честность

- [ ] 1000+ generated routes проходят validator.
- [ ] Golden seed совпадает минимум на двух Android ABI/device.
- [ ] Backend пересчитывает score из replay; client score не считается доверенным.
- [ ] assisted Daily попытки не попадают в основной leaderboard.
- [ ] Interstitial невозможен во время route display/drawing/result share/purchase.
- [ ] Offline Training работает без RuStore и сети.
- [ ] Pending offline Daily replay корректно отправляется после восстановления сети.

## Push / notifications

- [ ] Перед включением Push повторно сверена актуальная **Unity** версия SDK; не использовать Kotlin/Java version number как Unity package version.
- [ ] `push_enabled` остаётся false, пока RuStore Push project/signature не настроены и не проверены.
- [ ] Совместно протестированы `RuStoreUnityActivity`/`UnityPlayerActivity`, Pay, push tap и challenge deeplink на реальном устройстве.
- [ ] POST_NOTIFICATIONS на Android 13+ запрашивается только после завершённого Daily и value prompt.
- [ ] Отказ от notification permission не блокирует игру и не вызывает повторный системный prompt автоматически.

## UX / reliability

- [ ] Нет location/contacts/camera/microphone/SMS/file permissions без необходимости.
- [ ] Review вызывается только после позитивного события и без просьбы «5 звёзд».
- [ ] Mandatory Update блокирует устаревший client только на safe UI state, не посреди раунда.
- [ ] Store/leaderboard корректно работают при offline/timeout и не блокируют gameplay.
- [ ] Back button, pause/resume, background/foreground, screen lock и process recreation проверены на Android.
- [ ] Safe area, DPI/aspect ratios и читаемость проверены на нескольких устройствах.
- [ ] Production crash reporting подключён и проверен тестовым exception без персональных данных.
- [ ] Analytics события не содержат секреты, токены, полный replay без необходимости или PII.
- [ ] AAB release подписан production key и протестирован минимум на API 24 и Android 13+.
