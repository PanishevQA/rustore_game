# RuStore SDK matrix — 2026-09-17

Сверено с актуальными официальными RuStore Unity-страницами на дату выше. Перед **каждой** production-сборкой версии и repository requirements нужно проверить ещё раз: документация и registry могут обновляться независимо.

| SDK | Release target | Состояние проекта |
|---|---:|---|
| Pay Unity | 11.1.0 | release target сохранён; пакет временно исключён из локального Editor baseline вместе с остальными RuStore SDK; production защищён `RuStorePayReleaseContractValidator` |
| Install Referrer Unity | 10.6.1 | release target сохранён, но UPM package временно исключён из Editor baseline из-за compile regression на Unity 6000.3.24f1; reflection-adapter остаётся offline-safe; production preflight требует вернуть и проверить SDK перед release |
| Update Unity | 10.5.1 | release target сохранён; локальный adapter работает как безопасный no-op до восстановления SDK перед production |
| Review Unity | 10.5.1 | установлен; запрос только после positive event |
| GameCenter Unity | 10.5.2 | optional, не source of truth; package не нужен для базового gameplay |
| Remote Config Unity | 10.5.1 | release target сохранён, но UPM package временно исключён из Editor baseline из-за compile regression на Unity 6000.3.24f1; adapter/cache/default работают с safe fallback; production требует восстановить SDK, задать AppId и пройти Android test |
| Push Unity | — | **не используется в MVP**; Daily reminder реализован локально через Unity Mobile Notifications, поэтому Push package/version не pin-ится |

## Baseline verification

`RuStoreSdkVersions.LastVerifiedUtc` должен совпадать с датой этой матрицы. CI проверяет дату, release targets и обязательные compile-safe installed packages как единый baseline. Install Referrer и Remote Config остаются production targets, но не входят в Editor baseline до устранения подтверждённой compile regression.

На 2026-09-17 официальные Unity-источники подтверждают следующие release targets: Pay `11.1.0`, Install Referrer `10.6.1`, Update `10.5.1`, Review `10.5.1`, GameCenter `10.5.2`, Remote Config `10.5.1`.

## Android baseline

Официальные RuStore Unity-инструкции для Update/Install Referrer/Remote Config указывают Minimum API 24 и Target API 34. Unity 6000.3 уже не поддерживает API 24 как рабочий baseline, поэтому проект использует **minSdk 25** и **targetSdk 34 / highest installed**. Production preflight защищает эту комбинацию.

`UnityPlayerActivity` остаётся обязательным baseline для текущего Pay integration. Любое добавление activity-wrapper из другого SDK требует повторного device smoke-test Pay + deeplink + lifecycle.

## Репозитории и package integration

Текущий Editor baseline сохраняет scoped registry `https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`, но не устанавливает RuStore packages, чтобы локальная разработка и тестирование не блокировались package compile regression. Maven URL остаётся в release constants. В официальной документации отдельных SDK всё ещё встречаются переходные registry URL, поэтому адрес нельзя менять по одному примеру из одной страницы.

Registry меняется только после проверки **конкретных pinned packages**, успешного package resolve в закреплённой версии Unity и полного regression-test. Старый `artifactory-external.vkpartner.ru` запрещён.

## Install Referrer

Актуальная Unity-линия — 10.6.1. На 2026-09-18 пакет временно исключён из `Packages/manifest.json`, потому что его текущая UPM-разрешённая сборка дала compile errors внутри `Library/PackageCache` на закреплённом Unity 6000.3.24f1. Production preflight намеренно остаётся строгим и не позволит выпустить сборку без восстановленного и проверенного SDK. RuStore принимает install URL вида `https://www.rustore.ru/catalog/app/<package>?referrerId=<value>`. Referrer одноразовый: после успешного чтения приложение должно сразу сохранить `referrerId`; невыданный referrer хранится ограниченное время. Для serverless challenge `referrerId` содержит self-contained challenge token.

## Remote Config

Актуальная официальная Unity-линия на дату проверки — **10.5.1**. На 2026-09-18 пакет временно исключён из `Packages/manifest.json` после compile errors внутри текущей UPM package сборки на Unity 6000.3.24f1. Runtime adapter/cache/default остаются и дают безопасный offline fallback. Production preflight по-прежнему требует реально загруженный `RuStoreRemoteConfigClient`, заданный AppId и Android fallback/device test.

Runtime использует один `RuStoreRemoteConfigRuntime` instance. Gameplay tuning, review/update policy и локальные Daily reminders читают один общий snapshot/cache; при Unity Play без Domain Reload provider пересоздаётся на `SubsystemRegistration`.

## Notifications

RuStore Push не нужен для MVP: Daily reminder планируется локально на устройстве. На Android 13+ `POST_NOTIFICATIONS` запрашивается контекстно только после того, как пользователь увидел ценность Daily Challenge.
