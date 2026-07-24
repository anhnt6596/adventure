// The player's view. The shared movement/attack animation lives in UnitView; this is where MC-only view
// behaviour (health bar, name tag, ...) will go. It's the concrete on the MC prefab, so every unit kind has a
// typed view slot — mirroring MC : UnitController.
public class MCView : UnitView
{
}
