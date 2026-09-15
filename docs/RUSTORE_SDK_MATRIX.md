# RuStore SDK matrix — 2026-09-15

Проверено по официальной документации перед созданием проекта. Значения нужно пересматривать перед каждой production-сборкой.

| SDK | Зафиксировано | Примечание |
|---|---:|---|
| Pay Unity | 11.1.0 | актуальная ветка; только Pay SDK, BillingClient запрещён |
| Install Referrer Unity | 10.6.1 | referral читать и сохранять сразу после первого успешного запроса |
| Update Unity | 10.5.1 | flexible / immediate / silent |
| Review Unity | 10.5.1 | вызывать после положительного события, без просьбы о конкретной оценке |
| GameCenter Unity | 10.5.2 | optional, не source of truth |
| Remote Config Unity | 10.5.1 | package pin проекта; перед production повторно сверить официальный changelog |
| Push Unity | 6.3.0 | подтверждённая Unity-документация на дату проверки; не путать с Kotlin/Java 7.4.0 |

## Репозитории

UPM/NPM registry: `https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`.

Не возвращать старые `artifactory-external.vkpartner.ru` и `nexus-external.vkteam.ru` адреса.

## Push / Activity compatibility

Unity Push 6.3.0 документирует `RuStoreUnityActivity`, которая принимает push-intent и запускает `UnityPlayerActivity`. Pay 11.1.0 требует работу через `UnityPlayerActivity`. Поэтому Push нельзя включать в production только по факту установки package: после добавления `RuStoreUnityActivity` обязателен Android device smoke-test Pay + push-tap + challenge deeplink. До этого Push остаётся feature-flagged/off.

На Android 13+ уведомления дополнительно требуют runtime-разрешение `POST_NOTIFICATIONS`. Запрос должен показываться только после того, как игрок понял ценность Daily Challenge.
