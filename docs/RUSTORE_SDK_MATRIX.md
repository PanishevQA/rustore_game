# RuStore SDK matrix — 2026-09-16

Сверено с актуальными Unity-страницами RuStore на дату выше. Перед **каждой** production-сборкой версии и repository requirements нужно проверить ещё раз: страницы документации/registry могут обновляться независимо.

| SDK | Release target | Состояние проекта |
|---|---:|---|
| Pay Unity | 11.1.0 | установлен в `Packages/manifest.json`; BillingClient запрещён |
| Install Referrer Unity | 10.6.1 | актуальный release target; Editor UPM package временно не установлен из-за compile-regression внутри package source на Unity 6.3; adapter/reflection fallback сохранён; перед release обязателен официальный package + Android device test |
| Update Unity | 10.5.1 | установлен; flexible/immediate/silent adapter |
| Review Unity | 10.5.1 | установлен; запрос только после positive event |
| GameCenter Unity | 10.5.2 | optional, не source of truth; package не нужен для базового gameplay |
| Remote Config Unity | 10.5.0 | adapter/cache/default реализованы; Editor UPM package временно не установлен; перед production нужен официальный package/AppId + Android test |
| Push Unity | 6.3.0 | не входит в MVP release path; Daily reminder реализован локально через Unity Mobile Notifications |

## Android baseline

Официальные RuStore Unity-инструкции для Update/Install Referrer на дату проверки указывают Minimum API 24 и Target API 34. Unity 6000.3 уже помечает API 24 неподдерживаемым, поэтому проект использует **minSdk 25** и **targetSdk 34 / highest installed**. Production preflight защищает эту комбинацию.

`UnityPlayerActivity` остаётся обязательным baseline для текущего Pay integration. Любое добавление activity-wrapper из другого SDK требует повторного device smoke-test Pay + deeplink + lifecycle.

## Репозитории и package integration

Текущий проект использует scoped registry `https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`, и Maven `https://nexus-external.rustore.ru/repository/maven-rustore-exposed` в release constants.

В официальной документации RuStore на момент проверки встречаются страницы с переходными `vkteam` URL, поэтому **не менять repository только по одному старому/кэшированному примеру**. Непосредственно перед release нужно проверить актуальную страницу конкретного Unity SDK и фактический package resolve. Старый `artifactory-external.vkpartner.ru` запрещён; RuStore отдельно предупреждает о его отключении.

## Install Referrer

Актуальная Unity-линия — 10.6.1. Referrer одноразовый: после успешного чтения приложение должно сразу сохранить `referrerId`; RuStore хранит невыданный referrer ограниченное время. Для нашей serverless challenge-схемы `referrerId` содержит self-contained challenge token.

## Notifications

RuStore Push не нужен для MVP: Daily reminder планируется локально на устройстве. На Android 13+ `POST_NOTIFICATIONS` запрашивается контекстно только после того, как пользователь увидел ценность Daily Challenge.
