using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RegexLab.Core;

namespace RegexLab;

public partial class MainWindow : Window
{
    private const string ExampleExpression = @"[\w.+-]+@[\w-]+\.[\w.-]+";
    private const string ExampleText =
        "Почта: ivan.petrov@example.com\n" +
        "Резерв: support@regexlab.ru\n" +
        "Телефон: +7 (900) 123-45-67";

    public MainWindow()
    {
        InitializeComponent();
        UpdateTextLength();
        ResetResult();
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        Analyze();
    }

    private void Analyze()
    {
        var expression = ExpressionInput.Text;
        var text = TestInput.Text;

        if (expression.Length == 0 || text.Length == 0)
        {
            var message = (expression.Length, text.Length) switch
            {
                (0, 0) => "Введите регулярное выражение и строку для проверки.",
                (0, _) => "Поле «Регулярное выражение» не может быть пустым.",
                _ => "Поле «Строка для проверки» не может быть пустым."
            };

            ShowStatus(StatusKind.Warning, "Недостаточно данных", message);
            MatchesHeader.Text = "проверка не выполнена";

            if (text.Length == 0)
            {
                ShowPlaceholder("Строка пуста.");
                TestInput.Focus();
            }
            else
            {
                RenderText(text, []);
                ExpressionInput.Focus();
            }

            return;
        }

        if (!RegexPattern.TryCompile(
                expression,
                IgnoreCaseCheckBox.IsChecked == true,
                out var pattern,
                out var error))
        {
            ShowStatus(
                StatusKind.Error,
                "Некорректное выражение",
                $"{error!.Message} Позиция: {error.Position + 1}.");

            MatchesHeader.Text = "выражение не распознано";
            RenderText(text, []);

            ExpressionInput.Focus();
            ExpressionInput.Select(Math.Min(error.Position, expression.Length), 0);
            return;
        }

        var matches = pattern!.FindMatches(text);
        var matchWord = Pluralize(matches.Count, "совпадение", "совпадения", "совпадений");

        if (matches.Count == 0)
        {
            ShowStatus(
                StatusKind.Info,
                "Совпадений не найдено",
                "Выражение корректно, но в строке нет подходящих фрагментов.");

            MatchesHeader.Text = "0 совпадений";
            RenderText(text, []);
            return;
        }

        ShowStatus(
            StatusKind.Success,
            $"Найдено {matches.Count} {matchWord}",
            matches.Count == 1
                ? "Фрагмент выделен в тексте ниже."
                : "Фрагменты выделены в тексте ниже.");

        MatchesHeader.Text = $"{matches.Count} {matchWord}";
        RenderText(text, matches);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ExpressionInput.Clear();
        TestInput.Clear();
        IgnoreCaseCheckBox.IsChecked = false;
        ResetResult();
        ExpressionInput.Focus();
    }

    private void ExampleButton_Click(object sender, RoutedEventArgs e)
    {
        ExpressionInput.Text = ExampleExpression;
        TestInput.Text = ExampleText;
        IgnoreCaseCheckBox.IsChecked = false;
        Analyze();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Analyze();
            e.Handled = true;
        }
    }

    private void TestInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTextLength();
    }

    private void UpdateTextLength()
    {
        var count = TestInput.Text.Length;
        TextLengthLabel.Text = $"{count} {Pluralize(count, "символ", "символа", "символов")}";
    }

    private void ResetResult()
    {
        ShowStatus(
            StatusKind.Idle,
            "Готово к проверке",
            "Введите выражение и строку, затем нажмите «Проверить».");

        MatchesHeader.Text = "ожидание проверки";
        ShowPlaceholder("Здесь появится текст с выделенными совпадениями.");
    }

    private void ShowStatus(StatusKind kind, string title, string message)
    {
        var (background, accent, icon) = kind switch
        {
            StatusKind.Success => (GetBrush("SuccessSoftBrush"), GetBrush("SuccessBrush"), "✓"),
            StatusKind.Warning => (GetBrush("WarningSoftBrush"), GetBrush("WarningBrush"), "!"),
            StatusKind.Error => (GetBrush("DangerSoftBrush"), GetBrush("DangerBrush"), "×"),
            StatusKind.Info => (GetBrush("InfoSoftBrush"), GetBrush("InfoBrush"), "i"),
            _ => (GetBrush("SurfaceMutedBrush"), GetBrush("TextSecondaryBrush"), "•")
        };

        StatusBanner.Background = background;
        StatusBanner.BorderBrush = accent;
        StatusIconCircle.Background = GetBrush("SurfaceBrush");
        StatusIcon.Foreground = accent;
        StatusIcon.Text = icon;
        StatusTitle.Foreground = accent;
        StatusTitle.Text = title;
        StatusMessage.Foreground = GetBrush("TextSecondaryBrush");
        StatusMessage.Text = message;
    }

    private void RenderText(string text, IReadOnlyList<RegexMatch> matches)
    {
        HighlightedText.Inlines.Clear();
        HighlightedText.Foreground = GetBrush("TextPrimaryBrush");

        var position = 0;

        foreach (var match in matches)
        {
            if (match.Start > position)
            {
                HighlightedText.Inlines.Add(new Run(text[position..match.Start]));
            }

            HighlightedText.Inlines.Add(new Run(text.Substring(match.Start, match.Length))
            {
                Background = GetBrush("HighlightBrush"),
                Foreground = GetBrush("HighlightTextBrush"),
                FontWeight = FontWeights.SemiBold
            });

            position = match.End;
        }

        if (position < text.Length)
        {
            HighlightedText.Inlines.Add(new Run(text[position..]));
        }
    }

    private void ShowPlaceholder(string message)
    {
        HighlightedText.Inlines.Clear();
        HighlightedText.Foreground = GetBrush("TextMutedBrush");
        HighlightedText.Inlines.Add(new Run(message));
    }

    private SolidColorBrush GetBrush(string key) => (SolidColorBrush)FindResource(key);

    private static string Pluralize(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        var last = count % 10;

        if (lastTwo is >= 11 and <= 14)
        {
            return many;
        }

        return last switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }

    private enum StatusKind
    {
        Idle,
        Success,
        Warning,
        Error,
        Info
    }
}
