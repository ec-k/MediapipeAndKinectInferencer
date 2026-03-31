using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KinectPoseInferencer.Avalonia.Views.Controls;

public partial class VideoPlayerPanel : UserControl
{
    const double VideoWindowIdealAspectRatio = 16.0 / 9.0;

    public VideoPlayerPanel()
    {
        InitializeComponent();

        VideoWindowBorder.SizeChanged += OnVideoWindowSizeChanged;
    }

    void Slider_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.PlaybackControl.ConfirmSeek();
        }
    }

    void OnVideoWindowSizeChanged(object? sender, SizeChangedEventArgs e)
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

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        VideoWindowBorder.SizeChanged -= OnVideoWindowSizeChanged;
    }
}
