using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameScope : LifetimeScope
{
    [SerializeField] private MainCharStatsConfig _mainCharStatsConfig;
    [SerializeField] private Character _character;         // MapService repositions it on a map change
    [SerializeField] private CameraRig _cameraRig;         // MapService snaps it on a map change
    [SerializeField] private CollisionSystem _collisionSystem;  // MapService rebinds map terrain + bodies to it
    [SerializeField] private DayNightConfig _dayNightConfig;    // the day/night palette; DayNightLighting reads it

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_mainCharStatsConfig);
        builder.Register<MainCharStats>(Lifetime.Singleton).As<ICharacterStats>().AsSelf();

        builder.RegisterComponent(_character);
        builder.RegisterComponent(_cameraRig);
        builder.RegisterComponent(_collisionSystem);
        builder.Register<InteractField>(Lifetime.Singleton);
        builder.RegisterInstance(new CombatWorld());
        builder.Register<IMapService, MapService>(Lifetime.Singleton);

        builder.RegisterInstance(_dayNightConfig);
        builder.RegisterEntryPoint<DayNightClock>().AsSelf();   // ticks time of day; DayNightLighting (on camera) reads it

        builder.RegisterEntryPoint<GameController>();
    }
}
