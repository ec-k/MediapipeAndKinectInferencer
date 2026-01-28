namespace KinectPoseInferencer.Core.PoseInference.Filters;

public class LandmarkFilterFactory
{
    readonly OneEuroFilterSettings _oneEuroSettings;
    readonly ILandmarkFilter _mmToMeter;
    readonly ILandmarkFilter _tiltCorrector;
    readonly ILandmarkFilter _transformCoordinator;

    public LandmarkFilterFactory(
        OneEuroFilterSettings oneEuroSettings,
        MilimeterToMeter mmToMeter,
        TiltCorrector tiltCorrector,
        TransformCoordinator transformCoordinator)
    {
        _oneEuroSettings = oneEuroSettings;
        _mmToMeter = mmToMeter;
        _tiltCorrector = tiltCorrector;
        _transformCoordinator = transformCoordinator;
    }

    public IEnumerable<ILandmarkFilter> CreateFilterStack(int jointIndex)
    {
        return new List<ILandmarkFilter>
        {
            _mmToMeter,
            _tiltCorrector,

            // Each joint must have its own filter instances.
            new JointBasisCorrector(jointIndex),
            new OneEuroFilter(_oneEuroSettings.MinCutoff, _oneEuroSettings.Slope, _oneEuroSettings.DCutoff),

            _transformCoordinator,
        };
    }
}
