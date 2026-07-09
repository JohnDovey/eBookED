namespace eBookEditor.Core.Models;

public record BookMetadata
{
    public const string DefaultDisclaimerText =
        "All rights reserved. No part of this publication may be reproduced, distributed, or " +
        "transmitted in any form or by any means, including photocopying, recording, or other " +
        "electronic or mechanical methods, without the prior written permission of the publisher, " +
        "except in the case of brief quotations embodied in critical reviews and certain other " +
        "noncommercial uses permitted by copyright law. This is a work of fiction. Names, " +
        "characters, businesses, places, events, and incidents are either the products of the " +
        "author's imagination or used in a fictitious manner.";

    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public List<Contributor> Contributors { get; init; } = new();
    public string CopyrightHolder { get; init; } = "";
    public int? CopyrightYear { get; init; }
    public PublisherInfo? Publisher { get; init; }
    public string? CoverImagePath { get; init; }
    public DateOnly? PublicationDate { get; init; }
    public string Language { get; init; } = "en";
    public List<string> GenreTags { get; init; } = new();
    public List<string> FreeTags { get; init; } = new();
    public string? Blurb { get; init; }
    public string? Isbn10 { get; init; }
    public string? Isbn13 { get; init; }
    public AboutAuthorInfo? AboutAuthor { get; init; }
    public List<StoreLink> StoreLinks { get; init; } = new();
    public string CopyrightDisclaimer { get; init; } = DefaultDisclaimerText;
    public Guid Identifier { get; init; } = Guid.NewGuid();

    public IEnumerable<Contributor> Authors => Contributors.Where(c => c.Role == ContributorRole.Author);
    public IEnumerable<Contributor> Editors => Contributors.Where(c => c.Role == ContributorRole.Editor);
    public IEnumerable<Contributor> Illustrators => Contributors.Where(c => c.Role == ContributorRole.Illustrator);
}
