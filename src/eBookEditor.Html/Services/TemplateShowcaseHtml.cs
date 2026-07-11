namespace eBookEditor.Html.Services;

/// <summary>
/// Builds a self-contained HTML body fragment exercising every selector this app's built-in
/// templates target (see "Vellum Serif.css") — headings, paragraphs, blockquotes, lists, a
/// table, an image caption, a footnote, and one worked example of every EditorStyleCatalog
/// style class — so the "Preview" button on the template picker (StyleWindow) can show what a
/// candidate stylesheet actually looks like against real markup shapes before it's saved. All
/// text here is original placeholder prose invented for this purpose, not drawn from any book,
/// article, or other external source.
/// </summary>
public static class TemplateShowcaseHtml
{
    public static string Build() => """
        <h1>Chapter One: The Showcase</h1>
        <h2>A Subheading</h2>
        <p>This is an ordinary paragraph of body text, set here so you can judge the template's base font, size, line height, and paragraph justification. A second sentence follows to give the line-wrapping something real to do, and a third one rounds out the paragraph so its shape reads naturally on the page.</p>
        <p class="drop-cap">The opening paragraph of a chapter often gets special treatment. This one demonstrates the Drop Cap style, which enlarges and styles the very first letter while the rest of the paragraph continues normally beneath it, exactly as it would at the start of a real chapter.</p>
        <p>Inline styles are easiest to judge sitting inside an ordinary sentence. Here is a word in <span class="smallcaps">small caps</span>, a phrase that is <span class="underline">underlined</span>, some <span class="strikethrough">struck-through</span> text, a bit of <span class="monospace">monospace code</span>, a run set in <span class="sans-serif">sans-serif</span>, and finally <span class="all-caps">shouted text in all caps</span>, all inside one paragraph so their weights and spacing can be compared directly.</p>
        <blockquote>A blockquote sits apart from the surrounding prose, often used for a letter, an inscription, or a quoted passage within the story.</blockquote>
        <div class="verse">
        <p>Roses in the garden fade by morning light,</p>
        <p>Yet still the old stone wall stands firm and white.</p>
        <p>Seasons turn and turn again, forgetting nothing lost,</p>
        <p>While quiet hands rebuild the fence, undaunted by the frost.</p>
        </div>
        <div class="inset">
        <p>An inset block is indented from both margins — useful for a letter, a journal entry, or any passage that should read as visually separate from the main narrative without the formality of a blockquote.</p>
        </div>
        <div class="attribution">Old Proverb</div>
        <h3>Lists</h3>
        <ul>
        <li>An unordered list item</li>
        <li>A second unordered list item</li>
        <li>A third, just to see the spacing between three</li>
        </ul>
        <ol>
        <li>An ordered list item</li>
        <li>A second ordered list item</li>
        </ol>
        <h3>A Table</h3>
        <table>
        <thead>
        <tr><th>Name</th><th>Role</th><th>Notes</th></tr>
        </thead>
        <tbody>
        <tr><td>Aria Fenwick</td><td>Protagonist</td><td>Sample row one</td></tr>
        <tr><td>Corin Vale</td><td>Rival</td><td>Sample row two</td></tr>
        </tbody>
        </table>
        <h3>An Image with a Caption</h3>
        <figure>
        <img src="showcase-placeholder.svg" alt="A placeholder illustration">
        <div class="caption">A caption sits beneath an image, usually set smaller and italic.</div>
        </figure>
        <h3>A Footnote</h3>
        <p>Here is a sentence with a footnote reference attached to the end of it.<sup><a class="footnote-ref" href="#fn1" id="fnref1">1</a></sup></p>
        <div class="footnotes">
        <hr>
        <ol>
        <li id="fn1">This is the footnote's own text, shown at the bottom of the page or chapter. <a class="footnote-back-ref" href="#fnref1">↩</a></li>
        </ol>
        </div>
        <h4>An Even Smaller Heading</h4>
        <p>Headings render down to h4 in this app's chapter content, so this last one shows how far the heading hierarchy scales before falling back to plain paragraph text.</p>
        <hr>
        <p>A horizontal rule, like the one just above, is commonly used as a scene break within a chapter.</p>
        """;
}
