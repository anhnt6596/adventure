using System;
using UnityEngine.UIElements;

namespace Core.UI
{
    public class RepeatClickManipulator : PointerManipulator
    {
        public string TriggerClass { get; set; } = "triggered-scale-in";
        private IVisualElementScheduledItem _triggerFxSchedule;

        private readonly Action _onClick;
        private readonly long _delayMs;
        private readonly long _intervalMs;

        private IVisualElementScheduledItem _schedule;
        private bool _pressing;
        private int _activePointerId = -1;

        /// <param name="onClick">Action to invoke immediately, then repeatedly while pressed.</param>
        /// <param name="delayMs">Delay before repeat starts.</param>
        /// <param name="intervalMs">Interval between repeats.</param>
        public RepeatClickManipulator(Action onClick, long delayMs = 400, long intervalMs = 80)
        {
            _onClick = onClick ?? throw new ArgumentNullException(nameof(onClick));
            _delayMs = delayMs;
            _intervalMs = intervalMs;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            if (target is Button btn) btn.clickable = null;  
            target.AddToClassList("repeat-button");
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.RemoveFromClassList("repeat-button");
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            Cancel();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!target.enabledInHierarchy)
                return;

            if (evt.button != 0)
                return;

            // Prevent duplicate/reentrant scheduling.
            Cancel();

            _pressing = true;
            _activePointerId = evt.pointerId;

            target.CapturePointer(_activePointerId);

            Trigger();

            _schedule = target.schedule
                .Execute(() =>
                {
                    if (_pressing) Trigger();
                })
                .StartingIn(_delayMs)
                .Every(_intervalMs);

            evt.StopPropagation();
        }

        private void Trigger()
        {
            _onClick?.Invoke();
            PlayTriggerEffect();
        }
        private void PlayTriggerEffect()
        {
            target.RemoveFromClassList(TriggerClass);
            _triggerFxSchedule?.Pause();

            target.AddToClassList(TriggerClass);

            _triggerFxSchedule = target.schedule.Execute(() =>
            {
                target.RemoveFromClassList(TriggerClass);
            }).StartingIn(70);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointerId)
                return;

            Cancel();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != _activePointerId)
                return;

            Cancel();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            Cancel();
        }

        private void Cancel()
        {
            _pressing = false;

            _schedule?.Pause();
            _schedule = null;

            if (target != null && _activePointerId != -1 && target.HasPointerCapture(_activePointerId))
                target.ReleasePointer(_activePointerId);

            _activePointerId = -1;
        }
    }
}