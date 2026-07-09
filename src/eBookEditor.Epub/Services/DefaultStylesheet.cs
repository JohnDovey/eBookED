namespace eBookEditor.Epub.Services;

public static class DefaultStylesheet
{
    public const string Css = """
        body {
            font-family: serif;
            line-height: 1.5;
            margin: 1em;
            page-break-before: always;
            break-before: page;
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

        .footnote-ref {
            text-decoration: none;
        }

        .footnotes {
            margin-top: 2em;
            font-size: 0.9em;
        }

        .footnotes hr {
            border: none;
            border-top: 1px solid #888;
            margin-bottom: 1em;
        }

        .footnote-back-ref {
            text-decoration: none;
        }
        """;
}
