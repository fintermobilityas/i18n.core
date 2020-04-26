using System.Collections.Generic;
using System.IO;
using i18n.Core.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace i18n.Core
{
    /// <summary>
    /// provides a localization files from the content root folder.
    /// </summary>
    public class ContentRootPoFileLocationProvider : ILocalizationFileLocationProvider
    {
        readonly IFileProvider _fileProvider;
        readonly string _resourcesContainer;

        /// <summary>
        /// Creates a new instance of <see cref="ContentRootPoFileLocationProvider"/>.
        /// </summary>
        /// <param name="hostingEnvironment"><see cref="IHostEnvironment"/>.</param>
        /// <param name="localizationOptions">The IOptions<LocalizationOptions>.</param>
        public ContentRootPoFileLocationProvider(IHostEnvironment hostingEnvironment, IOptions<LocalizationOptions> localizationOptions)
        {
            _fileProvider = hostingEnvironment.ContentRootFileProvider;
            _resourcesContainer = localizationOptions.Value.ResourcesPath;
        }

        /// <inheritdocs />
        public IEnumerable<IFileInfo> GetLocations(string cultureName)
        {
            yield return _fileProvider.GetFileInfo(Path.Combine(_resourcesContainer, cultureName + ".po"));
        }
    }
}
