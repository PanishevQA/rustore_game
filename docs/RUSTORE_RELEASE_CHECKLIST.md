# Production release checklist

## SDK / Android

- [ ] Повторно сверены версии всех RuStore SDK с официальной документацией.
- [ ] Unity Editor — актуальный патч 6.3 LTS после smoke/regression теста.
- [ ] `PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity`.
- [ ] Основной класс — `com.unity3d.player.UnityPlayerActivity`, не GameActivity.
- [ ] minSdk = 24; targetSdk перепроверен по текущим требованиям RuStore/Android.
- [ ] Реальный Package Name полностью совпадает с приложением в RuStore Console.
- [ ] PayClient Settings содержит корректные `consoleApplicationId` и уникальный deeplink scheme.
- [ ] Выполнены PayClient `Patch Manifest` и `Verify Manifest`.
- [ ] Цена в UI берётся только из каталога Pay SDK.
- [ ] BillingClient packages отсутствуют.
- [ ] Старые Maven/Artifactory URL отсутствуют.

## Viral loop

- [ ] Link `/c/{referralId}` открывает challenge в установленной игре.
- [ ] Без игры landing ведёт на RuStore URL с `referrerId`.
- [ ] Install Referrer читается на первом запуске и сохраняется до успешной обработки.
- [ ] После установки открывается исходный duel/challenge, а не просто home.

## Gameplay / честность

- [ ] 1000+ generated routes проходят validator.
- [ ] Golden seed совпадает минимум на двух Android ABI/device.
- [ ] Backend пересчитывает score из replay; client score не считается доверенным.
- [ ] assisted Daily попытки не попадают в основной leaderboard.
- [ ] Interstitial невозможен во время route display/drawing/result share/purchase.
- [ ] Offline Training работает без RuStore и сети.

## UX / permissions

- [ ] POST_NOTIFICATIONS запрашивается контекстно после понятной ценности, не на первом экране.
- [ ] Нет location/contacts/camera/microphone/SMS/file permissions без необходимости.
- [ ] Review вызывается только после позитивного события и без просьбы «5 звёзд».
- [ ] AAB release протестирован на API 24 и нескольких современных Android версиях.
