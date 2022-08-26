using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using static System.Net.Mime.MediaTypeNames;

namespace Thomsen.ServiceTemplate.Observer {
    internal class ScrollingListView : ListView {
        public static readonly DependencyProperty IsScrollingEnabledProperty = DependencyProperty.Register(
            nameof(IsScrollingEnabled),
            typeof(bool),
            typeof(ScrollingListView));

        public bool IsScrollingEnabled {
            get => (bool)GetValue(IsScrollingEnabledProperty);
            set => SetValue(IsScrollingEnabledProperty, value);
        }

        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            int newItemCount = e.NewItems?.Count ?? 0;

            if (IsScrollingEnabled && newItemCount > 0) {
                ScrollIntoView(e.NewItems![newItemCount - 1]);
            }

            base.OnItemsChanged(e);
        }
    }
}
