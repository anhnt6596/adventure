using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

public class GameScope : LifetimeScope
{
    [SerializeField] private CameraRig _cameraRig;         // MapService snaps it on a map change
    [FormerlySerializedAs("_dayNightConfig")]
    [SerializeField] private DayLightConfig _dayLightConfig;    // the day/night palette; DayNightLighting reads it
    // CollisionSystem is a scene singleton (CollisionSystem.Instance) — bodies self-register, so it isn't in DI.

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IGetMCConfig, MCConfigProvider>(Lifetime.Singleton);   // wall over ConfigRegistry — PlayerSystem asks this, not the registry
        builder.Register<IGetPropConfig, PropConfigProvider>(Lifetime.Singleton);   // same wall for prop configs — trees/rocks/chests resolve by id

        builder.RegisterComponent(_cameraRig);
        builder.Register<InteractField>(Lifetime.Singleton);
        // CombatWorld, like CollisionSystem, is a static singleton (CombatWorld.Instance) — hittables
        // self-register and attacks query it, so it isn't in DI.
        builder.Register<InventorySystem>(Lifetime.Singleton);
        builder.Register<PayGateSystem>(Lifetime.Singleton);   // remembers which pay gates are already bought

        builder.Register<IGetUpgradeTree, UpgradeTreeProvider>(Lifetime.Singleton);   // wall over ConfigRegistry, like IGetMCConfig
        builder.Register<CharacterLevels>(Lifetime.Singleton);   // level + exp per character
        builder.Register<CardLibrary>(Lifetime.Singleton);        // which arena upgrade cards this save has
        builder.Register<UpgradePoints>(Lifetime.Singleton);     // the tree's own currency — earned, not derived from level
        builder.Register<UpgradeSystem>(Lifetime.Singleton);     // which nodes each character has bought

        // What wears a (!), and the one rule feeding it today. The notifier is an entry point registered AFTER
        // PlayerSystem so its Start runs once a body exists — see UpgradeNotifier.Report, which deliberately
        // says nothing until there is a character to say it about.
        builder.Register<NotificationService>(Lifetime.Singleton);

        // An entry point, and registered BEFORE PlayerSystem: it catches up the character being played, so it
        // has to exist before the first body is spawned. AsSelf so ExpOnDeath can inject it on an enemy.
        builder.RegisterEntryPoint<ExperienceSystem>().AsSelf();   // what pays experience, and for what
        builder.Register<IMapService, MapService>(Lifetime.Singleton);

        // AsSelf too: IPlayer is deliberately read-only, so switching character (cheat panel now, character
        // select later) needs the concrete system.
        builder.RegisterEntryPoint<PlayerSystem>().As<IPlayer>().AsSelf();   // owns + spawns the MC; runs before GameController warps
        builder.Register<EnemySpawner>(Lifetime.Singleton);                 // makes enemies by id (spawn zones call it)
        builder.RegisterEntryPoint<UpgradeNotifier>();               // unspent points -> the (!) on the avatar and its tab
        builder.RegisterEntryPoint<CameraFollowsPlayer>();          // aims CameraRig at the spawned body

        builder.RegisterInstance(_dayLightConfig);
        builder.RegisterEntryPoint<DayNightClock>().AsSelf();   // the OVERWORLD's clock; DayNightDebug scrubs this one specifically

        // Which clock the sky is currently reading. Views (DayNightLighting, ShadowSun) ask for ITimeOfDay and
        // get this, so a gate run can hand them its own clock without either of them knowing gates exist.
        // AsSelf as well, because ArenaRunner is the one that hands it over.
        builder.Register<ActiveTimeOfDay>(Lifetime.Singleton).As<ITimeOfDay>().AsSelf();

        // Owns a run and the scope a run lives in. AsSelf so an ArenaGate on a map can inject it.
        builder.RegisterEntryPoint<ArenaRunner>().AsSelf();

        builder.RegisterEntryPoint<GameController>();
    }
}
