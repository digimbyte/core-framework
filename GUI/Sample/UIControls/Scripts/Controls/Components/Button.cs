using Nova;
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
        }

        /// <summary>
        /// Fire the Unity event on Click.
        /// </summary>
        /// <param name="evt">The click event data.</param>
        /// <param name="visuals">The button visuals which received the click.</param>
        private void HandleClicked(Gesture.OnClick evt, ButtonVisuals visuals) => OnClicked?.Invoke();

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
    }
}
