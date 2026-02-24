using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using System;

namespace KinectPoseInferencer.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        const double VideoWindowIdealAspectRatio = 16.0 / 9.0;

        public MainWindow()
        {
            InitializeComponent();

            VideoWindowBorder.SizeChanged += OnVideoWindowSizeChanged;
        }

        void Slider_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.ConfirmSeek();
            }
        }

        void OnVideoWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e is null)
                return;

            var containerWidth = e.NewSize.Width;
            var containerHeight = e.NewSize.Height;
            var containerAspectRatio = containerWidth / containerHeight;

            if (containerAspectRatio > VideoWindowIdealAspectRatio)
            {
                var idealWidth = containerHeight * VideoWindowIdealAspectRatio;
                VideoWindowViewBox.MaxHeight = double.PositiveInfinity;
                VideoWindowViewBox.MaxWidth = idealWidth;

            }
            else
            {
                var idealHeight = containerWidth / VideoWindowIdealAspectRatio;
                VideoWindowViewBox.MaxHeight = idealHeight;
                VideoWindowViewBox.MaxWidth = double.PositiveInfinity;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            VideoWindowBorder.SizeChanged -= OnVideoWindowSizeChanged;

            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Explicitly shutdown the application
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}