using CommunityToolkit.Mvvm.Input;
using KinectPoseInferencer.Core.Playback;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand(IncludeCancelCommand = true)]
    async Task LoadFiles(CancellationToken token)
    {
        if (string.IsNullOrEmpty(VideoFilePath)) return;

        _settingsManager.Save(new()
        {
            VideoFilePath = VideoFilePath,
            InputLogFilePath = InputLogFilePath,
            MetaFilePath = MetaFilePath,
        });

        try
        {
            var playbackDesc = new PlaybackDescriptor(VideoFilePath, InputLogFilePath, MetaFilePath);
            _playbackController.Descriptor = playbackDesc;
            await _playbackController.Prepare(token);

            // Display the first frame after successful loading
            if (_playbackController.Reader.Playback.CurrentValue is K4AdotNet.Record.Playback playback)
            {
                await Task.Run(() => DisplayFirstColorFrame(playback, token), token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File loading was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading file: {ex.Message}");
        }
    }

    partial void OnSeekSliderPositionChanged(double value)
    {
        if (_isInternalUpdating) return;

        var targetTime = TimeSpan.FromSeconds(value);
        CurrentTime = targetTime;
    }

    public void ConfirmSeek()
    {
        _playbackController.SeekAsync(TimeSpan.FromSeconds(SeekSliderPosition));
    }

    void PlaybackPlayOrPause()
    {
        var state = _playbackController.State.CurrentValue;

        if (state is PlaybackState.Playing)
            _playbackController.Pause();
        if (state is PlaybackState.Pause)
            _playbackController.Play();
    }
}
