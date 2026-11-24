// Ответственности
// - Передаёт тестовые настройки загруженной сцене до запуска игровых систем.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieYard.Tests
{
    internal static class YardTestScene
    {
        internal static IEnumerator Load(Action<GameLifetimeScope> configure)
        {
            void Prepare(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == "Yard") configure(UnityEngine.Object.FindAnyObjectByType<GameLifetimeScope>());
            }
            SceneManager.sceneLoaded += Prepare;
            try
            {
                yield return SceneManager.LoadSceneAsync("Yard");
                yield return null;
            }
            finally { SceneManager.sceneLoaded -= Prepare; }
        }
    }
}
