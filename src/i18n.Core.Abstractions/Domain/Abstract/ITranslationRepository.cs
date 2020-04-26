using System.Collections.Generic;
using i18n.Core.Abstractions.Domain.Entities;

namespace i18n.Core.Abstractions.Domain.Abstract
{
    /// <summary>
    /// For managing a translation repository for reading, writing and searching. As long as you implement this you can store your translation wherever you want. Db/ po files/xml
    /// </summary>
    public interface ITranslationRepository
    {
        /// <summary>
        /// Retrieves a translation with all items (both with translation set and not)
        /// </summary>
        /// <param name="langtag">The language tag to get the translation for. For instance "sv-SE"</param>
        /// <param name="fileNames">A list of file names generated.</param>
        /// <param name="loadingCache">Flag determining whether the call came from the generator or the localizing module.</param>
        /// <returns>A Translation object with the Language->LanguageShortTag set and all the translation items returned in a Dictionary</returns>
        Translation GetTranslation(string langtag, List<string> fileNames = null, bool loadingCache = true);

        /// <summary>
        /// Gets all available languages. There is a setting for available languages that can be used by the implementation. But the implementation can if prefered use other method.
        /// </summary>
        /// <returns>List of <see cref="Language"/> with a minimum of LanguageShortTag set</returns>
        IEnumerable<Language> GetAvailableLanguages();

        /// <summary>
        /// Saves a translation (persisting it). How this is done is completely up to the implementation. As long as the same language can be loaded with <see cref="GetTranslation"/>
        /// </summary>
        /// <param name="translation">The translation to save. At minimum the Items and Language->LanguageShortTag must be set</param>
        void SaveTranslation(Translation translation);
    }
}
