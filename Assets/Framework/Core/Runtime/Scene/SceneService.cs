using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Core
{
    public class SceneService : ISceneService
    {
        public string ActiveScene { get; private set; } = "";

        public async UniTask LoadAsync(string sceneName, IProgress<float> progress = null)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null) throw new ArgumentException($"Scene '{sceneName}' is not in Build Settings.", nameof(sceneName));

            // Hold activation so callers can show progress reaching 1 before the swap.
            op.allowSceneActivation = false;

            // Unity stalls at 0.9 while activation is blocked -> normalize to 0..1.
            while (op.progress < 0.9f)
            {
                progress?.Report(op.progress / 0.9f);
                await UniTask.Yield();
            }
            progress?.Report(1f);

            op.allowSceneActivation = true;
            while (!op.isDone) await UniTask.Yield();

            ActiveScene = sceneName;
        }
    }
}
