using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core;
using Core.UI;

public class App : LifetimeScope
{
    private static App _instance;

    [SerializeField] private UISystem _uiSystem;
    [SerializeField] private Character _mainCharacter;
    [SerializeField] private MainCharStatsConfig _mainCharStatsConfig;

    protected override void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_mainCharStatsConfig);
        builder.Register<MainCharStats>(Lifetime.Singleton).As<ICharacterStats>().AsSelf();
        builder.RegisterComponent(_mainCharacter);

        builder.Register<IEventBus, EventBus>(Lifetime.Singleton);
        builder.Register<IDependencyInjector, VContainerInjector>(Lifetime.Singleton);
        builder.RegisterComponent(_uiSystem).As<IUISystem>().AsSelf();
    }

    private void Start()
    {
        _uiSystem.Initialize(
            Container.Resolve<IEventBus>(),
            Container.Resolve<IDependencyInjector>());
    }
}
