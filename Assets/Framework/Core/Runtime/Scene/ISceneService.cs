using System;
using Cysharp.Threading.Tasks;

namespace Core
{
    public interface ISceneService
    {
        /// <summary>Currently loaded scene name (empty until the first load).</summary>
        string ActiveScene { get; }

        /// <summary>Loads a scene, reporting 0..1 progress. Completes once the scene is active.</summary>
        UniTask LoadAsync(string sceneName, IProgress<float> progress = null);
    }
}
