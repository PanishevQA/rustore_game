# RuStore SDK matrix — 2026-09-16

Сверено с актуальными официальными RuStore Unity-страницами на дату выше. Перед **каждой** production-сборкой версии и repository requirements нужно проверить ещё раз: документация и registry могут обновляться независимо.

| SDK | Release target | Состояние проекта |
|---|---:|---|
| Pay Unity | 11.1.0 | установлен в `Packages/manifest.json`; старый Billing SDK/BillingClient integration запрещён |
| Install Referrer Unity | 10.6.1 | актуальный release target; Editor package временно не установлен из-за compile-regression package source на Unity 6.3; adapter/reflection fallback сохранён; перед release обязателен официальный package + Android device test |
| Update Unity | 10.5.1 | установлен; adapter реализован |
| Review Unity | 10.5.1 | установлен; запрос только после positive event |
| GameCenter Unity | 10.5.2 | optional, не source of truth; package не нужен для базового gameplay |
| Remote Config Unity | 10.5.0 | официальная Unity-страница RuStore помечает 10.5.0 как актуальную версию; adapter/cache/default реализованы; Editor package временно не установлен; перед production нужен официальный package/AppId + Android test |
| Push Unity | — | **не используется в MVP**; Daily reminder реализован локально через Unity Mobile Notifications, поэтому Push package/version не pin-ится |

## Android baseline

Официальные RuStore Unity-инструкции для Update/Install Referrer/Remote Config указывают Minimum API 24 и Target API 34. Unity 6000.3 уже не поддерживает API 24 как рабочий baseline, поэтому проект использует **minSdk 25** и **targetSdk 34 / highest installed**. Production preflight защищает эту комбинацию.

`UnityPlayerActivity` остаётся обязательным baseline для текущего Pay integration. Любое добавление activity-wrapper из другого SDK требует повторного device smoke-test Pay + deeplink + lifecycle.

## Репозитории и package integration

Текущий Editor baseline использует scoped registry `https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`, и Maven `https://nexus-external.rustore.ru/repository/maven-rustore-exposed` в release constants. Актуальные GitFlic-ветки Pay/Update уже показывают новый `rustore.ru` npm registry; часть help/readme-страниц других SDK ещё содержит переходный `vkteam` URL.

Поэтому registry нельзя менять по одному кэшированному примеру. Непосредственно перед release нужно проверить **конкретный SDK release**, фактический package resolve и актуальные RuStore migration notices. Старый `artifactory-external.vkpartner.ru` запрещён.

## Install Referrer

Актуальная Unity-линия — 10.6.1. RuStore принимает install URL вида `https://www.rustore.ru/catalog/app/<package>?referrerId=<value>`. Referrer одноразовый: после успешного чтения приложение должно сразу сохранить `referrerId`; невыданный referrer хранится ограниченное время. Для serverless challenge `referrerId` содержит self-contained challenge token.

## Remote Config

Актуальная официальная Unity-страница на дату проверки — **10.5.0**. Production preflight не считает adapter/fallback достаточной интеграцией: перед AAB должен быть реально загружен официальный `RuStoreRemoteConfigClient`, задан AppId и выполнен Android fallback/device test.

Runtime использует один `RuStoreRemoteConfigRuntime` instance. Gameplay tuning, review/update policy и локальные Daily reminders читают один общий snapshot/cache; при Unity Play без Domain Reload provider пересоздаётся на `SubsystemRegistration`.

## Notifications

RuStore Push не нужен для MVP: Daily reminder планируется локально на устройстве. На Android 13+ `POST_NOTIFICATIONS` запрашивается контекстно только после того, как пользователь увидел ценность Daily Challenge.
