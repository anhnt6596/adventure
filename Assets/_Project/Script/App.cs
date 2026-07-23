using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core;
using Core.UI;
using Core.Save;

public class App : LifetimeScope
{
    private static App _instance;

    [SerializeField] private UISystem _uiSystem;
    [SerializeField] private ConfigRegistry _configRegistry;
    [SerializeField] private PrefabRegistry _prefabRegistry;

    protected override void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // On a lag spike Time.deltaTime is clamped to this, so a fast body can't jump across water/wall in
        // one frame (tunneling) before collision can correct it. The game briefly slows instead of
        // teleporting through. Keep it below (cellSize / fastest speed) so a step never skips a cell.
        Time.maximumDeltaTime = 0.05f;

        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_configRegistry);
        builder.RegisterInstance(_prefabRegistry);
        builder.RegisterInstance(new SaveService());   // default JSON serializer; instance dodges the optional-ctor-param resolve

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
