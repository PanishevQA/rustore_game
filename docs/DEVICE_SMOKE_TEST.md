# Physical-device release smoke protocol

Этот протокол выполняется только на signed non-Development APK, собранном через `SignedDeviceSmokeBuild` / workflow `DeviceSmokeApk`. Публикационный артефакт остаётся production AAB.

## Test record

Перед началом зафиксируйте:

- Git commit SHA;
- app version / Android versionCode;
- SHA-256 APK из соседнего `.sha256`;
- модель устройства;
- Android API level;
- установленную версию RuStore;
- сеть: Wi-Fi/mobile/offline;
- дату/время теста.

Минимальный release matrix: одно устройство уровня **API 25** и одно устройство **Android 13+**.

## 1. Install / first launch

1. Удалите только предыдущую тестовую сборку с несовместимой подписью, если `adb install -r` сообщает signature mismatch.
2. Установите APK через `scripts/install_signed_device_smoke_apk.ps1`.
3. Запустите приложение обычным launcher icon.

PASS:
- package — `ru.release.nesbeisya`;
- приложение запускается без crash/ANR;
- portrait orientation сохраняется;
- первый запуск сразу показывает короткое обучение, без длинного onboarding;
- после обучения открывается Home.

## 2. Core route loop

Пройдите обучение, Daily и Training.

PASS:
- маршрут виден ограниченное время и затем исчезает;
- траектория рисуется одним движением;
- после отпускания показываются reference/player lines;
- score отображается с точностью 0,1%;
- restart/next round не оставляет старые линии;
- Back/pause/background во время раунда безопасно возвращают на Home;
- Training работает в Easy / Medium / Hard / Random.

## 3. Offline behavior

1. Запустите приложение онлайн хотя бы один раз.
2. Включите airplane mode.
3. Перезапустите приложение.

PASS:
- Home, Training, Campaign, settings, statistics, cosmetics и локальные saves доступны;
- Daily использует cache/default без crash;
- отсутствие RuStore/Remote Config не блокирует gameplay;
- UI не обещает несуществующую server synchronization;
- возвращение сети не ломает текущую сессию.

## 4. Daily / deterministic state

PASS:
- Daily корректно завершает 1/2/3 route profile, когда соответствующее значение задаётся config;
- replay/personal best/streak сохраняются после restart;
- assisted Daily помечается корректно;
- Duel остаётся без подсказок;
- уже созданный challenge не меняет seed/routeCount/display times после обновления Remote Config.

## 5. Share / installed deeplink

1. Завершите результат и нажмите share.
2. Проверьте PNG share card и текст.
3. Откройте `nesbeisya://challenge/<token>` на устройстве с установленной игрой.

PASS:
- Android share sheet открывается без permission/error;
- PNG доступна выбранному target через FileProvider;
- после возврата из share sheet результат не теряется;
- installed deeplink открывает нужный challenge;
- malformed token безопасно отклоняется.

## 6. Real RuStore Install Referrer

Этот пункт нельзя заменить sideload APK. Нужна установка через реальный RuStore install URL:

`https://www.rustore.ru/catalog/app/ru.release.nesbeisya?referrerId=<token>`

PASS:
- первый launch после установки получает referrer через native Install Referrer 10.6.1;
- challenge token сохраняется локально до обработки;
- пользователю предлагается тот же challenge;
- повторный launch не выдаёт повторно consumable referrer;
- seed, generatorVersion, routeCount и display-time profile совпадают с исходным challenge.

## 7. RuStore Pay 11.1.0

На устройстве должен быть актуальный RuStore.

Проверьте:
- каталог магазина показывает цену, полученную из SDK;
- purchase success;
- purchase cancel;
- purchase error/network loss;
- restore non-consumable после очистки локального state/reinstall в допустимом тестовом сценарии;
- `remove_ads` отключает interstitial;
- `hints_10` выдаёт ровно 10 hints и не дублирует grant при повторной обработке purchaseId;
- cosmetics выдаются только после подтверждённого ownership.

PASS:
- приложение не зависает на возврате из RuStore;
- payment deeplink возвращает в `UnityPlayerActivity`;
- новая Pay infrastructure работает с текущим 11.1.0 baseline;
- gameplay остаётся доступным при cancel/error.

## 8. Ads

PASS:
- rewarded показывается только по явному действию;
- reward (+1 hint или соответствующая награда) выдаётся только после reward callback;
- failed/cancelled rewarded не выдаёт награду;
- interstitial никогда не появляется во время route display/drawing/result/share/store/purchase;
- frequency cap/cooldown соблюдаются;
- `remove_ads` подавляет interstitial.

## 9. Review / Update

PASS:
- review prompt вызывается только после positive event;
- текст игры не просит конкретное число звёзд;
- отказ/закрытие prompt не блокирует игру;
- Update check происходит только в safe Home state;
- приложение корректно переживает отсутствие update/RuStore/network.

## 10. Notifications

На Android 13+:

PASS:
- `POST_NOTIFICATIONS` не запрашивается на первом экране;
- permission prompt появляется только после того, как пользователь увидел ценность Daily;
- отказ не блокирует игру и не вызывает бесконечные reprompt;
- при разрешении локальный Daily reminder планируется;
- feature flag off отменяет/не создаёт reminder.

## 11. Campaign / economy persistence

PASS:
- locks/stars/best scores/coins/hints сохраняются после restart;
- повтор уровня не фармит уже выданную reward;
- star improvement выдаёт только разницу;
- milestone hint выдаётся один раз;
- exchange `30 coins → 1 hint` изменяет оба значения атомарно;
- store/reward/campaign save не затирают друг друга.

## 12. Lifecycle / UI regression

Проверьте:
- несколько быстрых переходов Home → Daily/Training/Campaign → Back;
- background/foreground;
- screen lock/unlock;
- Android Back;
- share sheet;
- RuStore purchase return;
- rotate attempt (игра должна оставаться portrait).

PASS:
- нет duplicated overlays;
- touch targets остаются доступны;
- safe area корректна;
- нет зависшего input lock;
- нет crash/ANR;
- Home CTA не перекрываются rewarded/meta overlays.

## 13. Release decision

Release candidate считается прошедшим device smoke только если:

- все обязательные PASS выше выполнены;
- Install Referrer проверен через реальную RuStore installation path;
- Pay success/cancel/restore проверены с RuStore;
- минимум API 25 и Android 13+ покрыты;
- не найдено P0/P1 дефектов;
- commit SHA тестируемого APK совпадает с commit, из которого затем собирается production AAB, либо различие документировано и повторно проверено.
