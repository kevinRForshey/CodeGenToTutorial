using System.Collections.ObjectModel;

using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Models;
using CodeGenToTutorial.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeGenToTutorial.Views;

public sealed partial class TutorialDetailControl : UserControl
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IFileChangeApplyService _fileChangeApplyService;

    public ProposedFileChange? ProposedFileChange
    {
        get => GetValue(ProposedFileChangeProperty) as ProposedFileChange;
        set => SetValue(ProposedFileChangeProperty, value);
    }

    public static readonly DependencyProperty ProposedFileChangeProperty = DependencyProperty.Register(
        nameof(ProposedFileChange),
        typeof(ProposedFileChange),
        typeof(TutorialDetailControl),
        new PropertyMetadata(null, OnProposedFileChangeChanged));

    public ObservableCollection<TutorialStepViewModel> Steps { get; } = new();

    public TutorialDetailControl()
    {
        _promptResultStore = App.GetService<IPromptResultStore>();
        _fileChangeApplyService = App.GetService<IFileChangeApplyService>();
        InitializeComponent();
    }

    private static void OnProposedFileChangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TutorialDetailControl)d).RebuildSteps();

    private void RebuildSteps()
    {
        Steps.Clear();

        if (ProposedFileChange == null)
        {
            return;
        }

        foreach (var step in ProposedFileChange.TutorialSteps)
        {
            Steps.Add(new TutorialStepViewModel(step, ProposedFileChange, _promptResultStore.WorkingDirectoryPath, _fileChangeApplyService, MarkAllStepsApplied));
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
