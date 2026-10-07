using Avalonia;

using AvaloniaEdit;

namespace CodeGenToTutorial.Avalonia.Helpers;

// AvaloniaEdit's TextEditor.Text is a plain CLR property (backed by its Document), not an
// AvaloniaProperty, so it can't be the target of an XAML Binding directly. This attached property
// mirrors a bound string onto Text whenever the binding pushes a new value.
public static class TextEditorExtensions
{
    public static readonly AttachedProperty<string?> BindableTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("BindableText", typeof(TextEditorExtensions));

    static TextEditorExtensions()
    {
        BindableTextProperty.Changed.AddClassHandler<TextEditor>((editor, e) => editor.Text = e.NewValue as string ?? string.Empty);
    }

    public static void SetBindableText(TextEditor editor, string? value) => editor.SetValue(BindableTextProperty, value);

    public static string? GetBindableText(TextEditor editor) => editor.GetValue(BindableTextProperty);
}
