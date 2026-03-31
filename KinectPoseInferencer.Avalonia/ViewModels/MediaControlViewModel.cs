using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KinectPoseInferencer.Avalonia.Messages;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class MediaControlViewModel : ViewModelBase, IRecipient<OperationModeChangedMessage>, IRecipient<PlaybackStateChangedMessage>
{
    readonly IMessenger _messenger;

    [ObservableProperty] string _playPauseIconUnicode = PlayIconUnicode;
    [ObservableProperty] string _secondaryButtonIconUnicode = RewindIconUnicode;

    const string PlayIconUnicode = "\uE768";
    const string PauseIconUnicode = "\uE769";
    const string RewindIconUnicode = "\uE892";
    const string StopIconUnicode = "\uE71A";

    OperationMode _currentMode = OperationMode.Playback;

    public MediaControlViewModel(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.RegisterAll(this);
    }

    [RelayCommand]
    void PlayOrPause()
    {
        _messenger.Send(new PlayPauseRequestedMessage());
    }

    [RelayCommand]
    void SecondaryButton()
    {
        _messenger.Send(new SecondaryActionRequestedMessage());
    }

    public void Receive(OperationModeChangedMessage message)
    {
        _currentMode = message.Mode;
        SecondaryButtonIconUnicode = _currentMode == OperationMode.Playback ? RewindIconUnicode : StopIconUnicode;
    }

    public void Receive(PlaybackStateChangedMessage message)
    {
        PlayPauseIconUnicode = message.IsPlaying ? PauseIconUnicode : PlayIconUnicode;
    }
}
