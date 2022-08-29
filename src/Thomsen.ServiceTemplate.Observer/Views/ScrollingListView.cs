using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Thomsen.ServiceTemplate.Observer.Views;

internal class ScrollingListView : ListView {
    public static readonly DependencyProperty IsScrollingEnabledProperty = DependencyProperty.Register(
        nameof(IsScrollingEnabled),
        typeof(bool),
        typeof(ScrollingListView));

    public bool IsScrollingEnabled {
        get => (bool)GetValue(IsScrollingEnabledProperty);
        set => SetValue(IsScrollingEnabledProperty, value);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
        int newItemCount = e.NewItems?.Count ?? 0;

        if (IsScrollingEnabled && newItemCount > 0) {
            ScrollIntoView(e.NewItems![newItemCount - 1]);
        }

        base.OnItemsChanged(e);
    }
}
