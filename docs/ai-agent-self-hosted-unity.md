# Self-hosted Unity runner for the coding agent

This repository is public. The self-hosted Unity runner is therefore deliberately **not** triggered by pull requests. GitHub warns that self-hosted runners can execute untrusted code from public-repository pull requests if workflows are configured unsafely.

The repository workflow is:

`.github/workflows/unity-self-hosted.yml`

It only sends a job to the local runner when all of the following are true:

- the event is a push to an `agent/**` branch, or a manual workflow dispatch;
- repository variable `UNITY_SELF_HOSTED_ENABLED` equals `true`;
- the repository is exactly `PanishevQA/rustore_game`;
- a runner is online with labels `self-hosted`, `windows`, `x64`, and `unity`.

The job has read-only repository permissions and does not consume GitHub secrets. Push events run the `Full` Unity gate with `-SkipFast`: hosted CI owns static validators and pure C# tests, while the Windows runner owns real Unity compilation/tests plus release-readiness validation. Manual dispatch supports `Unity`, `Full`, `Build`, or `AndroidIntegrationProbe`. The probe mode performs a Development APK build, EDM4U resolution, and `javap` verification of the resolved RuStore native Install Referrer AAR without requiring production signing secrets. Run production `Build` only after hosted CI for that commit is green.

## One-time Windows setup

1. Use a dedicated Windows account for the GitHub Actions service if practical, and keep the runner in a dedicated directory that is **not** your normal Unity working copy. This isolates Unity persistent data and cached state from your personal Editor profile. A location such as `C:\actions-runner\rustore-game` is appropriate.
2. In GitHub, open this repository, then go to **Settings -> Actions -> Runners -> New self-hosted runner**.
3. Choose **Windows / x64**.
4. Use the download and registration commands shown by GitHub on that page. Do not copy a registration token into this repository or into chat; registration tokens are temporary credentials.
5. During runner configuration, add the custom label `unity`. The normal GitHub runner also receives the default labels `self-hosted`, `windows`, and `x64`.
6. Register the runner normally, but for Unity Personal do not keep it running under a built-in Windows service identity. After registration, use the repository helper described below to run it under the Unity-licensed interactive Windows user.
7. Make sure this Windows account can start Unity `6000.3.24f1` in batchmode and that Unity licensing is already valid for that account.
8. Make sure the machine has:
   - Unity `6000.3.24f1`;
   - Git;
   - Python does not need to be installed system-wide: the workflow bootstraps the pinned Python 3.13.15 embeddable package from python.org and verifies its SHA-256 before use;
   - enough free disk space for Unity Library/package cache/build artifacts.
9. If Unity is not installed in a normal Unity Hub location, create repository variable `UNITY_EXE` containing the full path to `Unity.exe`.
10. Create repository variable `UNITY_SELF_HOSTED_ENABLED` with value `true`.

Repository variables are non-secret configuration. Do not put passwords, keystore credentials, RuStore tokens, or other secrets in them.

## What happens after activation

Every subsequent push to a branch matching `agent/**` that changes Unity/project/automation files will automatically:

1. check out that exact commit into the runner workspace;
2. validate the Windows/Unity/Python environment;
3. launch Unity batchmode in the `Full` gate;
4. compile project code and run Unity tests;
5. validate enabled scenes/prefabs/serialized references without saving them;
6. run release-readiness validation;
7. collect logs and structured diagnostics;
8. upload `artifacts/agent-check/**`, `artifacts/release-candidate/**`, and any Android integration-probe diagnostics as workflow artifacts even when the job fails.

Static validators and pure C# tests remain on hosted CI and are intentionally skipped on the self-hosted job to avoid duplicating work.

This gives the coding agent a real Unity feedback loop: commit -> local Unity -> logs -> fix -> new commit.

## Security boundary

Because the repository is public:

- do not add `pull_request` or `pull_request_target` to the self-hosted Unity workflow;
- do not expose secrets to this job;
- do not reuse the runner workspace as your personal development checkout;
- treat anyone with write access to `agent/**` branches as someone who can execute code on the runner;
- keep Windows, Unity, Git, Python, .NET, and the GitHub runner updated;
- remove or disable the runner when it is no longer needed.

The static validator `scripts/validate_self_hosted_unity_runner.py` protects the main workflow security invariants.



## Android integration / native AAR probe

Use **Actions -> unity-self-hosted -> Run workflow -> AndroidIntegrationProbe** when changing RuStore Android dependencies, EDM4U configuration, or the native Install Referrer bridge.

This mode builds a Development APK with debug signing, forces Android dependency resolution, and then inspects the actually resolved RuStore artifacts with the JDK `javap` tool. For Install Referrer Android 10.6.1 the verified binary contract is: client method `getInstallReferrerV2()`, result type `InstallReferrerV2`, and referrer-string getter `getInstallReferrer()`. Diagnostics are stored under `artifacts/android-integration-probe/**`.

A passing integration probe verifies dependency resolution and the JVM API surface, but it does not replace the physical-device install/referral smoke test.

## Production AAB from the runner

Use **Actions -> unity-self-hosted -> Run workflow** and select **Build** only when you intentionally want a production Android artifact.

Build mode first runs all normal checks, PlayMode smoke, serialized validation, and release readiness. Only after those pass does it call the existing `DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine` entrypoint. The AAB and adjacent `.release.json` metadata are written under `artifacts/release-candidate/android/`, then `scripts/verify_release_artifact.py` verifies the package name, commit SHA, file size, and SHA-256 digest.

A successful Build mode still does not replace a signed physical-device smoke test of RuStore Pay, Update, Review, ads, deeplinks, and referral flows.

For signed Build mode, keep the release signing passwords only on the Windows runner account as user-scoped environment variables `NESBEISYA_KEYSTORE_PASS` and `NESBEISYA_KEYALIAS_PASS`. The build entrypoint injects them into Unity at runtime and never writes them to source control or logs. Restart the scheduled runner task after changing those environment variables so the runner process inherits the new values.


## Unity Personal and Windows service identity

Unity Personal licensing is tied to the Windows user profile that is signed into Unity Hub. A runner running as LocalSystem, NetworkService, or LocalService can connect to the machine-level licensing client but still receive zero Unity entitlements and exit batchmode with code 198.

For unattended Unity validation on this project, the Windows runner process/service must therefore execute as the same normal Windows user that has the active Unity Personal license. Do not use a built-in Windows service identity for Unity jobs.

The runner preflight rejects built-in service identities before launching Unity, so this configuration problem fails immediately instead of wasting a full Unity test cycle.


## Recommended Windows startup for Unity Personal

For this project, do not run the GitHub runner as NetworkService, LocalSystem, or LocalService when using Unity Personal.

The repository provides a password-free startup helper:

scripts/install_unity_runner_logon_task.ps1

Run it once from an elevated PowerShell opened by the same Windows user that is signed into Unity Hub:

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install_unity_runner_logon_task.ps1

The helper:
- stops and disables the old GitHub Actions Windows service;
- keeps the existing runner registration and credentials;
- creates a Windows Scheduled Task for the current user;
- writes a small watchdog beside the runner in `C:\actions-runner\run-unity-runner-forever.ps1`;
- launches that watchdog at user logon;
- restarts the official `run.cmd` automatically if the runner exits after a network/update interruption;
- writes restart events to `C:\actions-runner\_diag\unity-runner-watchdog.log`;
- starts the task immediately;
- stores no Windows password in the repository or command line.

This keeps the runner unattended after login while ensuring Unity batchmode runs in the same user profile that owns the Unity Personal entitlement, and prevents a normal runner exit from leaving future jobs queued indefinitely.
