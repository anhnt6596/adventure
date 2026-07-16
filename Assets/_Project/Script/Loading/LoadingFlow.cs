using System;
using Cysharp.Threading.Tasks;
using Core;
using UnityEngine;
using VContainer.Unity;

// Boot flow: runs in LoadingScene, loads the game scene.
// Loading UI / extra steps (warmup, save load, remote config...) hook in here later.
public class LoadingFlow : IStartable
{
    public const string GameSceneName = "GameScene";

    readonly ISceneService _scenes;

    public LoadingFlow(ISceneService scenes) => _scenes = scenes;

    public void Start() => Run().Forget();

    async UniTaskVoid Run()
    {
        var progress = new Progress<float>(p => Debug.Log($"[Loading] {p:P0}"));

        // future steps go here (save load, config fetch, asset warmup) — each can report progress

        await _scenes.LoadAsync(GameSceneName, progress);
    }
}
