using System.Collections;
using DontGetSidetracked.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DontGetSidetracked.Tests.PlayMode
{
    public sealed class MainSceneSmokeTests
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        [UnityTest]
        public IEnumerator MainSceneLoadsAndRuntimeBootstrapBuildsUi()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(MainScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Unity did not create a Main scene load operation.");

            while (!load.isDone)
                yield return null;

            // Give AfterSceneLoad runtime initializers and Awake/Start-driven presentation wiring time to settle.
            yield return null;
            yield return null;

            Scene active = SceneManager.GetActiveScene();
            Assert.That(active.path, Is.EqualTo(MainScenePath), "Main scene is not the active PlayMode scene.");

            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap was not created after Main scene load.");
            Assert.That(bootstrap.gameObject.activeInHierarchy, Is.True,
                "GameBootstrap is inactive after Main scene startup.");

            GameObject canvas = GameObject.Find("GameCanvas");
            Assert.That(canvas, Is.Not.Null, "Runtime UI root GameCanvas was not created.");
            Assert.That(canvas.activeInHierarchy, Is.True, "Runtime UI root is inactive after startup.");
            Assert.That(Application.targetFrameRate, Is.EqualTo(60), "Runtime frame-rate policy was not applied.");
        }
    }
}
