using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace StandupReminder.App.Controls;

public partial class SessionLogView : UserControl
{
    public static readonly DependencyProperty EventLogProperty =
        DependencyProperty.Register(
            nameof(EventLog),
            typeof(IEnumerable),
            typeof(SessionLogView),
            new PropertyMetadata(null)
        );

    public IEnumerable? EventLog
    {
        get => (IEnumerable?)GetValue(EventLogProperty);
        set => SetValue(EventLogProperty, value);
    }

    public SessionLogView()
    {
        InitializeComponent();
    }
}
