# Архитектура MVP

## Правило зависимостей

`Core <- Gameplay <- Presentation` и `Services <- Platform adapters`.

Gameplay не знает о RuStore, рекламной сети, HTTP, аналитике или MonoBehaviour. Координаты маршрута хранятся в fixed-point (`0..1_000_000`), поэтому генератор не зависит от float-реализации CPU/GPU.

## Модули

| Модуль | Ответственность |
|---|---|
| Core | fixed-point, RNG, базовые value objects |
| Gameplay | RouteGenerator, ScoreCalculator, DailyChallengeFactory |
| Daily | пока входит в Gameplay; server date/leaderboard приходят через IGameApi |
| Social | challenge/referral контракты в Services + backend |
| Economy | entitlements/inventory contracts; production persistence следующим шагом |
| Monetization | IAdService / IPaymentService, gameplay зависит только от интерфейса |
| Analytics | IAnalyticsService |
| Network | IGameApi; Unity HTTP adapter добавляется без изменения gameplay |
| Platform/RuStore | только RuStore-specific код и version matrix |

## Детерминизм маршрута

1. Seed — 31-bit integer, полученный от backend.
2. `generatorVersion` участвует в создании RNG-state.
3. SplitMix64 работает только с целочисленными операциями.
4. Все точки Bezier вычисляются целочисленно с фиксированным количеством samples на сегмент.
5. При изменении алгоритма повышается `generatorVersion`.

Backend содержит зеркальную реализацию и golden-тесты. Старые версии generator должны сохраняться как отдельные стратегии, а не переписываться на месте.

## Score

Score не зависит от скорости, кроме anti-cheat проверок server-side. Состав:

- 70% — среднее расстояние пользовательской траектории до reference polyline;
- 18% — завершённость маршрута;
- 8% — попадание в END;
- 4% — штраф за грубые отклонения.

При прохождении менее 50% маршрута применяется дополнительный penalty. Итог округляется до 0,1%.

## Daily

Production источник истины — backend `GET /daily`, который отдаёт `challengeId`, `seed`, `generatorVersion` и серверное время. Клиент генерирует три маршрута из seed. Для leaderboard отправляется replay всех трёх маршрутов; backend повторно генерирует reference и пересчитывает score.

## Offline

Training работает без сети всегда. Последний Daily и Remote Config должны кэшироваться локально. В этом foundation-коммите клиентский persistent cache оставлен за интерфейсом; backend и pure game rules уже готовы для синхронизации.
