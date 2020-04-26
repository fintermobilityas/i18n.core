using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace i18n.Core.Abstractions.Domain
{
    public interface ISettingsProvider
    {
        string ProjectDirectory { get; }
        string GetSetting(string key);
        void SetSetting(string key, string value);
    }

    public class SettingsProvider : ISettingsProvider
    {
        readonly object _syncRoot = new object();
        readonly Dictionary<string, string> _settings;

        public string ProjectDirectory { get; }

        public SettingsProvider(string webConfigFilename)
        {
            ProjectDirectory = Path.GetDirectoryName(webConfigFilename);
            _settings = !File.Exists(webConfigFilename) ? new Dictionary<string, string>() : Parse(webConfigFilename);
        }

        static Dictionary<string, string> Parse(string webConfigFilename)
        {
            if (webConfigFilename == null)
            {
                throw new ArgumentNullException(nameof(webConfigFilename));
            }

            var stream = File.OpenRead(webConfigFilename);
            using var xmlReader = new XmlTextReader(stream);
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(xmlReader);

            var appSettingsDict = new Dictionary<string, string>();
            foreach (XmlNode node in xmlDocument.SelectSingleNode("/configuration/appSettings").ChildNodes)
            {
                if (node.Name != "add")
                {
                    continue;
                }

                var key = node.Attributes["key"].Value;
                if (key == null || !key.StartsWith("i18n."))
                {
                    continue;
                }

                var value = node.Attributes["value"].Value;
                appSettingsDict.Add(key, value);
            }

            return appSettingsDict;
        }

        public string GetSetting(string key)
        {
            lock (_syncRoot)
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    return value;
                }

                return null;
            }
        }

        public void SetSetting(string key, string value)
        {
            lock (_syncRoot)
            {
                _settings[key] = value;
            }
        }
    }

}
