using System.Collections.Concurrent;

namespace i18n.Core.Pot.Entities
{
    /// <summary>
    /// Holds a complete translation in any one language. The template (TemplateItems) will have told the language which id's/strings that needs translation.
    /// </summary>
    internal sealed class Translation
    {
        public Language LanguageInformation { get; set; }
        public ConcurrentDictionary<string, TranslationItem> Items  { get; set; }
    }
}
