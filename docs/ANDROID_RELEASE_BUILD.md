# Production Android AAB build

Этот проект должен собираться для RuStore через единый release entrypoint `DontGetSidetracked.EditorTools.ProductionAndroidBuild`.

## Перед сборкой

Настройте реальные production-значения локально в Unity Editor: package name из RuStore Console, public version, Android versionCode, production keystore/key alias, RuStore Pay settings, Remote Config App ID и реальные рекламные block IDs. Секреты, keystore и пароли не коммитятся.

Обычные production validators (`IProcessSceneWithReport` / `IPreprocessBuildWithReport`) запускаются самим `BuildPipeline.BuildPlayer`. Если обязательная конфигурация отсутствует или выглядит как placeholder, AAB должен завершиться ошибкой до выпуска артефакта.

## Сборка из Unity Editor

Используйте меню:

`Tools → НЕ СБЕЙСЯ! → Build → Production Android AAB`

По умолчанию артефакт создаётся в:

`UnityProject/Builds/Android/nesbeisya-<version>-<versionCode>.aab`

Рядом создаётся `<имя>.release.json` с package name, public version, versionCode, Unity version, Unity build GUID, UTC timestamp, Git commit (если передан), именем AAB и размером файла.

## Batchmode

Пример:

```text
Unity -batchmode -quit -projectPath UnityProject -executeMethod DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine
```

Чтобы задать конкретный путь AAB, перед запуском установите переменную окружения `NESBEISYA_RELEASE_OUTPUT`. Значение обязано оканчиваться на `.aab`.

Для локального build metadata можно передать `RELEASE_GIT_SHA`. В GitHub Actions автоматически используется `GITHUB_SHA`, если `RELEASE_GIT_SHA` не задан.

## После сборки

Перед загрузкой в RuStore сверяйте AAB и соседний `.release.json`: package name, version, versionCode и Git commit должны соответствовать релизу. После этого выполняются physical-device smoke tests из `RUSTORE_RELEASE_CHECKLIST.md`: Pay success/cancel/restore, deeplink + Install Referrer, Review/Update, notifications, share/FileProvider, ads и lifecycle regression.
