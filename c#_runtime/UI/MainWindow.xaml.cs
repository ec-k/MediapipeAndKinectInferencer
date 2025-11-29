using System;
using System.Windows;
using System.Collections.Generic;

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

        // _viewModel.UpdateVisuals += OnUpdateVisualsRequested; // Keep this line for now, but it might be removed
        VideoWindowBorder.SizeChanged += OnVideoWindowSizeChanged;

        // Subscribe to CollectionChanged event of BodyVisualElements // No longer needed
        _viewModel.UpdateVisuals += OnUpdateVisualsRequested; // Subscribe to new UpdateVisuals event

    }

    void OnUpdateVisualsRequested(List<UIElement> elements) // Change signature
    {
        DrawingCanvas.Children.Clear();
        foreach (var element in elements)
        {
            DrawingCanvas.Children.Add(element);
        }
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
        // _viewModel.UpdateVisuals -= OnUpdateVisualsRequested; // Keep this line for now
        _viewModel.UpdateVisuals -= OnUpdateVisualsRequested; // Unsubscribe from new UpdateVisuals event
        _viewModel?.Dispose();
    }
}
