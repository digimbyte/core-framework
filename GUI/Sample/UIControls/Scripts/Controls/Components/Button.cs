using Nova;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace NovaSamples.UIControls
{
    /// <summary>
    /// A UI control which reacts to user input and fires click / hover events.
    /// </summary>
    public class Button : UIControl<ButtonVisuals>
    {
        [Tooltip("Event fired when the button is clicked.")]
        public UnityEvent OnClicked = null;

        [Tooltip("Event fired when the button is Hovered over.")]
        public UnityEvent OnHoverEnter = null;

        [Tooltip("Event fired when the button is not hovered anymore.")]
        public UnityEvent OnHoverExit = null;

        private void OnEnable()
        {
            if (View.TryGetVisuals(out ButtonVisuals visuals))
            {
                // Set default state
                visuals.UpdateVisualState(VisualState.Default);
            }

            // Subscribe to desired events
            View.UIBlock.AddGestureHandler<Gesture.OnClick, ButtonVisuals>(HandleClicked);

            // Route hover/unhover through local handlers so we can both update visuals and fire UnityEvents
            View.UIBlock.AddGestureHandler<Gesture.OnHover, ButtonVisuals>(HandleHoverEnter);
            View.UIBlock.AddGestureHandler<Gesture.OnUnhover, ButtonVisuals>(HandleHoverExit);

            View.UIBlock.AddGestureHandler<Gesture.OnPress, ButtonVisuals>(ButtonVisuals.HandlePressed);
            View.UIBlock.AddGestureHandler<Gesture.OnRelease, ButtonVisuals>(ButtonVisuals.HandleReleased);
            View.UIBlock.AddGestureHandler<Gesture.OnCancel, ButtonVisuals>(ButtonVisuals.HandlePressCanceled);

            // Keyboard/gamepad navigation: mirror hover visuals when nav focus moves to/from this button.
            View.UIBlock.AddGestureHandler<Navigate.OnMoveTo, ButtonVisuals>(HandleNavFocusEnter);
            View.UIBlock.AddGestureHandler<Navigate.OnMoveFrom, ButtonVisuals>(HandleNavFocusExit);
            View.UIBlock.AddGestureHandler<Navigate.OnSelect, ButtonVisuals>(HandleNavSelect);
            View.UIBlock.AddGestureHandler<Navigate.OnDeselect, ButtonVisuals>(HandleNavDeselect);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            View.UIBlock.RemoveGestureHandler<Gesture.OnClick, ButtonVisuals>(HandleClicked);
            View.UIBlock.RemoveGestureHandler<Gesture.OnHover, ButtonVisuals>(HandleHoverEnter);
            View.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, ButtonVisuals>(HandleHoverExit);
            View.UIBlock.RemoveGestureHandler<Gesture.OnPress, ButtonVisuals>(ButtonVisuals.HandlePressed);
            View.UIBlock.RemoveGestureHandler<Gesture.OnRelease, ButtonVisuals>(ButtonVisuals.HandleReleased);
            View.UIBlock.RemoveGestureHandler<Gesture.OnCancel, ButtonVisuals>(ButtonVisuals.HandlePressCanceled);

            View.UIBlock.RemoveGestureHandler<Navigate.OnMoveTo, ButtonVisuals>(HandleNavFocusEnter);
            View.UIBlock.RemoveGestureHandler<Navigate.OnMoveFrom, ButtonVisuals>(HandleNavFocusExit);
            View.UIBlock.RemoveGestureHandler<Navigate.OnSelect, ButtonVisuals>(HandleNavSelect);
            View.UIBlock.RemoveGestureHandler<Navigate.OnDeselect, ButtonVisuals>(HandleNavDeselect);
        }

        /// <summary>
        /// Fire the Unity event on Click.
        /// For navigation-triggered clicks (ControlID != 0), also flash the pressed visual state
        /// since Gesture.OnPress / OnRelease are not fired for keyboard/gamepad activations.
        /// </summary>
        /// <param name="evt">The click event data.</param>
        /// <param name="visuals">The button visuals which received the click.</param>
        private void HandleClicked(Gesture.OnClick evt, ButtonVisuals visuals)
        {
            OnClicked?.Invoke();

            // ControlID 0 = pointer (mouse/touch) — pressed visual is already handled by
            // Gesture.OnPress / Gesture.OnRelease, so no flash needed.
            // Any other ControlID = keyboard / gamepad nav — flash pressed colour ourselves.
            // Click handlers may hide or reparent this control (e.g. auth busy) before we return;
            // do not start a coroutine on an inactive hierarchy.
            if (evt.Interaction.ControlID != 0 && isActiveAndEnabled)
            {
                StartCoroutine(NavSelectFlash(visuals));
            }
        }

        /// <summary>
        /// Update visuals and fire the hover-enter UnityEvent.
        /// </summary>
        private void HandleHoverEnter(Gesture.OnHover evt, ButtonVisuals visuals)
        {
            ButtonVisuals.HandleHovered(evt, visuals);
            OnHoverEnter?.Invoke();
        }

        /// <summary>
        /// Update visuals and fire the hover-exit UnityEvent.
        /// </summary>
        private void HandleHoverExit(Gesture.OnUnhover evt, ButtonVisuals visuals)
        {
            ButtonVisuals.HandleUnhovered(evt, visuals);
            OnHoverExit?.Invoke();
        }

        // ── Keyboard / gamepad navigation visual state ─────────────────────────────

        /// <summary>
        /// Nav focus arrived on this button — show hovered colour and fire OnHoverEnter.
        /// </summary>
        private void HandleNavFocusEnter(Navigate.OnMoveTo evt, ButtonVisuals visuals)
        {
            visuals.UpdateVisualState(VisualState.Hovered);
            OnHoverEnter?.Invoke();
        }

        /// <summary>
        /// Nav focus left this button — revert to default colour and fire OnHoverExit.
        /// </summary>
        private void HandleNavFocusExit(Navigate.OnMoveFrom evt, ButtonVisuals visuals)
        {
            visuals.UpdateVisualState(VisualState.Default);
            OnHoverExit?.Invoke();
        }

        /// <summary>
        /// Nav select (Enter/Space/gamepad South) — flash pressed colour, fire OnClicked, restore hovered.
        /// </summary>
        private void HandleNavSelect(Navigate.OnSelect evt, ButtonVisuals visuals)
        {
            OnClicked?.Invoke();
            if (isActiveAndEnabled)
                StartCoroutine(NavSelectFlash(visuals));
        }

        private IEnumerator NavSelectFlash(ButtonVisuals visuals)
        {
            visuals.UpdateVisualState(VisualState.Pressed);
            yield return new WaitForSeconds(0.15f);
            visuals.UpdateVisualState(VisualState.Hovered);
        }

        /// <summary>
        /// Nav deselect (Escape) — revert to default colour.
        /// </summary>
        private void HandleNavDeselect(Navigate.OnDeselect evt, ButtonVisuals visuals)
        {
            visuals.UpdateVisualState(VisualState.Default);
        }
    }
}
