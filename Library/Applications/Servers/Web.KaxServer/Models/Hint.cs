using System.Xml.Linq;

namespace Web.KaxServer.Models
{
    public class Hint
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "Info"; // Info（蓝色）、Message（白色）、Warning（黄色）
        public string Content { get; set; } = string.Empty;
        
        public XElement ToXml()
        {
            var element = new XElement("Hint",
                new XAttribute("Id", Id),
                new XAttribute("Type", Type),
                new XElement("Content", Content)
            );
            
            return element;
        }
        
        public static Hint FromXml(XElement element)
        {
            if (element == null) return null;
            
            var hint = new Hint
            {
                Id = element.Attribute("Id")?.Value ?? string.Empty,
                Type = element.Attribute("Type")?.Value ?? "Info",
                Content = element.Element("Content")?.Value ?? string.Empty
            };
            
            return hint;
        }
    }
} 