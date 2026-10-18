using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

using Paarde.Avalonia.ViewModels;
using Paarde.Core.Contracts.Services;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class TutorialDetailControl : UserControl
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IFileChangeApplyService _fileChangeApplyService;
    private readonly ILlmRunner _llmRunner;

    public ObservableCollection<TutorialStepViewModel> Steps { get; } = new();

    public TutorialDetailControl()
    {
        _promptResultStore = App.GetService<IPromptResultStore>();
        _fileChangeApplyService = App.GetService<IFileChangeApplyService>();
        _llmRunner = App.GetService<ILlmRunner>();
        InitializeComponent();

        StepsItemsControl.ItemsSource = Steps;
        DataContextChanged += (_, _) => RebuildSteps();

        // CanUndo/CanRedo are plain CLR properties on AvaloniaEdit's TextEditor, not
        // AvaloniaProperties, so they can't be bound from XAML - TextChanged (which also fires on
        // Undo()/Redo() themselves) is the hook used to keep the buttons' enabled state in sync.
        Editor.TextChanged += (_, _) => RefreshUndoRedoButtons();
        RefreshUndoRedoButtons();
    }

    private void RefreshUndoRedoButtons()
    {
        UndoButton.IsEnabled = Editor.CanUndo;
        RedoButton.IsEnabled = Editor.CanRedo;
    }

    private void OnUndoButtonClick(object? sender, RoutedEventArgs e)
    {
        Editor.Undo();
        RefreshUndoRedoButtons();
    }

    private void OnRedoButtonClick(object? sender, RoutedEventArgs e)
    {
        Editor.Redo();
        RefreshUndoRedoButtons();
    }

    private void RebuildSteps()
    {
        Steps.Clear();

        if (DataContext is not ProposedFileChangeViewModel proposedFileChange)
        {
            return;
        }

        foreach (var hunk in proposedFileChange.Hunks)
        {
            Steps.Add(new TutorialStepViewModel(hunk, proposedFileChange, _promptResultStore.WorkingDirectoryPath, _promptResultStore.SessionId, _fileChangeApplyService, _llmRunner));
        }
    }
}
