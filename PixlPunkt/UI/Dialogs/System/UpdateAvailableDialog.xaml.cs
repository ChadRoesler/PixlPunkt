using System;
using System.Collections.Generic;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Settings;
using PixlPunkt.Core.Updates;
using Windows.UI;

// Aliases to disambiguate Markdig types from WinUI types
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using WinUIGrid = Microsoft.UI.Xaml.Controls.Grid;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Dialog shown when a new update is available.
    /// </summary>
    public sealed partial class UpdateAvailableDialog : ContentDialog
    {
        private readonly UpdateInfo _updateInfo;
        private readonly MarkdownPipeline _markdownPipeline;

        // Current RichTextBlock being built (for non-table content)
        private RichTextBlock? _currentTextBlock;

        /// <summary>
        /// Gets whether the user chose to skip this version.
        /// </summary>
        public bool SkipThisVersion { get; private set; }

        /// <summary>
        /// Gets whether auto-update checking is enabled.
        /// </summary>
        public bool AutoCheckEnabled => AutoCheckBox.IsChecked == true;

        /// <summary>
        /// Creates a new UpdateAvailableDialog.
        /// </summary>
        /// <param name="updateInfo">The update information to display.</param>
        public UpdateAvailableDialog(UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            // Configure Markdig with GitHub Flavored Markdown extensions
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions() // Includes tables, task lists, etc.
                .Build();

            // Set version texts
            CurrentVersionText.Text = $"Current: v{UpdateService.GetCurrentVersionString()}";
            NewVersionText.Text = $"New: v{updateInfo.Version}";

            // Show pre-release badge if applicable
            if (updateInfo.IsPreRelease)
            {
                PreReleaseBadge.Visibility = Visibility.Visible;
            }

            // Render markdown release notes
            RenderMarkdown(updateInfo.ReleaseNotes);

            // Set published date
            PublishedDateText.Text = $"Published: {updateInfo.PublishedAt:MMMM d, yyyy}";

            // Load auto-check setting
            AutoCheckBox.IsChecked = AppSettings.Instance.CheckForUpdatesOnStartup;

            // Wire up close button to mark as skip
            Closing += OnClosing;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // If closed via CloseButton ("Skip This Version"), mark it
            if (args.Result == ContentDialogResult.None)
            {
                SkipThisVersion = true;
            }

            // Save auto-check preference
            AppSettings.Instance.CheckForUpdatesOnStartup = AutoCheckBox.IsChecked == true;
            AppSettings.Instance.Save();
        }

        /// <summary>
        /// Gets the update info associated with this dialog.
        /// /// </summary>
        public UpdateInfo UpdateInfo => _updateInfo;

        /// <summary>
        /// Renders markdown content using Markdig.
        /// </summary>
        private void RenderMarkdown(string markdown)
        {
            ReleaseNotesContainer.Children.Clear();
            _currentTextBlock = null;

            if (string.IsNullOrWhiteSpace(markdown))
            {
                var textBlock = CreateTextBlock();
                var para = new Paragraph();
                para.Inlines.Add(new Run { Text = "No release notes available.", FontStyle = Windows.UI.Text.FontStyle.Italic });
                textBlock.Blocks.Add(para);
                FlushTextBlock();
                return;
            }

            try
            {
                // Parse markdown using Markdig
                var document = Markdown.Parse(markdown, _markdownPipeline);

                // Walk the AST and render
                foreach (var block in document)
                {
                    RenderBlock(block);
                }

                // Flush any remaining text content
                FlushTextBlock();
            }
            catch (Exception)
            {
                // Fallback: show raw text if parsing fails
                var textBlock = CreateTextBlock();
                var para = new Paragraph();
                para.Inlines.Add(new Run { Text = markdown });
                textBlock.Blocks.Add(para);
                FlushTextBlock();
            }
        }

        /// <summary>
        /// Creates or returns the current RichTextBlock for text content.
        /// </summary>
        private RichTextBlock CreateTextBlock()
        {
            if (_currentTextBlock == null)
            {
                _currentTextBlock = new RichTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                };
            }
            return _currentTextBlock;
        }

        /// <summary>
        /// Flushes the current text block to the container if it has content.
        /// </summary>
        private void FlushTextBlock()
        {
            if (_currentTextBlock != null && _currentTextBlock.Blocks.Count > 0)
            {
                ReleaseNotesContainer.Children.Add(_currentTextBlock);
                _currentTextBlock = null;
            }
        }

        /// <summary>
        /// Renders a Markdig block element.
        /// </summary>
        private void RenderBlock(MarkdigBlock block, int listIndent = 0)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    RenderHeading(heading);
                    break;

                case ParagraphBlock paragraph:
                    RenderParagraph(paragraph);
                    break;

                case ListBlock list:
                    RenderList(list, listIndent);
                    break;

                case ListItemBlock listItem:
                    RenderListItem(listItem, listIndent);
                    break;

                case FencedCodeBlock fencedCode:
                    RenderFencedCode(fencedCode);
                    break;

                case CodeBlock code:
                    RenderCodeBlock(code);
                    break;

                case Table table:
                    // Flush text before table, render table as Grid, then continue with new text block
                    FlushTextBlock();
                    RenderTableAsGrid(table);
                    break;

                case ThematicBreakBlock:
                    // Skip horizontal rules
                    break;

                case QuoteBlock quote:
                    RenderQuote(quote);
                    break;

                case HtmlBlock:
                    // Skip HTML blocks
                    break;

                default:
                    // For unknown blocks, try to render any inline content
                    if (block is LeafBlock leaf && leaf.Inline != null)
                    {
                        var textBlock = CreateTextBlock();
                        var para = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                        RenderInlines(para.Inlines, leaf.Inline);
                        if (para.Inlines.Count > 0)
                            textBlock.Blocks.Add(para);
                    }
                    break;
            }
        }

        private void RenderHeading(HeadingBlock heading)
        {
            var textBlock = CreateTextBlock();
            var para = new Paragraph
            {
                Margin = new Thickness(0, heading.Level == 1 ? 0 : 8, 0, 4)
            };

            var span = new Span();

            switch (heading.Level)
            {
                case 1:
                    span.FontSize = 18;
                    span.FontWeight = FontWeights.Bold;
                    break;
                case 2:
                    span.FontSize = 16;
                    span.FontWeight = FontWeights.SemiBold;
                    break;
                case 3:
                    span.FontSize = 14;
                    span.FontWeight = FontWeights.SemiBold;
                    break;
                default:
                    span.FontSize = 13;
                    span.FontWeight = FontWeights.SemiBold;
                    break;
            }

            if (heading.Inline != null)
                RenderInlines(span.Inlines, heading.Inline);

            para.Inlines.Add(span);
            textBlock.Blocks.Add(para);
        }

        private void RenderParagraph(ParagraphBlock paragraph)
        {
            var textBlock = CreateTextBlock();
            var para = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };

            if (paragraph.Inline != null)
                RenderInlines(para.Inlines, paragraph.Inline);

            if (para.Inlines.Count > 0)
                textBlock.Blocks.Add(para);
        }

        private void RenderList(ListBlock list, int indent)
        {
            int itemNumber = 1;
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    RenderListItem(listItem, indent, list.IsOrdered ? itemNumber++ : (int?)null);
                }
            }
        }

        private void RenderListItem(ListItemBlock listItem, int indent, int? number = null)
        {
            var textBlock = CreateTextBlock();
            var para = new Paragraph
            {
                Margin = new Thickness(12 + (indent * 16), 2, 0, 2),
                TextIndent = -12
            };

            string prefix = number.HasValue ? $"{number}. " : "• ";
            para.Inlines.Add(new Run { Text = prefix });

            bool firstBlock = true;
            foreach (var block in listItem)
            {
                if (firstBlock && block is ParagraphBlock firstPara)
                {
                    if (firstPara.Inline != null)
                        RenderInlines(para.Inlines, firstPara.Inline);
                    firstBlock = false;
                }
                else if (block is ListBlock nestedList)
                {
                    if (para.Inlines.Count > 1)
                        textBlock.Blocks.Add(para);
                    para = new Paragraph();
                    RenderList(nestedList, indent + 1);
                }
                else
                {
                    if (para.Inlines.Count > 1)
                    {
                        textBlock.Blocks.Add(para);
                        para = new Paragraph();
                    }
                    RenderBlock(block, indent);
                }
            }

            if (para.Inlines.Count > 1)
                textBlock.Blocks.Add(para);
        }

        private void RenderFencedCode(FencedCodeBlock fencedCode)
        {
            var textBlock = CreateTextBlock();
            var para = new Paragraph
            {
                Margin = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("Consolas")
            };

            var codeText = fencedCode.Lines.ToString();
            para.Inlines.Add(new Run
            {
                Text = codeText.TrimEnd(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            });

            textBlock.Blocks.Add(para);
        }

        private void RenderCodeBlock(CodeBlock code)
        {
            var textBlock = CreateTextBlock();
            var para = new Paragraph
            {
                Margin = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("Consolas")
            };

            var codeText = code.Lines.ToString();
            para.Inlines.Add(new Run
            {
                Text = codeText.TrimEnd(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            });

            textBlock.Blocks.Add(para);
        }

        /// <summary>
        /// Renders a Markdig table as a proper WinUI Grid control.
        /// </summary>
        private void RenderTableAsGrid(Table table)
        {
            // Parse table structure
            var rows = new List<List<string>>();
            bool hasHeader = false;
            int columnCount = 0;

            foreach (var row in table)
            {
                if (row is TableRow tableRow)
                {
                    if (tableRow.IsHeader)
                        hasHeader = true;

                    var cells = new List<string>();
                    foreach (var cell in tableRow)
                    {
                        if (cell is TableCell tableCell)
                        {
                            var cellText = GetCellText(tableCell);
                            cells.Add(cellText);
                        }
                    }
                    columnCount = Math.Max(columnCount, cells.Count);
                    rows.Add(cells);
                }
            }

            if (rows.Count == 0 || columnCount == 0)
                return;

            // Create the Grid
            var grid = new WinUIGrid
            {
                Margin = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };

            // Add column definitions
            for (int col = 0; col < columnCount; col++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Add row definitions
            for (int row = 0; row < rows.Count; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Add cells
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var rowData = rows[rowIndex];
                bool isHeader = hasHeader && rowIndex == 0;

                for (int colIndex = 0; colIndex < columnCount; colIndex++)
                {
                    var cellText = colIndex < rowData.Count ? rowData[colIndex] : "";

                    // Create cell border
                    var cellBorder = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
                        BorderThickness = new Thickness(
                            colIndex == 0 ? 0 : 1,  // Left
                            rowIndex == 0 ? 0 : 1,  // Top
                            0,                       // Right
                            0                        // Bottom
                        ),
                        Padding = new Thickness(8, 6, 8, 6),
                        Background = isHeader
                            ? (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
                            : new SolidColorBrush(Colors.Transparent)
                    };

                    // Create cell content
                    var textBlock = new TextBlock
                    {
                        Text = cellText,
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Check if cell contains a link and make it clickable
                    if (TryParseLink(cellText, out var linkText, out var linkUrl))
                    {
                        var hyperlinkButton = new HyperlinkButton
                        {
                            Content = linkText,
                            NavigateUri = new Uri(linkUrl),
                            Padding = new Thickness(0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        cellBorder.Child = hyperlinkButton;
                    }
                    else
                    {
                        cellBorder.Child = textBlock;
                    }

                    WinUIGrid.SetRow(cellBorder, rowIndex);
                    WinUIGrid.SetColumn(cellBorder, colIndex);
                    grid.Children.Add(cellBorder);
                }
            }

            ReleaseNotesContainer.Children.Add(grid);
        }

        /// <summary>
        /// Extracts plain text from a table cell.
        /// </summary>
        private string GetCellText(TableCell cell)
        {
            var parts = new List<string>();
            foreach (var block in cell)
            {
                if (block is ParagraphBlock para && para.Inline != null)
                {
                    parts.Add(GetInlineText(para.Inline));
                }
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Extracts plain text from inline content.
        /// </summary>
        private string GetInlineText(ContainerInline container)
        {
            var parts = new List<string>();
            foreach (var inline in container)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        parts.Add(literal.Content.ToString());
                        break;
                    case LinkInline link:
                        // For links, we want to preserve the markdown format for parsing
                        var linkText = link.FirstChild != null ? GetInlineText(link) : link.Url ?? "";
                        if (!string.IsNullOrEmpty(link.Url))
                            parts.Add($"[{linkText}]({link.Url})");
                        else
                            parts.Add(linkText);
                        break;
                    case EmphasisInline emphasis:
                        parts.Add(GetInlineText(emphasis));
                        break;
                    case CodeInline code:
                        parts.Add(code.Content);
                        break;
                    case ContainerInline cont:
                        parts.Add(GetInlineText(cont));
                        break;
                    default:
                        var text = inline.ToString();
                        if (!string.IsNullOrEmpty(text))
                            parts.Add(text);
                        break;
                }
            }
            return string.Join("", parts);
        }

        /// <summary>
        /// Tries to parse a markdown link from text.
        /// </summary>
        private bool TryParseLink(string text, out string linkText, out string linkUrl)
        {
            linkText = "";
            linkUrl = "";

            // Match [text](url) pattern
            var match = System.Text.RegularExpressions.Regex.Match(text, @"^\[([^\]]+)\]\(([^)]+)\)$");
            if (match.Success)
            {
                linkText = match.Groups[1].Value;
                linkUrl = match.Groups[2].Value;
                return Uri.TryCreate(linkUrl, UriKind.Absolute, out _);
            }

            return false;
        }

        private void RenderQuote(QuoteBlock quote)
        {
            var textBlock = CreateTextBlock();
            foreach (var block in quote)
            {
                if (block is ParagraphBlock para)
                {
                    var quotePara = new Paragraph
                    {
                        Margin = new Thickness(12, 2, 0, 2),
                        Foreground = new SolidColorBrush(Colors.Gray)
                    };

                    quotePara.Inlines.Add(new Run { Text = "? ", Foreground = new SolidColorBrush(Colors.DarkGray) });

                    if (para.Inline != null)
                        RenderInlines(quotePara.Inlines, para.Inline);

                    textBlock.Blocks.Add(quotePara);
                }
                else
                {
                    RenderBlock(block);
                }
            }
        }

        /// <summary>
        /// Renders Markdig inline elements to a WinUI InlineCollection.
        /// </summary>
        private void RenderInlines(InlineCollection inlines, ContainerInline container)
        {
            foreach (var inline in container)
            {
                RenderInline(inlines, inline);
            }
        }

        private void RenderInline(InlineCollection inlines, MarkdigInline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    inlines.Add(new Run { Text = literal.Content.ToString() });
                    break;

                case EmphasisInline emphasis:
                    var emphSpan = new Span();
                    if (emphasis.DelimiterCount == 2)
                        emphSpan.FontWeight = FontWeights.Bold;
                    else
                        emphSpan.FontStyle = Windows.UI.Text.FontStyle.Italic;

                    RenderInlines(emphSpan.Inlines, emphasis);
                    inlines.Add(emphSpan);
                    break;

                case CodeInline code:
                    inlines.Add(new Run
                    {
                        Text = code.Content,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(Colors.DarkOrange)
                    });
                    break;

                case LinkInline link:
                    var hyperlink = new Hyperlink();
                    if (link.Url != null && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                    {
                        hyperlink.NavigateUri = uri;
                    }

                    if (link.FirstChild != null)
                    {
                        foreach (var child in link)
                        {
                            RenderInline(hyperlink.Inlines, child);
                        }
                    }
                    else if (!string.IsNullOrEmpty(link.Url))
                    {
                        hyperlink.Inlines.Add(new Run { Text = link.Url });
                    }

                    inlines.Add(hyperlink);
                    break;

                case AutolinkInline autolink:
                    var autolinkHyperlink = new Hyperlink();
                    if (Uri.TryCreate(autolink.Url, UriKind.Absolute, out var autolinkUri))
                    {
                        autolinkHyperlink.NavigateUri = autolinkUri;
                    }
                    autolinkHyperlink.Inlines.Add(new Run { Text = autolink.Url });
                    inlines.Add(autolinkHyperlink);
                    break;

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                        inlines.Add(new LineBreak());
                    else
                        inlines.Add(new Run { Text = " " });
                    break;

                case HtmlInline:
                    break;

                case ContainerInline container:
                    RenderInlines(inlines, container);
                    break;

                default:
                    var text = inline.ToString();
                    if (!string.IsNullOrEmpty(text))
                        inlines.Add(new Run { Text = text });
                    break;
            }
        }
    }
}
