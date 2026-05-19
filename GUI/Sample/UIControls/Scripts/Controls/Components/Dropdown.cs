using Nova;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NovaSamples.UIControls
{
    /// <summary>
    /// A UI control which reacts to user input and displays a list of selectable options.
    /// </summary>
    public class Dropdown : UIControl<DropdownVisuals>
    {
        [Tooltip("The event fired when a new item is selected from the dropdown list.")]
        public UnityEvent<string> OnValueChanged = null;

        [Tooltip("The data used to populate the list of selectable items in the dropdown.")]
        public DropdownData DropdownOptions = new DropdownData();

        /// <summary>
        /// Adds an option if it is not already present (comparison is case-insensitive).
        /// </summary>
        /// <returns><c>true</c> if the option was added; <c>false</c> if it was null/whitespace, a duplicate, or <see cref="DropdownOptions"/> is missing.</returns>
        public bool TryAddOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option) || DropdownOptions == null)
            {
                return false;
            }

            DropdownOptions.Options ??= new List<string>();

            if (IndexOfOptionIgnoreCase(option, DropdownOptions.Options) >= 0)
            {
                return false;
            }

            DropdownOptions.Options.Add(option);
            RefreshAfterOptionsChanged();
            return true;
        }

        /// <summary>
        /// Removes the first option that matches <paramref name="option"/> using case-insensitive comparison.
        /// </summary>
        /// <returns><c>true</c> if an option was removed.</returns>
        public bool RemoveOption(string option)
        {
            if (string.IsNullOrEmpty(option) || DropdownOptions?.Options == null)
            {
                return false;
            }

            int index = IndexOfOptionIgnoreCase(option, DropdownOptions.Options);
            if (index < 0)
            {
                return false;
            }

            DropdownOptions.Options.RemoveAt(index);
            AdjustSelectedIndexAfterRemoveAt(index);
            RefreshAfterOptionsChanged();
            return true;
        }

        /// <summary>
        /// Removes all options and clears the current selection.
        /// </summary>
        public void ClearOptions()
        {
            if (DropdownOptions == null)
            {
                return;
            }

            DropdownOptions.Options ??= new List<string>();
            DropdownOptions.Options.Clear();
            DropdownOptions.SelectedIndex = -1;
            RefreshAfterOptionsChanged();
        }

        /// <summary>
        /// Selects the option at <paramref name="index"/>, updates the main label, refreshes the list if it was bound,
        /// collapses the dropdown like a list click, and invokes <see cref="OnValueChanged"/> when the index actually changes.
        /// </summary>
        /// <returns><c>true</c> if the index is in range; otherwise <c>false</c>.</returns>
        public bool SetSelect(int index)
        {
            if (DropdownOptions?.Options == null || index < 0 || index >= DropdownOptions.Options.Count)
            {
                return false;
            }

            int previousIndex = DropdownOptions.SelectedIndex;
            DropdownOptions.SelectedIndex = index;
            string selectedText = DropdownOptions.Options[index];

            if (View.TryGetVisuals(out DropdownVisuals visuals))
            {
                visuals.InitSelectionLabel(selectedText);
                visuals.RefreshDataSourceList();
                visuals.Collapse();
            }

            if (index != previousIndex)
            {
                OnValueChanged?.Invoke(selectedText);
            }

            return true;
        }

        /// <summary>
        /// Selects the first option equal to <paramref name="value"/> using case-insensitive comparison.
        /// </summary>
        /// <inheritdoc cref="SetSelect(int)"/>
        public bool SetSelect(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || DropdownOptions?.Options == null)
            {
                return false;
            }

            int index = IndexOfOptionIgnoreCase(value, DropdownOptions.Options);
            if (index < 0)
            {
                return false;
            }

            return SetSelect(index);
        }

        private static int IndexOfOptionIgnoreCase(string option, List<string> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], option, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void AdjustSelectedIndexAfterRemoveAt(int removedIndex)
        {
            if (DropdownOptions == null)
            {
                return;
            }

            if (DropdownOptions.SelectedIndex == removedIndex)
            {
                DropdownOptions.SelectedIndex = -1;
            }
            else if (DropdownOptions.SelectedIndex > removedIndex)
            {
                DropdownOptions.SelectedIndex--;
            }

            int count = DropdownOptions.Options?.Count ?? 0;
            if (count == 0)
            {
                DropdownOptions.SelectedIndex = -1;
            }
            else if (DropdownOptions.SelectedIndex >= count)
            {
                DropdownOptions.SelectedIndex = count - 1;
            }
        }

        private void RefreshAfterOptionsChanged()
        {
            if (!View.TryGetVisuals(out DropdownVisuals visuals))
            {
                return;
            }

            visuals.InitSelectionLabel(DropdownOptions.CurrentSelection);
            visuals.RefreshDataSourceList();
        }

        /// <summary>
        /// The visuals associated with this dropdown control
        /// </summary>
        private DropdownVisuals Visuals => View.Visuals as DropdownVisuals;

        public void Expand()
        {
            // Tell the dropdown to expand, showing a list of
            // selectable options.
            Visuals.Expand(DropdownOptions);
        }

        public void Collapse()
        {
            // Collapse the dropdown and stop tracking it
            // as the expanded focused object.
            Visuals.Collapse();
        }

        private void OnEnable()
        {
            if (View.TryGetVisuals(out DropdownVisuals visuals))
            {
                // Set default state
                visuals.UpdateVisualState(VisualState.Default);
            }

            // Subscribe to desired events
            View.UIBlock.AddGestureHandler<Gesture.OnHover, DropdownVisuals>(DropdownVisuals.HandleHovered);
            View.UIBlock.AddGestureHandler<Gesture.OnUnhover, DropdownVisuals>(DropdownVisuals.HandleUnhovered);
            View.UIBlock.AddGestureHandler<Gesture.OnPress, DropdownVisuals>(DropdownVisuals.HandlePressed);
            View.UIBlock.AddGestureHandler<Gesture.OnRelease, DropdownVisuals>(DropdownVisuals.HandleReleased);
            View.UIBlock.AddGestureHandler<Gesture.OnCancel, DropdownVisuals>(DropdownVisuals.HandlePressCanceled);
            View.UIBlock.AddGestureHandler<Gesture.OnClick, DropdownVisuals>(HandleDropdownClicked);

            Visuals.OnValueChanged += HandleValueChanged;
            InputManager.OnPostClick += HandlePostClick;

            // Ensure label is initialized
            Visuals.InitSelectionLabel(DropdownOptions.CurrentSelection);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            View.UIBlock.RemoveGestureHandler<Gesture.OnHover, DropdownVisuals>(DropdownVisuals.HandleHovered);
            View.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, DropdownVisuals>(DropdownVisuals.HandleUnhovered);
            View.UIBlock.RemoveGestureHandler<Gesture.OnPress, DropdownVisuals>(DropdownVisuals.HandlePressed);
            View.UIBlock.RemoveGestureHandler<Gesture.OnRelease, DropdownVisuals>(DropdownVisuals.HandleReleased);
            View.UIBlock.RemoveGestureHandler<Gesture.OnCancel, DropdownVisuals>(DropdownVisuals.HandlePressCanceled);
            View.UIBlock.RemoveGestureHandler<Gesture.OnClick, DropdownVisuals>(HandleDropdownClicked);

            Visuals.OnValueChanged -= HandleValueChanged;
            InputManager.OnPostClick -= HandlePostClick;
        }

        /// <summary>
        /// Fire the Unity event when the selected value changes.
        /// </summary>
        /// <param name="value">The string in the list of selectable options.</param>
        private void HandleValueChanged(string value)
        {
            OnValueChanged?.Invoke(value);
        }

        /// <summary>
        /// Handle a <see cref="DropdownVisuals"/> object in the <see cref="ListView">
        /// being clicked, and either expand or collapse it accordingly.
        /// </summary>
        /// <param name="evt">The click event data.</param>
        /// <param name="dropdownControl">The <see cref="ItemVisuals"/> object which was clicked.</param>
        private void HandleDropdownClicked(Gesture.OnClick evt, DropdownVisuals dropdownControl)
        {
            if (evt.Receiver.transform.IsChildOf(dropdownControl.OptionsView.transform))
            {
                // The clicked object was not the dropdown itself but rather a list item within the dropdown.
                // The dropdownControl itself will handle this event, so we don't need to do anything here.
                return;
            }

            // Toggle the expanded state of the dropdown on click

            if (dropdownControl.IsExpanded)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        /// <summary>
        /// Handles unfocusing the <see cref="Dropdown"/> if the user clicks somewhere else.
        /// </summary>
        private void HandlePostClick(UIBlock clickedUIBlock)
        {
            if (!Visuals.IsExpanded)
            {
                return;
            }

            if (clickedUIBlock == null || !clickedUIBlock.transform.IsChildOf(transform))
            {
                // Clicked somewhere else, so remove focus.
                Collapse();
            }
        }
    }
}

