using System.IO;
using i18n.Core.Abstractions;
using Microsoft.Extensions.FileProviders;

namespace i18n.Core.PortableObject
{
    /// <summary>
    /// Represents a provider that provides a translations for .po files.
    /// </summary>
    public class PoFilesTranslationsProvider : ITranslationProvider
    {
        readonly ILocalizationFileLocationProvider _poFilesLocationProvider;
        readonly PoParser _parser;

        /// <summary>
        /// Creates a new instance of <see cref="PoFilesTranslationsProvider"/>.
        /// </summary>
        /// <param name="poFileLocationProvider">The <see cref="ILocalizationFileLocationProvider"/>.</param>
        public PoFilesTranslationsProvider(ILocalizationFileLocationProvider poFileLocationProvider)
        {
            _poFilesLocationProvider = poFileLocationProvider;
            _parser = new PoParser();
        }

        /// <inheritdocs />
        public void LoadTranslations(string cultureName, CultureDictionary dictionary)
        {
            foreach (var fileInfo in _poFilesLocationProvider.GetLocations(cultureName))
            {
                LoadFileToDictionary(fileInfo, dictionary);
            }
        }

        void LoadFileToDictionary(IFileInfo fileInfo, CultureDictionary dictionary)
        {
            if (fileInfo.Exists && !fileInfo.IsDirectory)
            {
                using var stream = fileInfo.CreateReadStream();
                using var reader = new StreamReader(stream);
                dictionary.MergeTranslations(_parser.Parse(reader));
            }
        }
    }
}
