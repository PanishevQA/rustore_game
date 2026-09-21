# Self-hosted Unity runner for the coding agent

This repository is public. The self-hosted Unity runner is therefore deliberately **not** triggered by pull requests. GitHub warns that self-hosted runners can execute untrusted code from public-repository pull requests if workflows are configured unsafely.

The repository workflow is:

`.github/workflows/unity-self-hosted.yml`

It only sends a job to the local runner when all of the following are true:

- the event is a push to an `agent/**` branch, or a manual workflow dispatch;
- repository variable `UNITY_SELF_HOSTED_ENABLED` equals `true`;
- the repository is exactly `PanishevQA/rustore_game`;
- a runner is online with labels `self-hosted`, `windows`, `x64`, and `unity`.

The job has read-only repository permissions and does not consume GitHub secrets. Push events always run `scripts/agent_check.ps1 -Mode Unity`. Manual dispatch lets an authorized collaborator explicitly choose `Unity`, `Full`, or `Build`.

## One-time Windows setup

1. Use a dedicated Windows account for the GitHub Actions service if practical, and keep the runner in a dedicated directory that is **not** your normal Unity working copy. This isolates Unity persistent data and cached state from your personal Editor profile. A location such as `C:\actions-runner\rustore-game` is appropriate.
2. In GitHub, open this repository, then go to **Settings -> Actions -> Runners -> New self-hosted runner**.
3. Choose **Windows / x64**.
4. Use the download and registration commands shown by GitHub on that page. Do not copy a registration token into this repository or into chat; registration tokens are temporary credentials.
5. During runner configuration, add the custom label `unity`. The normal GitHub runner also receives the default labels `self-hosted`, `windows`, and `x64`.
6. Configure the runner application as a Windows service so it starts with the machine. Use the service instructions shown by GitHub for the runner package you installed.
7. Make sure this Windows account can start Unity `6000.3.24f1` in batchmode and that Unity licensing is already valid for that account.
8. Make sure the machine has:
   - Unity `6000.3.24f1`;
   - Git;
   - Python 3 available as `python` or `py`;
   - .NET SDK 8 or newer;
   - enough free disk space for Unity Library/package cache/build artifacts.
9. If Unity is not installed in a normal Unity Hub location, create repository variable `UNITY_EXE` containing the full path to `Unity.exe`.
10. Create repository variable `UNITY_SELF_HOSTED_ENABLED` with value `true`.

Repository variables are non-secret configuration. Do not put passwords, keystore credentials, RuStore tokens, or other secrets in them.

## What happens after activation

Every subsequent push to a branch matching `agent/**` that changes Unity/project/automation files will automatically:

1. check out that exact commit into the runner workspace;
2. validate the Windows/Unity/Python/.NET environment;
3. run repository static validators and pure C# tests;
4. launch Unity batchmode;
5. compile project code;
6. run EditMode tests;
7. open and validate enabled scenes/prefabs/serialized references without saving them;
8. collect logs and structured diagnostics;
9. upload `artifacts/agent-check/**` and `artifacts/release-candidate/**` as a workflow artifact even when the job fails.

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


## Production AAB from the runner

Use **Actions -> unity-self-hosted -> Run workflow** and select **Build** only when you intentionally want a production Android artifact.

Build mode first runs all normal checks, PlayMode smoke, serialized validation, and release readiness. Only after those pass does it call the existing `DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine` entrypoint. The AAB and adjacent `.release.json` metadata are written under `artifacts/release-candidate/android/`, then `scripts/verify_release_artifact.py` verifies the package name, commit SHA, file size, and SHA-256 digest.

A successful Build mode still does not replace a signed physical-device smoke test of RuStore Pay, Update, Review, ads, deeplinks, and referral flows.
