using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Media3D;

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

        _viewModel.ModelVisuals.CollectionChanged += ModelVisuals_CollectionChanged;
        VideoWindowBorder.SizeChanged += OnVideoWindowSizeChanged;
    }

    void ModelVisuals_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            View3D.Children.Clear();
        }

        //if (e.Action != NotifyCollectionChangedAction.Reset && e.OldItems is not null)
        //{
        //    foreach (ModelVisual3D model in e.OldItems)
        //    {
        //        View3D.Children.Remove(model);
        //    }
        //}

        if (e.NewItems is not null)
        {
            foreach (ModelVisual3D model in e.NewItems)
            {
                View3D.Children.Add(model);
            }
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
        _viewModel.ModelVisuals.CollectionChanged -= ModelVisuals_CollectionChanged;
        _viewModel?.Dispose();
    }
}
