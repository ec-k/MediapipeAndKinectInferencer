using System;
using System.Windows;


namespace KinectPoseInferencer.UI;

public partial class MainWindow
{
    const double VideoWindowIdealAspectRatio = 16.0 / 9.0;
    readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.DataContext = _viewModel;

        VideoWindowBorder.SizeChanged += OnVideoWindowSizeChanged;
    }

    void OnVideoWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
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
        _viewModel?.Dispose();
    }
}
