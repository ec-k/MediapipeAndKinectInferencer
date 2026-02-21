using HumanLandmarks;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference.Filters;
using KinectPoseInferencer.Core.PoseInference.Utils;
using R3;
using ZLinq;
using K4APlayback = K4AdotNet.Record.Playback;
using K4APlaybackException = K4AdotNet.Record.PlaybackException;

namespace KinectPoseInferencer.Core.PoseInference;

public class LandmarkPresenter : IDisposable
{
    readonly KinectInferencer _inferencer;
    readonly ResultManager _resultManager;
    readonly SkeletonToPoseLandmarksConverter _converter;
    readonly RecordDataBroker _recordDataBroker;
    readonly FrameManager _frameManager;
    readonly IPlaybackReader _playbackReader;
    readonly IPlaybackController _playbackController;
    readonly KinectDeviceController _kinectDeviceController;
    readonly TiltCorrector _tiltCorrector;

    readonly IEnumerable<ILandmarkUser> _resultUsers;
    readonly Dictionary<int, JointFilterPipeline> _jointFilterPipelines = new();
    readonly LandmarkFilterFactory _filterFactory;

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
        LandmarkFilterFactory filterFactory,
        IEnumerable<ILandmarkUser> resultUsers,
        IPlaybackReader playbackReader,
        IPlaybackController playbackController,
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
        _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
        _kinectDeviceController = kinectDeviceController ?? throw new ArgumentNullException(nameof(kinectDeviceController));
        _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        _filterFactory = filterFactory ?? throw new ArgumentNullException(nameof(filterFactory));
        _resultUsers = resultUsers;

        // Reset tracker on seek/rewind to clear internal queue
        _playbackController.OnSeek += () => _inferencer.Reset();

        // Initialize calibration from playback
        _playbackReader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => LoadCalibrationFromPlayback(playback!))
            .AddTo(ref _disposables);

        // Initialize calibration from live device
        _kinectDeviceController.KinectDevice
            .Where(device => device is not null)
            .Subscribe(_ =>
            {
                _currentCalibration = _kinectDeviceController.GetCalibration();
                Configure();
            })
            .AddTo(ref _disposables);

        if (_resultManager.Result.KinectPoseLandmarks is null)
            _resultManager.Result.KinectPoseLandmarks = new();

        // Subscribe to IMU data for tilt correction
        _recordDataBroker.Imu
            .Subscribe(imu =>
            {
                if (_currentCalibration is Calibration calibration)
                    _tiltCorrector.UpdateTiltRotation(imu, calibration);
            })
            .AddTo(ref _disposables);

        // Subscribe to inference results from KinectInferencer
        _inferencer.Result
            .Where(result => result is not null)
            .Subscribe(result =>
            {
                if (_isKinectEnabled)
                    ProcessResult(result!);

                foreach (var user in _resultUsers)
                    user.Process(_resultManager.Result);
            })
            .AddTo(ref _disposables);
    }

    void LoadCalibrationFromPlayback(K4APlayback playback)
    {
        Calibration calibration = default;
        bool isCalibrationLoaded = false;

        try
        {
            playback.GetCalibration(out calibration);
            isCalibrationLoaded = true;
        }
        catch (K4APlaybackException)
        {
            // Clipped video by k4acut may not have calibration data,
            // but it may have custom calibration stored in tags.
            if (playback.TryGetTag("CUSTOM_CALIBRATION_RAW", out var base64))
            {
                var rawData = Convert.FromBase64String(base64);
                playback.GetRecordConfiguration(out var recordConfig);
                Calibration.CreateFromRaw(rawData, recordConfig.DepthMode, recordConfig.ColorResolution, out calibration);
                isCalibrationLoaded = calibration.IsValid;
            }
        }

        if (isCalibrationLoaded)
        {
            _currentCalibration = calibration;
            Configure();
        }
    }

    void ProcessResult(KinectInferenceResult result)
    {
        var resultLandmarks = _converter.Convert(result.Skeleton).Landmarks
            .AsValueEnumerable()
            .Where(nullableLandmark => nullableLandmark is Landmark landmark)
            .Select((landmark, index) =>
            {
                if (!_jointFilterPipelines.TryGetValue(index, out var pipeline))
                {
                    var filters = _filterFactory.CreateFilterStack(index);
                    pipeline = new JointFilterPipeline(filters);
                    _jointFilterPipelines[index] = pipeline;
                }

                return pipeline.Apply(landmark, result.Timestamp);
            })
            .ToList();

        _resultManager.Result.KinectPoseLandmarks = new()
        {
            Landmarks = { resultLandmarks }
        };
    }

    void Configure()
    {
        if (_currentCalibration is Calibration calibration)
            _inferencer.SetCalibration(calibration);
    }

    void UpdateResultManagerSettings(bool isKinectEnabled)
    {
        var flag = !isKinectEnabled
            ? _resultManager.ReceiverSetting | ReceiverEventSettings.Body
            : _resultManager.ReceiverSetting & ~ReceiverEventSettings.Body;

        _resultManager.UpdateSettings(flag);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
