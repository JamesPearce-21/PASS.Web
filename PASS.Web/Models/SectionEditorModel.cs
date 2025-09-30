namespace PASS.Web.Models
{
    public class SectionEditorModel
    {
        public string Title { get; set; }
        public string SectionKey { get; set; }
        public int ParagraphCount { get; set; }
        public SectionWrapper SectionWrapper { get; set; }
    }

    public class SectionWrapper
    {
        public Section[] Sections { get; set; } = new Section[0];
    }

    public class Section
    {
        public TextContentItem[] TextContent { get; set; } = new TextContentItem[0];
        public ImageContentItem[] ImageContent { get; set; } = new ImageContentItem[0];
    }

    public class TextContentItem
    {
        public string Heading { get; set; }
        public string Paragraph { get; set; }
    }

    public class ImageContentItem
    {
        public string Src { get; set; }
        public string Alt { get; set; }
    }
}
