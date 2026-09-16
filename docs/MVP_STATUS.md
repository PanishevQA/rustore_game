# Состояние проекта — 2026-09-16

> Этот файл больше не использует один общий процент «готовности игры». Предыдущая оценка смешивала почти готовое техническое ядро с ещё не реализованным игровым контентом и поэтому создавала неверное впечатление о полном продукте.

## Что уже реально работает

### Игровое ядро

- детерминированная генерация маршрутов по `seed + generatorVersion`;
- запись траектории и timestamps;
- score `0–100%` с точностью до `0,1%`;
- Tutorial;
- Daily Challenge;
- бесконечная Training;
- friend Duel / challenge token;
- hints, cosmetics, medals, Perfect effect, result-card;
- локальный JSON SaveData с миграциями;
- portrait mobile layout, safe area, Android Back, pause/resume;
- локальные Sound/Haptics settings.

### Локальная кампания — новый блок

Первая полноценная версия кампании реализована в коде:

- **60 локальных уровней**;
- **6 глав по 10 уровней**;
- уровень всегда имеет фиксированные `seed`, `generatorVersion`, difficulty и display time;
- постепенный рост сложности;
- `60% = ★`, `80% = ★★`, `95% = ★★★`;
- следующий уровень открывается после получения хотя бы одной звезды;
- лучший score и максимальное число звёзд уровня сохраняются локально;
- повторное прохождение не может ухудшить сохранённый результат;
- экран выбора уровней показывает замки, звёзды и лучший процент;
- на Home теперь отдельные входы **УРОВНИ**, **DAILY**, **ТРЕНИРОВКА**;
- после уровня доступны следующий уровень / повтор / возврат к выбору уровней;
- Android Back и safe area учитывают экран кампании.

Campaign catalog/progression находятся в pure C# и покрыты unit tests. На текущем этапе **кампания ещё требует ручного Play Mode теста в Unity**.

### Offline-first

Для release-клиента не требуется наш сервер или собственная база данных. Критический прогресс хранится локально. Опциональный `server/` остаётся только заделом для будущих online-функций.

## Текущее состояние по блокам

| Блок | Состояние |
|---|---|
| Core route/scoring/input | Реализовано и покрыто pure tests |
| Campaign 60 levels | Реализовано в коде; нужен ручной Unity UX/progression pass |
| Daily / Training | Реализовано и вручную запускается в Unity |
| Friend Duel / referral token | Реализовано; Android Install Referrer integration ещё требует device pass |
| Save / migrations | **SaveData v13**, включая campaign progress |
| Hints / cosmetics / settings | Реализовано |
| Store / RuStore Pay | Код интеграции готов; нужны RuStore Console credentials и device purchase tests |
| Ads | Adapter/policies готовы; официальный Yandex package + IDs ещё не подключены в production |
| UI | Рабочий тестовый portrait UI; **финальный дизайн ещё не реализован** |
| RuStore Review / Update | Adapter flow реализован; нужен реальный device/store test |
| Install Referrer / Remote Config | Platform adapters сохранены, но проблемные UPM packages временно исключены из Editor manifest для Unity 6.3; перед интеграционным тестом нужно подключить официальные `.tgz` и проверить на Android |
| Release | Нужны production package name, signing, AAB и физические Android-тесты |

## Автоматические проверки

В PR работают пять проверок:

1. `pure-csharp-tests`;
2. `backend-tests` — только для необязательного future backend;
3. `offline-mode-guard`;
4. `rustore-dependency-guard`;
5. `unity-static-validation`.

Campaign rules, star thresholds, unlock progression и SaveData v13 migration входят в pure C# suite. Offline guard также защищает наличие 60-level campaign и её локальную природу.

## Что обязательно проверить вручную сейчас

После `git pull` в Unity:

1. Home показывает **УРОВНИ**, **DAILY**, **ТРЕНИРОВКА**, статистику и магазин.
2. `УРОВНИ` открывает главу 1; уровень 1 доступен, уровни 2–10 заблокированы.
3. Результат `<60%` не открывает следующий уровень.
4. Результат `>=60%` открывает уровень 2.
5. `>=80%` даёт две звезды, `>=95%` — три.
6. Более слабое повторное прохождение не уменьшает лучший score/звёзды.
7. После перезапуска Unity прогресс кампании сохраняется.
8. Переход между главами корректно показывает 10 уровней.
9. Android Back на выборе уровней закрывает меню, а не завершает приложение.
10. Daily/Training/Duel после добавления кампании не получили регрессий.

## Что ещё не является готовым продуктом

### Финальный UI/UX

Текущий интерфейс нужен для функционального тестирования. Перед релизом будет отдельный final design pass: финальная типографика, иконки, campaign map/level cards, result screen, магазин, статистика, настройки, анимации и адаптация под несколько Android aspect ratios.

### RuStore / Android production integration

Остаются реальные внешние шаги:

- production package name;
- keystore/signing;
- RuStore Console Pay configuration;
- официальные Install Referrer / Remote Config packages и их Android smoke test;
- Yandex Mobile Ads package и реальные block IDs;
- Pay purchase/cancel/error/restore на устройстве;
- deeplink/install-referrer test через реальную установку;
- signed AAB;
- device regression минимум на Android API 25+ и Android 13+;
- повторная сверка актуальной документации RuStore непосредственно перед production build.

## Главный следующий этап

Сначала вручную проверить новый цикл кампании **уровень 1 → звезда → unlock уровня 2 → сохранение прогресса**. После стабилизации кампании продолжать наращивать видимый игровой продукт и только затем делать финальный UI/UX pass и production RuStore integration.
