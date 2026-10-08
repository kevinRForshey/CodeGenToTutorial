using Avalonia;
using Avalonia.Data;

using AvaloniaEdit;

namespace CodeGenToTutorial.Avalonia.Helpers;

// AvaloniaEdit's TextEditor.Text is a plain CLR property (backed by its Document), not an
// AvaloniaProperty, so it can't be the target of an XAML Binding directly. This attached property
// bridges the two ways: pushing a bound value onto Text when the source changes, and (via a
// TextChanged hook, wired up once per editor) pushing the user's edits back onto the bound value
// so a TwoWay binding round-trips.
public static class TextEditorExtensions
{
    public static readonly AttachedProperty<string?> BindableTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>(
            "BindableText", typeof(TextEditorExtensions), defaultBindingMode: BindingMode.TwoWay);

    private static readonly AttachedProperty<bool> IsHookedProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("IsHooked", typeof(TextEditorExtensions));

    static TextEditorExtensions()
    {
        BindableTextProperty.Changed.AddClassHandler<TextEditor>(OnBindableTextChanged);
    }

    public static void SetBindableText(TextEditor editor, string? value) => editor.SetValue(BindableTextProperty, value);

    public static string? GetBindableText(TextEditor editor) => editor.GetValue(BindableTextProperty);

    private static void OnBindableTextChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (!editor.GetValue(IsHookedProperty))
        {
            editor.SetValue(IsHookedProperty, true);
            editor.TextChanged += (_, _) => editor.SetValue(BindableTextProperty, editor.Text);
        }

        var newText = e.NewValue as string ?? string.Empty;
        if (editor.Text != newText)
        {
            editor.Text = newText;
        }
    }
}
