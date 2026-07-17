using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameScope : LifetimeScope
{
    [SerializeField] private MainCharStatsConfig _mainCharStatsConfig;
    [SerializeField] private Character _character;   // MapService repositions it on a map change

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_mainCharStatsConfig);
        builder.Register<MainCharStats>(Lifetime.Singleton).As<ICharacterStats>().AsSelf();

        builder.RegisterComponent(_character);
        builder.Register<InteractField>(Lifetime.Singleton);
        builder.Register<IMapService, MapService>(Lifetime.Singleton);

        builder.RegisterEntryPoint<GameController>();
    }
}
