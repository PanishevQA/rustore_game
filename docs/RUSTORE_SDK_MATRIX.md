# RuStore SDK matrix — 2026-09-15

Проверено по официальной документации перед созданием проекта. Значения нужно пересматривать перед каждой production-сборкой.

| SDK | Зафиксировано | Примечание |
|---|---:|---|
| Pay Unity | 11.1.0 | актуальная ветка; только Pay SDK, BillingClient запрещён |
| Install Referrer Unity | 10.6.1 | referral хранится RuStore ограниченное время, читать на первом запуске |
| Update Unity | 10.5.1 | flexible/forced/silent |
| Review Unity | 10.5.1 | вызывать после положительного события |
| GameCenter Unity | 10.5.2 | optional, не source of truth |
| Remote Config Unity | 10.5.0 | текущая Unity-страница документации на дату проверки |
| Push Unity | 6.10.0 | последняя явно найденная Unity-документация при проверке; **перепроверить в Package Manager перед включением** |

## Репозитории

UPM/NPM registry: `https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/`, scope `ru.rustore`.

Не возвращать старые `artifactory-external.vkpartner.ru` адреса. В документации RuStore есть уведомление об отключении старой инфраструктуры.
