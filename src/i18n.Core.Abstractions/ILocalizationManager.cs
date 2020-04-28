using System.Globalization;

namespace i18n.Core.Abstractions
{
    /// <summary>
    /// Contract to manage the localization.
    /// </summary>
    public interface ILocalizationManager
    {
        /// <summary>
        /// Disables cache. This is usefull during development.
        /// </summary>
        bool DisableCache { get; set; }
        /// <summary>
        /// Retrieves a dictionary for a specified culture.
        /// </summary>
        /// <param name="culture">The <see cref="CultureInfo"/>.</param>
        /// <returns>A <see cref="CultureDictionary"/> for the specified culture.</returns>
        CultureDictionary GetDictionary(CultureInfo culture);
        /// <summary>
        /// Translates text to a given culture.
        /// </summary>
        /// <param name="culture"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        string Translate(CultureInfo culture, string text);
    }
}
