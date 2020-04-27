using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace i18n.Core.PortableObject
{
    /// <summary>
    /// Represents a provider that provides a translations for .po files.
    /// </summary>
    public class PortableObjectFilesTranslationsProvider : ITranslationProvider
    {
        readonly ILocalizationFileLocationProvider _poFilesLocationProvider;
        [MaybeNull] readonly ILogger<PortableObjectFilesTranslationsProvider> _logger;
        readonly PortableObjectParser _parser;

        /// <summary>
        /// Creates a new instance of <see cref="PortableObjectFilesTranslationsProvider"/>.
        /// </summary>
        /// <param name="poFileLocationProvider">The <see cref="ILocalizationFileLocationProvider"/>.</param>
        /// <param name="logger"></param>
        public PortableObjectFilesTranslationsProvider(ILocalizationFileLocationProvider poFileLocationProvider, ILogger<PortableObjectFilesTranslationsProvider> logger = null)
        {
            _poFilesLocationProvider = poFileLocationProvider;
            _logger = logger;
            _parser = new PortableObjectParser();
        }

        /// <inheritdocs />
        public void LoadTranslations([JetBrains.Annotations.NotNull] CultureInfo cultureInfo, [JetBrains.Annotations.NotNull] CultureDictionary dictionary)
        {
            if (cultureInfo == null) throw new ArgumentNullException(nameof(cultureInfo));
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            var fileInfos = new List<IFileInfo>();
            var cultureName = cultureInfo.Name;
            fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureName));

            foreach (var fileInfo in fileInfos.Where(fileInfo => !fileInfo.IsDirectory))
            {
                if (fileInfo.Exists)
                {
                    using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    dictionary.MergeTranslations(_parser.Parse(reader));

                    _logger?.LogDebug($"Translations for culture found: {cultureName}. Translations available: {dictionary.Translations.Count}. Path: {fileInfo.PhysicalPath}.");
                    break;
                }

                _logger?.LogWarning($"Translation for culture was not found: {cultureName}. Path: {fileInfo.PhysicalPath}.");
            }
        }

    }
}
