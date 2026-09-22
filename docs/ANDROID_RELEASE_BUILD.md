# Production Android AAB build

Этот проект должен собираться для RuStore через единый release entrypoint `DontGetSidetracked.EditorTools.ProductionAndroidBuild`.

## Перед сборкой

Настройте реальные production-значения локально в Unity Editor: package name из RuStore Console, public version, Android versionCode, production keystore/key alias, RuStore Pay settings, Remote Config App ID и реальные рекламные block IDs. Секреты, keystore и пароли не коммитятся.

Обычные production validators (`IProcessSceneWithReport` / `IPreprocessBuildWithReport`) запускаются самим `BuildPipeline.BuildPlayer`. Если обязательная конфигурация отсутствует или выглядит как placeholder, AAB должен завершиться ошибкой до выпуска артефакта.

Перед `BuildPipeline` единый entrypoint сам:
- применяет Android project settings;
- генерирует launcher branding;
- создаёт Gradle templates из **текущего установленного Unity Editor**;
- переключает active target на Android;
- запускает EDM4U `PlayServicesResolver.ResolveSync(true)` через fail-closed adapter;
- повторно импортирует сгенерированные assets/templates.

Если EDM4U не загружен или dependency resolution завершается ошибкой, production AAB блокируется. Ручной Force Resolve больше не является обязательным отдельным шагом перед каждой сборкой, но остаётся доступен через `Tools → НЕ СБЕЙСЯ! → Force Resolve Android Dependencies` для диагностики.

Перед началом сборки entrypoint удаляет старый AAB, старый `.release.json` и незавершённый `.tmp` по целевому пути. Если новая сборка или запись metadata завершается ошибкой, частичный AAB и metadata удаляются. Поэтому после failed build по целевому пути не остаётся старый AAB, который можно случайно принять за свежий релиз.

## Readiness report

Перед production AAB запустите **Tools → НЕ СБЕЙСЯ! → Release Readiness Report**. Команда сначала выполняет ту же детерминированную подготовку, что и production build (Android target, branding, Gradle templates, EDM4U Force Resolve, AAB profile), затем использует те же fail-closed проверки версии, placeholder-конфигурации, Android/SDK-контракта и RuStore Pay и сохраняет копию отчёта в `Library/NesbeisyaReleaseReadiness.txt`.

Статус `READY FOR SIGNED ANDROID DEVICE SMOKE TEST` означает, что репозиторий, production-конфигурация и signing-runtime этой машины готовы: настроен custom keystore, сам файл keystore доступен, выбран key alias и Unity-процесс видит непустые `NESBEISYA_KEYSTORE_PASS` / `NESBEISYA_KEYALIAS_PASS`. Значения секретов никогда не записываются в отчёт или логи. Статус всё равно не заменяет device-тесты Pay, Install Referrer, рекламы, Review, Update, notifications и share.

## Сборка из Unity Editor

Используйте меню:

`Tools → НЕ СБЕЙСЯ! → Build → Production Android AAB`

По умолчанию артефакт создаётся в:

`UnityProject/Builds/Android/nesbeisya-<version>-<versionCode>.aab`

Рядом создаётся `<имя>.release.json` с package name, public version, versionCode, Unity version, Unity build GUID, UTC timestamp, Git commit (если передан), именем AAB, фактическим размером и SHA-256 артефакта. Metadata сначала полностью записывается во временный файл и только затем переименовывается в финальный `.release.json`.

## Batchmode

Пример:

```text
Unity -batchmode -quit -projectPath UnityProject -executeMethod DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine
```

Чтобы задать конкретный путь AAB, перед запуском установите переменную окружения `NESBEISYA_RELEASE_OUTPUT`. Значение обязано оканчиваться на `.aab`.

Для production signing Unity-процесс должен видеть user-scoped переменные `NESBEISYA_KEYSTORE_PASS` и `NESBEISYA_KEYALIAS_PASS`. На self-hosted runner после их изменения нужно перезапустить scheduled runner task, чтобы новый процесс унаследовал переменные. Значения нельзя добавлять в репозиторий, документацию или логи.

Для локального build metadata можно передать `RELEASE_GIT_SHA`. В GitHub Actions автоматически используется `GITHUB_SHA`, если `RELEASE_GIT_SHA` не задан.

## Проверка артефакта

Перед загрузкой в RuStore запустите verifier на соседнем metadata-файле:

```text
python scripts/verify_release_artifact.py UnityProject/Builds/Android/nesbeisya-1.0.0-10.release.json --package ru.panishedqa.nesbeisya --version 1.0.0 --version-code 10 --git-sha <commit>
```

Verifier проверяет, что AAB существует рядом с metadata, имеет расширение `.aab`, его фактический размер и SHA-256 совпадают с данными сборки, а package/version/versionCode/Git SHA совпадают с ожидаемым релизом. Любая модификация AAB после сборки приводит к ошибке проверки.

## После сборки

После успешной integrity-проверки выполняются physical-device smoke tests из `RUSTORE_RELEASE_CHECKLIST.md`: Pay success/cancel/restore, deeplink + Install Referrer, Review/Update, notifications, share/FileProvider, ads и lifecycle regression.
