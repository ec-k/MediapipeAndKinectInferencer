namespace KinectPoseInferencer.UI;

public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }
}
