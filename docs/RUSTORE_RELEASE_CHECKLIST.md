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
- [ ] `OptionalBackendBaseUrl` остаётся пустым: production MVP не требует собственного backend/DB.
- [ ] Production preflight проходит без placeholder package name и без demo ad IDs.

## Pay / economy — без собственного сервера

- [ ] Покупки идут только через актуальный RuStore Pay SDK.
- [ ] Успешная локальная выдача требует completed-result SDK с непустыми `purchaseId`/`invoiceId`.
- [ ] `GetPurchases` восстанавливает non-consumable entitlements после переустановки/очистки локального save.
- [ ] `ProcessedPurchaseIds` не позволяет дважды выдать consumable в рамках сохранённого локального состояния.
- [ ] Протестированы success/cancel/error и restore.
- [ ] Проверено, что отсутствие RuStore/сети не блокирует основной gameplay.
- [ ] Принят риск offline-MVP: без собственного backend защита покупок и результатов от модифицированного клиента слабее, чем при server-side verification.

## Advertising

- [ ] Импортирован официальный Yandex Mobile Ads Unity plugin версии, повторно проверенной перед release.
- [ ] Для текущей интеграции проверена ветка Unity Plugin 8.x; на дату разработки использовалась 8.4.0.
- [ ] В Android Scripting Define Symbols есть `YANDEX_MOBILE_ADS`.
- [ ] В `YandexMobileAdsSettings` указаны реальные rewarded/interstitial `R-M-...` IDs.
- [ ] Demo IDs отсутствуют в production build.
- [ ] Rewarded выдаёт награду только после `OnRewarded`.
- [ ] Interstitial невозможен во время route display/drawing/result/share/store/purchase.
- [ ] `remove_ads` и `starter_pack` отключают interstitial.
- [ ] Frequency cap проверен на реальном устройстве.

## Viral loop — без собственного сервера

- [ ] Share содержит `nesbeisya://challenge/<token>` для установленной игры.
- [ ] Share содержит RuStore install URL с тем же token в `referrerId` для нового пользователя.
- [ ] Challenge token содержит дату, seed, generatorVersion и score отправителя.
- [ ] Install Referrer читается один раз и сохраняется локально до обработки.
- [ ] После установки приложение восстанавливает тот же duel без обращения к нашей БД.
- [ ] Seed + generatorVersion приглашённого challenge совпадают с challenge отправителя.
- [ ] Повреждённый/невалидный token отклоняется безопасно и не ломает Home.

## Daily / gameplay

- [ ] Один UTC день + generatorVersion дают один и тот же deterministic seed на разных устройствах.
- [ ] 1000+ generated routes проходят validator.
- [ ] Golden seed совпадает минимум на двух Android ABI/device.
- [ ] Replay score повторно рассчитывается локально из траектории, client display score не используется как источник расчёта.
- [ ] Пользователь понимает, что Daily использует часы устройства; без backend невозможно надёжно защититься от ручной смены даты/времени.
- [ ] Streak, personal best, статистика, settings и косметика работают без сети.
- [ ] Offline Training работает без RuStore и сети.

## Push / notifications

- [ ] Перед включением Push повторно сверена актуальная **Unity** версия SDK; не использовать Kotlin/Java version number как Unity package version.
- [ ] `push_enabled` остаётся false, пока RuStore Push project/signature не настроены и не проверены.
- [ ] Совместно протестированы Activity, Pay, push tap и challenge deeplink на реальном устройстве.
- [ ] POST_NOTIFICATIONS на Android 13+ запрашивается только после завершённого Daily и value prompt.
- [ ] Отказ от notification permission не блокирует игру и не вызывает повторный системный prompt автоматически.

## UX / reliability

- [ ] Нет location/contacts/camera/microphone/SMS/file permissions без необходимости.
- [ ] Review вызывается только после позитивного события и без просьбы «5 звёзд».
- [ ] Update проверяется только на safe Home state, не посреди раунда.
- [ ] Локальная статистика и Store корректно переживают отсутствие сети.
- [ ] Android Back закрывает meta/notification prompt перед выходом или отменой активного раунда.
- [ ] Pause/background во время активного раунда безопасно возвращает пользователя на Home.
- [ ] Safe area, DPI/aspect ratios и читаемость проверены на нескольких устройствах.
- [ ] `LocalCrashLog` пишет ограниченный on-device diagnostics log и не загружает его автоматически.
- [ ] `LocalAnalyticsService` хранит ограниченный on-device журнал и не отправляет данные на наш сервер.
- [ ] AAB release подписан production key и протестирован минимум на API 24 и Android 13+.
