using System.Windows.Controls;
using System.Windows.Input;

namespace PRISM.App.Views;

public partial class AudioStudioView : UserControl
{
    public AudioStudioView()
    {
        InitializeComponent();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta * 0.8));
            e.Handled = true;
        }
    }
}
