using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class TutorialDetailControl : UserControl
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IFileChangeApplyService _fileChangeApplyService;

    public ObservableCollection<TutorialStepViewModel> Steps { get; } = new();

    public TutorialDetailControl()
    {
        _promptResultStore = App.GetService<IPromptResultStore>();
        _fileChangeApplyService = App.GetService<IFileChangeApplyService>();
        InitializeComponent();

        StepsItemsControl.ItemsSource = Steps;
        DataContextChanged += (_, _) => RebuildSteps();
    }

    private void RebuildSteps()
    {
        Steps.Clear();

        if (DataContext is not ProposedFileChange proposedFileChange)
        {
            return;
        }

        foreach (var step in proposedFileChange.TutorialSteps)
        {
            Steps.Add(new TutorialStepViewModel(step, proposedFileChange, _promptResultStore.WorkingDirectoryPath, _fileChangeApplyService, MarkAllStepsApplied));
        }
    }

    private void MarkAllStepsApplied()
    {
        foreach (var step in Steps)
        {
            step.IsApplied = true;
        }
    }
}
