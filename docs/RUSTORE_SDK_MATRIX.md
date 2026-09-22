# RuStore SDK matrix — 2026-09-22

Сверено с актуальными официальными RuStore Unity-страницами на дату выше. Перед **каждой** production-сборкой версии и repository requirements нужно проверить ещё раз: документация и registry могут обновляться независимо.

| SDK | Release target | Состояние проекта |
|---|---:|---|
| Pay Unity | 11.1.0 | установлен из официального npm registry; production дополнительно защищён `RuStorePayReleaseContractValidator` |
| Install Referrer Android | 10.6.1 | подключён как официальный Android artifact через EDM4U/native bridge; Unity package намеренно не ставится рядом с Remote Config из-за подтверждённых duplicate `.meta` GUID |
| Update Unity | 10.5.1 | установлен; adapter реализован |
| Review Unity | 10.5.1 | установлен; запрос только после positive event |
| GameCenter Unity | 10.5.2 | optional, не source of truth; package не нужен для базового gameplay |
| Remote Config Unity | 10.5.1 | установлен из официального Unity npm registry; shared runtime/cache/default реализованы, production требует AppId и device fallback smoke |
| Push Unity | — | **не используется в MVP**; Daily reminder реализован локально через Unity Mobile Notifications, поэтому Push package/version не pin-ится |

## Baseline verification

`RuStoreSdkVersions.LastVerifiedUtc` должен совпадать с датой этой матрицы. CI проверяет дату, exact release targets и установленные feature packages. `ru.rustore.core` должен разрешаться транзитивно и не pin-иться отдельно.

На 2026-09-22 официальные Unity-источники подтверждают следующие release targets: Pay `11.1.0`, Install Referrer `10.6.1`, Update `10.5.1`, Review `10.5.1`, GameCenter `10.5.2`, Remote Config `10.5.1`.

## Android baseline

Официальные RuStore Unity-инструкции для Update/Install Referrer/Remote Config указывают Minimum API 24 и Target API 34. Unity 6000.3 уже не поддерживает API 24 как рабочий baseline, поэтому проект использует **minSdk 25** и **targetSdk 34 / highest installed**. Production preflight защищает эту комбинацию.

`UnityPlayerActivity` остаётся обязательным baseline для текущего Pay integration. Любое добавление activity-wrapper из другого SDK требует повторного device smoke-test Pay + deeplink + lifecycle.

## Репозитории и package integration

Текущий Editor baseline использует scoped Unity registry `https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`, для Pay 11.1.0, Remote Config 10.5.1, Update 10.5.1 и Review 10.5.1. Install Referrer 10.6.1 подключён отдельно как официальный Android artifact `ru.rustore.sdk:installreferrer:10.6.1` через Maven `https://nexus-external.rustore.ru/repository/maven-rustore-exposed`, потому что совместная установка Unity-пакетов Install Referrer 10.6.1 и Remote Config 10.5.1 подтверждённо даёт cross-package duplicate `.meta` GUID. `ru.rustore.core` напрямую не pin-ится.

Registry меняется только после проверки **конкретных pinned packages**, успешного package resolve в закреплённой версии Unity и полного regression-test. Старый `artifactory-external.vkpartner.ru` запрещён.

Pay 11.1.0 также входит в обязательную проверку инфраструктуры сентября 2026: перед production/device smoke необходимо подтвердить работу новой платежной инфраструктуры `paymentBaseUrl = api-m.rustore.ru`.

## Install Referrer

Актуальная линия — 10.6.1. Isolated Unity package probe и Android integration build уже подтвердили артефакт; production baseline использует нативный Android bridge, чтобы избежать collision с Remote Config Unity package. Перед релизом остаётся physical-device test реальной установки → первого запуска → `getInstallReferrerV2()` → восстановления challenge. RuStore принимает install URL вида `https://www.rustore.ru/catalog/app/<package>?referrerId=<value>`. Referrer одноразовый: после успешного чтения приложение сразу сохраняет `referrerId`; невыданный referrer хранится ограниченное время.

## Remote Config

Актуальная официальная Unity-линия на дату проверки — **10.5.1**. Package успешно прошёл isolated Unity compile probe и входит в production baseline. Инициализация выполняется через `RuStoreRemoteConfigClientSettings` с production AppId; runtime adapter/cache/default сохраняют безопасный offline fallback. Production preflight требует реально загруженный `RuStoreRemoteConfigClient`, заданный AppId и Android fallback/device test.

Runtime использует один `RuStoreRemoteConfigRuntime` instance. Gameplay tuning, review/update policy и локальные Daily reminders читают один общий snapshot/cache; при Unity Play без Domain Reload provider пересоздаётся на `SubsystemRegistration`.

## Notifications

RuStore Push не нужен для MVP: Daily reminder планируется локально на устройстве. На Android 13+ `POST_NOTIFICATIONS` запрашивается контекстно только после того, как пользователь увидел ценность Daily Challenge.
