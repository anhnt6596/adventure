// The enemy's view. Shared movement/attack animation lives in UnitView; this is where enemy-only view
// behaviour (aggro marker, hit flash, HP bar, ...) will go. It's the concrete on the enemy prefab, so every
// unit kind has a typed view slot — mirroring EnemyController : UnitController.
public class EnemyView : UnitView
{
}
