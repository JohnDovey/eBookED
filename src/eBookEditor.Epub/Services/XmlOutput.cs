using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace eBookEditor.Epub.Services;

internal static class XmlOutput
{
    public static string ToXmlString(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };

        using var stringWriter = new Utf8StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
            document.Save(xmlWriter);

        return stringWriter.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
