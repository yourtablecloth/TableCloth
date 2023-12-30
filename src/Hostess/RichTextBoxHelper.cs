using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Hostess
{
    // https://stackoverflow.com/a/2641774
    public sealed class RichTextBoxHelper : DependencyObject
    {
        static RichTextBoxHelper()
        {
            TargetEncoding = Encoding.UTF8;

            var metadata = new FrameworkPropertyMetadata
            {
                BindsTwoWayByDefault = true,
                PropertyChangedCallback = (_d, _e) =>
                {
                    var richTextBox = (RichTextBox)_d;

                    // Parse the XAML to a document (or use XamlReader.Parse())
                    var doc = new FlowDocument();
                    var xaml = GetDocumentXaml(richTextBox);

                    var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    var memoryStream = new MemoryStream(TargetEncoding.GetBytes(xaml));
                    range.Load(memoryStream, DataFormats.Xaml);

                    // Set the document
                    richTextBox.Document = doc;

                    // When the document changes update the source
                    range.Changed += (__sender, __e) =>
                    {
                        if (richTextBox.Document == doc)
                        {
                            var buffer = new MemoryStream();
                            range.Save(buffer, DataFormats.Xaml);
                            SetDocumentXaml(richTextBox, TargetEncoding.GetString(buffer.ToArray()));
                        }
                    };
                }
            };

            DocumentXamlProperty = DependencyProperty.RegisterAttached(
                DocumentXamlPropertyName, typeof(string),
                typeof(RichTextBoxHelper), metadata);
        }

        public static readonly string DocumentXamlPropertyName = "DocumentXaml";

        public static string GetDocumentXaml(DependencyObject obj)
            => (string)obj.GetValue(DocumentXamlProperty);

        public static void SetDocumentXaml(DependencyObject obj, string value)
            => obj.SetValue(DocumentXamlProperty, value);

        public static readonly DependencyProperty DocumentXamlProperty;

        private static readonly Encoding TargetEncoding;

        private static void TextRange_Changed(object __sender, EventArgs __e)
        {

        }
    }
}
