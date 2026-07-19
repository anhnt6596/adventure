using UnityEngine;
using VContainer;

// Dev/test control for the day/night clock. Tick Pause to freeze time (lighting + shadows hold still);
// while paused, drag Scrub Hour to jog the sun to any time of day live — handy for tuning shadows at
// dawn / noon / dusk without waiting for the cycle.
//
// Wiring: drop on a scene object and add it to GameScope's Auto Inject list (needs DayNightClock).
[DisallowMultipleComponent]
public class DayNightDebug : MonoBehaviour
{
    [SerializeField] bool pause;
    [SerializeField, Range(0f, 24f)] float scrubHour = 12f;

    DayNightClock _clock;

    [Inject]
    public void Construct(DayNightClock clock) => _clock = clock;

    void Update()
    {
        if (_clock == null) return;
        _clock.Paused = pause;
        if (pause) _clock.SetTime01(scrubHour / 24f);   // paused → the slider scrubs the time of day
    }
}
