using HumanLandmarks;
using KinectPoseInferencer.Core.PoseInference.Filters;


namespace KinectPoseInferencer.Core.PoseInference;

public class JointFilterPipeline
{
    readonly List<ILandmarkFilter> _filters;

    public JointFilterPipeline(IEnumerable<ILandmarkFilter> filters)
    {
        // Important: Use unique instances to avoid shared state.
        _filters = filters.ToList();
    }

    public Landmark Apply(Landmark input, float timestamp)
    {
        var current = input;
        foreach (var filter in _filters)
        {
            current = filter.Apply(current, timestamp);
        }
        return current;
    }
}
