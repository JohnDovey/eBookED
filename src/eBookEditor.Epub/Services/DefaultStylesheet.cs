namespace eBookEditor.Epub.Services;

internal static class DefaultStylesheet
{
    public const string Css = """
        body {
            font-family: serif;
            line-height: 1.5;
            margin: 1em;
        }

        h1, h2, h3 {
            font-family: sans-serif;
            line-height: 1.2;
        }

        img {
            max-width: 100%;
        }

        table {
            border-collapse: collapse;
            width: 100%;
        }

        th, td {
            border: 1px solid #888;
            padding: 0.4em;
        }
        """;
}
