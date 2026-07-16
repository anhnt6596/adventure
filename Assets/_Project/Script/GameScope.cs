using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameScope : LifetimeScope
{
    [SerializeField] private MainCharStatsConfig _mainCharStatsConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_mainCharStatsConfig);
        builder.Register<MainCharStats>(Lifetime.Singleton).As<ICharacterStats>().AsSelf();

        builder.RegisterEntryPoint<GameController>();
    }
}
