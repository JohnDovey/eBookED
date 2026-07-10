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

        /* Styles selectable from the editor's right-click "Apply Style" menu — adapted from
           a Vellum-generated reference EPUB's stylesheet (see EditorStyleCatalog). Applied
           via a Markdown custom container, e.g. "::: {.smallcaps} ... :::". */
        .smallcaps {
            font-variant: small-caps;
            letter-spacing: 0.04em;
        }

        .underline {
            text-decoration: underline;
        }

        .strikethrough {
            text-decoration: line-through;
        }

        .monospace {
            font-family: Courier, monospace;
        }

        .sans-serif {
            font-family: "Helvetica Neue", Helvetica, Arial, sans-serif;
        }

        .all-caps {
            text-transform: uppercase;
            letter-spacing: 0.04em;
        }

        .verse {
            padding-left: 3em;
            padding-right: 3em;
            font-style: italic;
        }

        .inset {
            margin-left: 1.5em;
            margin-right: 1.5em;
        }

        .attribution {
            text-align: right;
            font-size: 90%;
            letter-spacing: 0.05em;
            margin-top: 0.5em;
        }

        .attribution::before {
            content: "— ";
        }

        .drop-cap:first-letter {
            float: left;
            font-size: 3.2em;
            line-height: 0.8;
            padding-right: 0.08em;
            padding-top: 0.05em;
        }

        .caption {
            font-size: 50%;
            font-style: italic;
        }
        """;
}
