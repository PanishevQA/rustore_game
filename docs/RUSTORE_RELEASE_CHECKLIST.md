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
- [ ] Production preflight проходит без placeholder package name, пустого Remote Config App ID и demo ad IDs.
- [ ] Release build — AAB, IL2CPP, ARM64, положительный versionCode.
- [ ] Включён custom production keystore и выбран production key alias; debug/default signing не используется.
- [ ] Keystore/passwords не закоммичены в репозиторий.

## Remote Config — RuStore, без собственного backend

- [ ] В `RuStoreRemoteConfigSettings.AppId` указан реальный App ID инструмента Remote Config из RuStore Console.
- [ ] `ru.rustore.remoteconfig` закреплён на версии, повторно проверенной перед production; на дату разработки — `10.5.1`.
- [ ] В RuStore Console заведены ключи с корректными типами: `route_display_time_easy_ms`, `route_display_time_medium_ms`, `route_display_time_hard_ms`, `daily_route_count`, `rewarded_enabled`, `interstitial_enabled`, `interstitial_min_rounds`, `interstitial_cooldown_sec`, `share_copy_variant`, `review_min_sessions`, `local_daily_reminder_enabled`, `daily_reminder_hour`, `store_offer_variant`, `min_supported_version`, `recommended_version`.
- [ ] `daily_route_count` принимает только 1–3; клиент валидирует значение и применяет безопасный default 3.
- [ ] Display-time Remote Config меняет только время показа, но не геометрию `seed + generatorVersion`.
- [ ] Display-time values вне 750–10000 мс не применяются; используются безопасные defaults/cache.
- [ ] Без сети/без RuStore Remote Config основной gameplay, Training, сохранения и магазин продолжают запускаться.
- [ ] Проверено, что runtime config не обращается к developer-owned `/config/bootstrap`.

## Pay / economy — без собственного сервера

- [ ] Покупки идут только через актуальный RuStore Pay SDK.
- [ ] Consumable выдаётся только после completed-result RuStore с непустым `purchaseId`; `ProcessedPurchaseIds` защищает от повторной локальной выдачи.
- [ ] Non-consumable после покупки выдаётся только когда `GetPurchases` подтверждает ownership.
- [ ] `GetPurchases` восстанавливает non-consumable entitlements после переустановки/очистки локального save.
- [ ] Протестированы success/cancel/error и restore.
- [ ] Проверено, что отсутствие RuStore/сети не блокирует основной gameplay.
- [ ] Принят риск offline-MVP: без собственного backend защита consumable-покупок и игровых результатов от модифицированного клиента слабее, чем при server-side verification.

## Advertising

- [ ] Импортирован официальный Yandex Mobile Ads Unity plugin версии, повторно проверенной перед release.
- [ ] Для текущей интеграции проверена ветка Unity Plugin 8.x; на дату разработки использовалась 8.4.0.
- [ ] В Android Scripting Define Symbols есть `YANDEX_MOBILE_ADS`.
- [ ] В `YandexMobileAdsSettings` указаны реальные rewarded/interstitial `R-M-...` IDs.
- [ ] Demo IDs отсутствуют в production build.
- [ ] Rewarded выдаёт награду только после `OnRewarded`.
- [ ] Interstitial невозможен во время route display/drawing/result/share/store/purchase.
- [ ] Ошибка показа interstitial не сбрасывает frequency cap и не запускает cooldown.
- [ ] `remove_ads` и `starter_pack` отключают interstitial.
- [ ] Frequency cap проверен на реальном устройстве.

## Viral loop — без собственного сервера

- [ ] Share содержит `nesbeisya://challenge/<token>` для установленной игры.
- [ ] Share содержит RuStore install URL с тем же token в `referrerId` для нового пользователя.
- [ ] Challenge token L3 содержит дату, seed, generatorVersion, `routeCount`, exact display-time profile и score отправителя.
- [ ] L1 challenge tokens продолжают открываться как 3 маршрута с default display times.
- [ ] L2 challenge tokens продолжают сохранять routeCount и открываются с default display times.
- [ ] Install Referrer читается один раз и сохраняется локально до обработки.
- [ ] После установки приложение восстанавливает тот же duel без обращения к нашей БД.
- [ ] Seed + generatorVersion + routeCount приглашённого challenge совпадают с challenge отправителя.
- [ ] Exact display time каждого маршрута L3 совпадает с challenge отправителя даже после изменения Remote Config на устройстве друга.
- [ ] Reshare уже полученного L3 challenge сохраняет исходные seed/version/routeCount/display-times, меняя только score нового отправителя.
- [ ] Повреждённый/невалидный token отклоняется безопасно и не ломает Home.
- [ ] Максимальный трёхмаршрутный L3 token корректно проходит deeplink + Install Referrer путь.

## Daily / gameplay

- [ ] Один UTC день + generatorVersion дают один и тот же deterministic seed на разных устройствах.
- [ ] `daily_route_count` 1/2/3 корректно завершает Daily, локальный replay recalculation и Duel.
- [ ] LastDaily cache сохраняет routeCount **и display-time profile**; offline fallback не меняет условия уже созданного challenge.
- [ ] 1000+ generated routes проходят validator.
- [ ] Golden seed совпадает минимум на двух Android ABI/device.
- [ ] Replay score повторно рассчитывается локально из траектории, client display score не используется как источник расчёта.
- [ ] Пользователь понимает, что Daily использует часы устройства; без backend невозможно надёжно защититься от ручной смены даты/времени.
- [ ] Streak, personal best, статистика, settings и косметика работают без сети.
- [ ] Offline Training работает без RuStore и сети.
- [ ] UI нигде не показывает «синхронизацию» или «отправим позже» в offline-first build.

## Notifications

- [ ] `com.unity.mobile.notifications` закреплён на проверенной released-версии; на дату разработки — `2.4.3`.
- [ ] Daily reminder планируется локально на устройстве и не требует Push/backend.
- [ ] `POST_NOTIFICATIONS` на Android 13+ запрашивается только после завершённого Daily и value prompt.
- [ ] Отказ от notification permission не блокирует игру и не вызывает повторный системный prompt автоматически.
- [ ] После разрешения следующий reminder рассчитывается через общий `DailyReminderPolicy`.
- [ ] Если `local_daily_reminder_enabled=false`, запланированный reminder отменяется.

## UX / reliability

- [ ] Нет location/contacts/camera/microphone/SMS/file permissions без необходимости.
- [ ] Review вызывается только после позитивного события и без просьбы «5 звёзд».
- [ ] Update проверяется только на safe Home state, не посреди раунда.
- [ ] Локальная статистика и Store корректно переживают отсутствие сети.
- [ ] Android Back закрывает meta/notification prompt перед выходом или отменой активного раунда.
- [ ] Pause/background во время активного раунда безопасно возвращает пользователя на Home.
- [ ] Result/share переживает системный share sheet/background transition без потери результата.
- [ ] Safe area, DPI/aspect ratios и читаемость проверены на нескольких устройствах.
- [ ] `LocalCrashLog` пишет ограниченный on-device diagnostics log и не загружает его автоматически.
- [ ] `LocalAnalyticsService` хранит ограниченный on-device журнал и не отправляет данные на наш сервер.
- [ ] AAB release подписан production key и протестирован минимум на API 24 и Android 13+.
