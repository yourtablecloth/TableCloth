using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Hostess
{
    // https://stackoverflow.com/a/2641774
    public sealed class RichTextBoxHelper : DependencyObject
    {
        static RichTextBoxHelper()
        {
            TargetEncoding = new UTF8Encoding(false);

            var metadata = new FrameworkPropertyMetadata
            {
                BindsTwoWayByDefault = false,
                PropertyChangedCallback = (_d, _e) =>
                {
                    var richTextBox = (RichTextBox)_d;

                    // Parse the XAML to a document (or use XamlReader.Parse())
                    var doc = new FlowDocument();
                    var xaml = GetDocumentXaml(richTextBox);

                    var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    var memoryStream = new MemoryStream(TargetEncoding.GetBytes(xaml));
                    range.Load(memoryStream, DataFormats.Text);

                    // Set the document
                    richTextBox.Document = doc;

                    // https://www.codeproject.com/Questions/226402/wpf-richtext-box-detect-hyperlink
                    var pointer = doc.ContentStart;

                    while (pointer != null)
                    {
                        if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                        {
                            var textRun = pointer.GetTextInRun(LogicalDirection.Forward);
                            var matches = Regex.Matches(textRun, @"((https://|http://|ftp://|mailto:)[^\s]+)");

                            foreach (Match match in matches)
                            {
                                var start = pointer.GetPositionAtOffset(match.Index);
                                var end = start.GetPositionAtOffset(match.Length);
                                var hyperlink = new Hyperlink(start, end);
                                hyperlink.NavigateUri = new Uri(match.Value);
                                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                            }
                        }

                        pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    }

                    // When the document changes update the source
                    range.Changed += (__sender, __e) =>
                    {
                        if (richTextBox.Document == doc)
                        {
                            var buffer = new MemoryStream();
                            range.Save(buffer, DataFormats.Text);
                            SetDocumentXaml(richTextBox, TargetEncoding.GetString(buffer.ToArray()));
                        }
                    };
                }
            };

            DocumentXamlProperty = DependencyProperty.RegisterAttached(
                DocumentXamlPropertyName, typeof(string),
                typeof(RichTextBoxHelper), metadata);
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true, });
            e.Handled = true;
        }

        public static readonly string DocumentXamlPropertyName = "DocumentXaml";

        public static string GetDocumentXaml(DependencyObject obj)
            => (string)obj.GetValue(DocumentXamlProperty);

        public static void SetDocumentXaml(DependencyObject obj, string value)
            => obj.SetValue(DocumentXamlProperty, value);

        public static readonly DependencyProperty DocumentXamlProperty;

        private static readonly Encoding TargetEncoding;
    }
}
