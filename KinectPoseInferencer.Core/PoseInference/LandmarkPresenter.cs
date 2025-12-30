using K4AdotNet.Sensor;
using R3;
using ZLinq;

using HumanLandmarks;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference.Filters;
using KinectPoseInferencer.Core.PoseInference.Utils;


namespace KinectPoseInferencer.Core.PoseInference;

public class LandmarkPresenter: IDisposable
{
    readonly KinectInferencer _inferencer;
    readonly ResultManager _resultManager;
    readonly SkeletonToPoseLandmarksConverter _converter;
    readonly RecordDataBroker _recordDataBroker;
    readonly FrameManager _frameManager;
    readonly IPlaybackReader _playbackReader;
    readonly KinectDeviceController _kinectDeviceController;
    readonly TiltCorrector _tiltCorrector = new();

    readonly IEnumerable<ILandmarkUser> _resultUsers;
    readonly IEnumerable<ILandmarkFilter> _landmarkFilterChain;

    bool _isKinectEnabled = true;
    public bool IsKinectEnabled
    {
        get => _isKinectEnabled;
        set
        {
            _isKinectEnabled = value;
            UpdateResultManagerSettings(_isKinectEnabled);
        }
    }

    Calibration? _currentCalibration = null;
    DisposableBag _disposables = new();

    public LandmarkPresenter(
        KinectInferencer inferencer,
        ResultManager resultManager,
        SkeletonToPoseLandmarksConverter converter,
        IEnumerable<ILandmarkFilter> landmarkFilterChain,
        IEnumerable<ILandmarkUser> resultUsers,
        IPlaybackReader playbackReader,
        KinectDeviceController kinectDeviceController,
        RecordDataBroker recordDataBroker,
        FrameManager frameManager,
        TiltCorrector tiltCorrector
    )
    {
        _inferencer = inferencer ?? throw new ArgumentNullException(nameof(inferencer));
        _resultManager = resultManager ?? throw new ArgumentNullException(nameof(resultManager));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _recordDataBroker = recordDataBroker ?? throw new ArgumentNullException(nameof(recordDataBroker));
        _frameManager = frameManager ?? throw new ArgumentNullException(nameof(frameManager));
        _playbackReader = playbackReader ?? throw new ArgumentNullException(nameof(playbackReader));
        _kinectDeviceController = kinectDeviceController ?? throw new ArgumentNullException(nameof(kinectDeviceController));
        _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        _resultUsers = resultUsers;
        _landmarkFilterChain = landmarkFilterChain;

        // Initailize
        _playbackReader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback =>
            {
                playback.GetCalibration(out var calibration);
                _currentCalibration = calibration;
                Configure();
            })
            .AddTo(ref _disposables);
        _kinectDeviceController.KinectDevice
            .Where(device => device is not null)
            .Subscribe(device =>
            {
                _currentCalibration = _kinectDeviceController.GetCalibration();
                Configure();
            })
            .AddTo(ref _disposables);

        if (_resultManager.Result.KinectPoseLandmarks is null)
            _resultManager.Result.KinectPoseLandmarks = new();

        // Process frames
        _recordDataBroker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture => {
                if (!_inferencer.TryEnqueueData(capture)) return;

                using var frame = _inferencer.ProcessFrame();
                if (frame is null) return;
                _frameManager.Frame = frame.DuplicateReference();
                _recordDataBroker.SetBodyFrame(frame);
            })
            .AddTo(ref _disposables);
        _recordDataBroker.Imu
            .Subscribe(imu => {
                if (_currentCalibration is Calibration calibration)
                    _tiltCorrector.UpdateTiltRotation(imu, calibration);
                })
            .AddTo(ref _disposables);

        _inferencer.Result
            .Subscribe(result => {
                if(_isKinectEnabled)
                    ProcessResult(result);

                foreach(var user in _resultUsers)
                    user.Process(_resultManager.Result);
            })
            .AddTo(ref _disposables);
    }

    void ProcessResult(KinectInferenceResult result)
    {
        var kinectLandmarks = _converter.Convert(result.Skeleton);
        var resultLandmark = kinectLandmarks.Landmarks
            .AsValueEnumerable()
            .Where(nullableLandmark => nullableLandmark is Landmark landmark)
            .Select(landmark =>
            {
                // Apply filters to landmark
                return _landmarkFilterChain
                            .AsValueEnumerable()
                            .Aggregate(landmark,
                                (current, filter) => filter.Apply(current, result.Timestamp)
                            );
            })
            .ToList();

        _resultManager?.Result?.KinectPoseLandmarks?.Landmarks?.Clear();
        _resultManager?.Result?.KinectPoseLandmarks?.Landmarks?.AddRange(resultLandmark);
    }

    void Configure()
    {
        if(_currentCalibration is K4AdotNet.Sensor.Calibration calibration)
            _inferencer.Configure(calibration);
    }

    void UpdateResultManagerSettings(bool isKinectEnabled)
    {
        // set flag
        var flag = isKinectEnabled 
            ? _resultManager.ReceiverSetting | ReceiverEventSettings.Body
            : _resultManager.ReceiverSetting & ~ReceiverEventSettings.Body;

        _resultManager.UpdateSettings(flag);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
