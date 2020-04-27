using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using i18n.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace i18n.Core
{
    /// <summary>
    /// provides a localization files from the content root folder.
    /// </summary>
    public class ContentRootPoFileLocationProvider : ILocalizationFileLocationProvider
    {
        readonly string _contentRootPath;
        readonly CultureInfo _defaultCulture;

        /// <summary>
        /// Creates a new instance of <see cref="ContentRootPoFileLocationProvider"/>.
        /// </summary>
        /// <param name="hostEnvironment"><see cref="IHostEnvironment"/>.</param>
        /// <param name="requestLocalizationOptions">The IOptions<RequestLocalizationOptions>.</param>
        public ContentRootPoFileLocationProvider(IHostEnvironment hostEnvironment, IOptions<RequestLocalizationOptions> requestLocalizationOptions)
        {
            _contentRootPath = hostEnvironment.ContentRootPath;
            _defaultCulture = requestLocalizationOptions.Value.DefaultRequestCulture.Culture;
        }

        /// <inheritdocs />
        public IEnumerable<IFileInfo> GetLocations(string cultureName)
        {
            if (string.Equals(cultureName, _defaultCulture.Name, StringComparison.Ordinal))
            {
                yield return new PhysicalFileInfo(new FileInfo(Path.Combine(_contentRootPath, "locale", "messages.pot")));
                yield break;
            }

            yield return new PhysicalFileInfo(new FileInfo(Path.Combine(_contentRootPath, "locale", cultureName, "messages.po")));
        }
    }
}
