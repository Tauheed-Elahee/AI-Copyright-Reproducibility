using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AICopyrightReproducibility.Gui.Controls
{
    // AutoCompleteBox subclass with correct arrow-key navigation.
    //
    // Avalonia's AutoCompleteBox processes Up/Down inside TextBox_KeyDown (subscribed
    // to the inner TextBox's KeyDown event). That handler runs before the event bubbles,
    // so overriding OnKeyDown is too late — the selection and Text have already changed.
    //
    // Fix: subscribe with RoutingStrategies.Tunnel so we fire FIRST, mark Up/Down as
    // handled (TextBox_KeyDown never runs), then move the dropdown selection ourselves
    // while suppressing OnAdapterSelectionChanged via reflection so Text stays unchanged.
    // Enter is left unhandled so TextBox_KeyDown commits the highlighted item normally.
    public sealed class NavAutoCompleteBox : AutoCompleteBox
    {
        protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

        // _ignorePropertyChange is a private bool in AutoCompleteBox that gates
        // OnAdapterSelectionChanged. Setting it true while we move SelectedIndex
        // prevents that handler from calling UpdateTextCompletion, which would
        // otherwise push the item text into the Text property (and the ViewModel).
        private static readonly FieldInfo? s_ignorePropertyChange =
            typeof(AutoCompleteBox).GetField(
                "_ignorePropertyChange",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly bool s_isInt =
            s_ignorePropertyChange?.FieldType == typeof(int);

        private ListBox? _selector;

        public NavAutoCompleteBox()
        {
            AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _selector = e.NameScope.Find<ListBox>("PART_SelectingItemsControl");
        }

        private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
        {
            if (_selector == null || !IsDropDownOpen)
                return;

            int count = _selector.ItemCount;

            switch (e.Key)
            {
                case Key.Down when count > 0:
                {
                    int cur = _selector.SelectedIndex;
                    MoveSelection(cur < 0 ? 0 : Math.Min(cur + 1, count - 1));
                    e.Handled = true;
                    break;
                }
                case Key.Up when count > 0:
                {
                    int cur = _selector.SelectedIndex;
                    MoveSelection(cur < 0 ? count - 1 : Math.Max(cur - 1, 0));
                    e.Handled = true;
                    break;
                }
                case Key.Enter:
                {
                    // SelectionAdapter's internal state is null because we suppressed
                    // OnAdapterSelectionChanged during MoveSelection, so HandleKeyDown
                    // (called by TextBox_KeyDown) would see no selected item.
                    // Read directly from the ListBox instead.
                    if (_selector.SelectedItem is string selected)
                    {
                        Text = selected;
                        IsDropDownOpen = false;
                        e.Handled = true;
                    }
                    // If nothing highlighted, fall through so the base can handle Enter normally.
                    break;
                }
            }
        }

        private void MoveSelection(int newIndex)
        {
            // Temporarily suppress OnAdapterSelectionChanged so changing SelectedIndex
            // does not cascade into UpdateTextCompletion → ViewModel text update.
            if (s_ignorePropertyChange != null)
            {
                object suppress = s_isInt ? (object)1 : true;
                object restore = s_isInt ? (object)0 : false;
                s_ignorePropertyChange.SetValue(this, suppress);
                try { _selector!.SelectedIndex = newIndex; }
                finally { s_ignorePropertyChange.SetValue(this, restore); }
            }
            else
            {
                _selector!.SelectedIndex = newIndex;
            }

            if (_selector!.SelectedItem != null)
                _selector.ScrollIntoView(_selector.SelectedItem);
        }
    }
}
