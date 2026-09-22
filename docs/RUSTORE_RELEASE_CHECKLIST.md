# Production release checklist

## SDK / Android

- [ ] Повторно сверены версии всех RuStore SDK с официальной документацией непосредственно перед production build.
- [ ] Unity Editor — `6000.3.24f1` или более новый проверенный патч той же LTS-линии после smoke/regression теста.
- [ ] `PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity`.
- [ ] Pay работает через `com.unity3d.player.UnityPlayerActivity`, не GameActivity.
- [ ] Remote Config 10.5.1 инициализируется через `RuStoreRemoteConfigClientSettings` с production AppId; кастомный Android `Application` class для выбранного C# init-пути не требуется.
- [ ] minSdk = **25** (Unity 6.3 уже не поддерживает API 24); targetSdk = 34 либо highest installed после повторной сверки RuStore requirements.
- [ ] Реальный Package Name полностью совпадает с приложением в RuStore Console.
- [ ] PayClient Settings содержит корректные `consoleApplicationId` и уникальный deeplink scheme.
- [ ] Выполнены PayClient `Patch Manifest` и `Verify Manifest`/эквивалентные проверки актуального SDK.
- [ ] Цена в UI берётся только из каталога Pay SDK.
- [ ] Старый Billing SDK/BillingClient integration отсутствует.
- [ ] Scoped npm registry — `https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/`; Maven registry — `https://nexus-external.vkteam.ru/repository/maven-rustore-exposed`; старые `artifactory-external.vkpartner.ru` и `nexus-external.rustore.ru` отсутствуют.
- [ ] `OptionalBackendBaseUrl` остаётся пустым: production MVP не требует собственного backend/DB.
- [ ] Production preflight проходит без placeholder package name, пустого Remote Config App ID и demo ad IDs.
- [ ] Release build — AAB, IL2CPP, ARM64, положительный versionCode.
- [ ] Включён custom production keystore и выбран production key alias; debug/default signing не используется.
- [ ] Keystore/passwords не закоммичены в репозиторий.

## Install Referrer / Remote Config

- [ ] Unity package `ru.rustore.installreferrer` не добавлен рядом с Remote Config 10.5.1: совместный npm/tarball probe подтвердил cross-package duplicate `.meta` GUID.
- [ ] В production Android integration установлен официальный **Install Referrer Android 10.6.1** через EDM4U/native bridge; `ru.rustore.core` разрешается транзитивно, без отдельного direct pin.
- [ ] После установки Install Referrer проверены Android dependency resolve, IL2CPP stripping и physical-device `getInstallReferrerV2()`/referral flow.
- [ ] `referrerId` сохраняется сразу после первого успешного чтения: RuStore выдаёт его одноразово и хранит ограниченное время.
- [ ] RuStore install URL использует официальный формат `https://www.rustore.ru/catalog/app/<package>?referrerId=<value>`.
- [ ] В `RuStoreRemoteConfigSettings.AppId` указан реальный App ID из RuStore Console.
- [ ] Production integration использует официальный Remote Config package версии, повторно проверенной перед release; текущий проверенный Unity target — **10.5.1** (актуальная версия, повторно сверена 2026-09-22).
- [ ] `RuStoreRemoteConfigRuntime` является единственным shared runtime provider; gameplay tuning, ads и platform policy читают один snapshot/cache.
- [ ] В RuStore Console заведены ключи с корректными типами: `route_display_time_easy_ms`, `route_display_time_medium_ms`, `route_display_time_hard_ms`, `daily_route_count`, `rewarded_enabled`, `interstitial_enabled`, `interstitial_min_rounds`, `interstitial_cooldown_sec`, `share_copy_variant`, `review_min_sessions`, `local_daily_reminder_enabled`, `daily_reminder_hour`, `store_offer_variant`, `min_supported_version`, `recommended_version`.
- [ ] Четыре gameplay-ключа Daily (`daily_route_count` и три `route_display_time_*`) настроены **глобально без audience targeting/A-B сегментации**; иначе пользователи одного UTC-дня могут получить разные условия.
- [ ] `daily_route_count` принимает только 1–3; клиент валидирует значение и применяет безопасный default 3.
- [ ] Display-time Remote Config меняет только время показа, но не геометрию `seed + generatorVersion`.
- [ ] Display-time values вне 750–10000 мс не применяются; используются безопасные defaults/cache.
- [ ] Без сети/без RuStore Remote Config Campaign, Daily, Training, сохранения и локальная экономика продолжают работать.
- [ ] Проверено, что runtime config не обращается к developer-owned `/config/bootstrap`.

## Pay / economy — без собственного сервера

- [ ] Покупки идут только через актуальный RuStore Pay SDK; target проекта — Pay 11.1.0 на дату последней сверки.
- [ ] Consumable выдаётся только после completed-result RuStore с непустым `purchaseId`; `ProcessedPurchaseIds` защищает от повторной локальной выдачи.
- [ ] Non-consumable после покупки выдаётся только когда `GetPurchases` подтверждает ownership.
- [ ] `GetPurchases` восстанавливает non-consumable entitlements после переустановки/очистки локального save.
- [ ] `remove_ads` действительно подавляет interstitial.
- [ ] `hints_10` даёт ровно 10 расходуемых подсказок.
- [ ] Neon/Retro/Gold cosmetics реально доступны для выбора только после ownership/grant.
- [ ] Campaign coins не подменяют реальные цены RuStore: они используются только в локальном обмене `30 coins → 1 hint`.
- [ ] Campaign reward idempotency проверена: старые звёзды и повтор главы нельзя фармить повторно.
- [ ] Протестированы Pay success/cancel/error и restore на физическом устройстве с RuStore.
- [ ] Проверено, что отсутствие RuStore/сети не блокирует основной gameplay.
- [ ] Принят риск offline-MVP: без собственного backend защита consumable-покупок и игровых результатов от модифицированного клиента слабее, чем при server-side verification.

## Advertising

- [ ] В `Packages/manifest.json` закреплён официальный Yandex Mobile Ads Unity plugin **8.4.0** через upstream Git tag `#8.4.0` (повторно сверено 2026-09-18; перед production всё равно перепроверить актуальную официальную версию).
- [ ] `Game.Monetization.asmdef` ссылается на `YandexMobileAds` и автоматически включает `YANDEX_MOBILE_ADS` только для проверенной линии `[8.4.0,8.5.0)`; ручной глобальный define не требуется.
- [ ] EDM4U закреплён на `v1.2.188`; production build успешно выполнил встроенный synchronous Force Resolve. При ошибке resolver release обязан блокироваться.
- [ ] Production build сгенерировал `Custom Main Gradle Template`, `Custom Gradle Properties Template` и `Custom Gradle Settings Template` из текущего Unity Editor; preflight подтвердил Yandex Android dependency `8.4.0` в resolved template.
- [ ] В `YandexMobileAdsSettings` указаны реальные rewarded/interstitial `R-M-...` IDs.
- [ ] Demo IDs отсутствуют в production build.
- [ ] Rewarded выдаёт +1 hint только после подтверждённого reward callback.
- [ ] Rewarded offer не перекрывает Home CTA и показывается только как opt-in действие.
- [ ] Interstitial невозможен во время route display/drawing/result/share/store/purchase.
- [ ] Ошибка показа interstitial не сбрасывает frequency cap и не запускает cooldown.
- [ ] `remove_ads` и `starter_pack` отключают interstitial.
- [ ] Frequency cap проверен на реальном устройстве.

## Campaign / Training

- [ ] В каталоге ровно 60 детерминированных уровней / 6 глав по 10 уровней.
- [ ] Один levelNumber + generatorVersion всегда создаёт одинаковую geometry на разных устройствах.
- [ ] Порог звёзд стабилен: 60% / 80% / 95% → 1 / 2 / 3 stars.
- [ ] Результат хуже личного рекорда не уменьшает best score или stars.
- [ ] Следующий уровень открывается только после минимум одной звезды; нельзя открыть заблокированный уровень прямым вызовом UI.
- [ ] Новая звезда даёт 5 coins только один раз; улучшение 1→3 stars выдаёт только разницу.
- [ ] Первое прохождение уровня 10/20/30/40/50/60 выдаёт +1 hint только один раз.
- [ ] Уровень 60 показывает завершение кампании и общий star progress.
- [ ] Chapter/level menu сохраняет locks, best score, stars, coins/hints после restart.
- [ ] Training имеет явный выбор Easy / Medium / Hard / Random.
- [ ] Campaign/Training полностью работают без сети и не содержат прямых Network/API зависимостей.
- [ ] Campaign PNG share-card подписана как Campaign/Level, а не как internal Training mode.

## Viral loop — без собственного сервера

- [ ] Share содержит `nesbeisya://challenge/<token>` для установленной игры.
- [ ] Share содержит RuStore install URL с тем же token в `referrerId` для нового пользователя.
- [ ] Текущий Challenge token **L4** содержит дату, seed, generatorVersion, `routeCount`, exact display-time profile, score отправителя и 16-bit checksum payload.
- [ ] L1 challenge tokens продолжают открываться как 3 маршрута с default display times.
- [ ] L2 challenge tokens продолжают сохранять routeCount и открываются с default display times.
- [ ] Legacy L3 tokens продолжают сохранять routeCount + exact display-time profile и корректно открываются.
- [ ] Install Referrer читается один раз и сохраняется локально до обработки.
- [ ] После установки приложение восстанавливает тот же duel без обращения к нашей БД.
- [ ] Seed + generatorVersion + routeCount приглашённого challenge совпадают с challenge отправителя.
- [ ] Exact display time каждого маршрута L4 совпадает с challenge отправителя даже после изменения Remote Config на устройстве друга.
- [ ] Reshare legacy L3 challenge сохраняет исходные seed/version/routeCount/display-times, меняет только score нового отправителя и выпускает новый L4 token.
- [ ] Повреждённый/невалидный token отклоняется безопасно и не ломает Home.
- [ ] Single-character mutation текущего L4 payload/checksum отклоняется `OfflineChallengeCodec` и `ReferralLinkParser`.
- [ ] Максимальный трёхмаршрутный L4 token имеет 40 символов и корректно проходит deeplink + Install Referrer путь.

## Daily / gameplay

- [ ] Один UTC день + generatorVersion дают один и тот же deterministic seed на разных устройствах.
- [ ] `daily_route_count` 1/2/3 корректно завершает Daily, локальный replay recalculation и Duel.
- [ ] LastDaily cache сохраняет routeCount **и display-time profile**; offline fallback не меняет условия уже созданного challenge.
- [ ] 1000+ generated routes проходят validator.
- [ ] Golden seed совпадает минимум на двух Android ABI/device.
- [ ] Replay score повторно рассчитывается локально из траектории, client display score не используется как источник расчёта.
- [ ] Пользователь понимает, что Daily использует часы устройства; без backend невозможно надёжно защититься от ручной смены даты/времени.
- [ ] Streak, personal best, статистика, settings и косметика работают без сети.
- [ ] Подсказка используется в Campaign/Training и корректно маркирует assisted Daily; Duel остаётся без подсказок.
- [ ] UI нигде не показывает «синхронизацию» или «отправим позже» в offline-first build.

## Notifications

- [ ] `com.unity.mobile.notifications` закреплён на проверенной released-версии; текущий pin — `2.4.3`.
- [ ] Daily reminder планируется локально на устройстве и не требует Push/backend.
- [ ] `POST_NOTIFICATIONS` на Android 13+ запрашивается только после завершённого Daily и value prompt.
- [ ] Отказ от notification permission не блокирует игру и не вызывает повторный системный prompt автоматически.
- [ ] После разрешения следующий reminder рассчитывается через общий `DailyReminderPolicy`.
- [ ] Если `local_daily_reminder_enabled=false`, запланированный reminder отменяется.

## UX / reliability

- [ ] Нет location/contacts/camera/microphone/SMS/file permissions без необходимости.
- [ ] Portrait UI и safe area проверены минимум на нескольких aspect ratios/DPI.
- [ ] Home CTA не перекрываются rewarded/meta overlays.
- [ ] Android Back закрывает Training selector/Campaign/meta/notification prompt перед выходом или отменой активного раунда.
- [ ] Pause/background во время активного раунда безопасно возвращает пользователя на Home.
- [ ] Result/share переживает системный share sheet/background transition без потери результата.
- [ ] Review вызывается только после позитивного события и без просьбы «5 звёзд».
- [ ] Update проверяется только на safe Home state, не посреди раунда.
- [ ] `LocalCrashLog` пишет ограниченный on-device diagnostics log и не загружает его автоматически.
- [ ] `LocalAnalyticsService` хранит ограниченный on-device журнал и не отправляет данные на наш сервер.
- [ ] Campaign `level_start/level_complete`, Daily, share, rewarded, store и purchase события присутствуют в analytics allow-list.
- [ ] Shared runtime SaveData проверен: покупка/reward/coin exchange не затираются последующим Daily/campaign save.
- [ ] AAB release подписан production key и протестирован минимум на API 25 и Android 13+.
