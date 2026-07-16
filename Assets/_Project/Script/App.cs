using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core;
using Core.UI;

public class App : LifetimeScope
{
    private static App _instance;

    [SerializeField] private UISystem _uiSystem;
    [SerializeField] private ConfigRegistry _configRegistry;

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
        builder.RegisterInstance(_configRegistry);

        builder.Register<IEventBus, EventBus>(Lifetime.Singleton);
        builder.Register<IDependencyInjector, VContainerInjector>(Lifetime.Singleton);
        builder.RegisterComponent(_uiSystem).As<IUISystem>().AsSelf();

        builder.Register<ISceneService, SceneService>(Lifetime.Singleton);
        builder.Register<IInputGate, InputGate>(Lifetime.Singleton);
        builder.RegisterEntryPoint<LoadingFlow>();
    }

    private void Start()
    {
        _uiSystem.Initialize(
            Container.Resolve<IEventBus>(),
            Container.Resolve<IDependencyInjector>());
    }
}
