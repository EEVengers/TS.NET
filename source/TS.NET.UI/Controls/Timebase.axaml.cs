using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TS.NET.UI.Controls
{
    public partial class Timebase : UserControl
    {
        public Timebase()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
