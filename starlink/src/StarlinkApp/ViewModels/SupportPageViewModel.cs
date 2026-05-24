using System.Windows.Input;

namespace StarlinkApp.ViewModels;

public sealed class SupportPageViewModel : ViewModelBase
{
    private readonly Func<string, string> _submitFeedback;
    private string _feedbackText = "Connection looked unstable near the north window.";
    private string _submitStatusText = "Feedback is submitted only to the local simulator.";

    public SupportPageViewModel(Func<string, string> submitFeedback)
    {
        _submitFeedback = submitFeedback;
        SubmitFeedbackCommand = new RelayCommand(_ => SubmitFeedback());
    }

    public IReadOnlyList<string> SupportCards { get; } =
    [
        "Check Starlink placement and look for obstructions.",
        "Run an advanced speed test to compare device, router, and internet segments.",
        "Inspect connected devices if speed or latency changes unexpectedly."
    ];

    public ICommand SubmitFeedbackCommand { get; }

    public string FeedbackText
    {
        get => _feedbackText;
        set => SetProperty(ref _feedbackText, value);
    }

    public string SubmitStatusText
    {
        get => _submitStatusText;
        private set => SetProperty(ref _submitStatusText, value);
    }

    private void SubmitFeedback()
    {
        SubmitStatusText = _submitFeedback(FeedbackText);
    }
}
