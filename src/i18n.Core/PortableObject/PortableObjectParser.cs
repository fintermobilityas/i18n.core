using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using i18n.Core.Abstractions;

namespace i18n.Core.PortableObject
{
    /// <summary>
    /// Represents a parser for portable objects.
    /// </summary>
    public class PortableObjectParser
    {
        static readonly Dictionary<char, char> EscapeTranslations = new Dictionary<char, char> {
            { 'n', '\n' },
            { 'r', '\r' },
            { 't', '\t' }
        };

        /// <summary>
        /// Parses a .po file.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/>.</param>
        /// <returns>A list of culture records.</returns>
        public IEnumerable<CultureDictionaryRecord> Parse(TextReader reader)
        {
            var entryBuilder = new DictionaryRecordBuilder();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var (context, content) = ParseLine(line);

                if (context == PoContext.Other)
                {
                    continue;
                }

                // msgid or msgctxt are first lines of the entry. If builder contains valid entry return it and start building a new one.
                if ((context == PoContext.MessageId 
                     || context == PoContext.MessageContext) && entryBuilder.ShouldFlushRecord)
                {
                    yield return entryBuilder.BuildRecordAndReset();
                }

                entryBuilder.Set(context, content);
            }

            if (entryBuilder.ShouldFlushRecord)
            {
                yield return entryBuilder.BuildRecordAndReset();
            }
        }

        static string Unescape(string str)
        {
            StringBuilder sb = null;
            var escaped = false;
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                if (escaped)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder(str.Length);
                        if (i > 1)
                        {
                            sb.Append(str.Substring(0, i - 1));
                        }
                    }

                    sb.Append(EscapeTranslations.TryGetValue(c, out var unescaped) ? unescaped : c);

                    escaped = false;
                }
                else
                {
                    if (c == '\\')
                    {
                        escaped = true;
                    }
                    else
                    {
                        sb?.Append(c);
                    }
                }
            }
            return sb == null ? str : sb.ToString();
        }

        static string TrimQuote(string str)
        {
            if (str.StartsWith('\"') && str.EndsWith('\"'))
            {
                return str.Length == 1 ? string.Empty : str.Substring(1, str.Length - 2);
            }

            return str;
        }

        static (PoContext context, string content) ParseLine(string line)
        {
            if (line.StartsWith('\"'))
            {
                return (PoContext.Text, Unescape(TrimQuote(line.Trim())));
            }

            var keyAndValue = line.Split(null, 2);
            if (keyAndValue.Length != 2)
            {
                return (PoContext.Other, string.Empty);
            }

            var content = Unescape(TrimQuote(keyAndValue[1].Trim()));
            return keyAndValue[0] switch
            {
                "msgctxt" => (PoContext.MessageContext, content),
                "msgid" => (PoContext.MessageId, content),
                var key when key.StartsWith("msgstr", StringComparison.Ordinal) => (PoContext.Translation, content),
                _ => (PoContext.Other, content)
            };
        }

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        sealed class DictionaryRecordBuilder
        {
            readonly List<string> _values;
            IEnumerable<string> ValidValues => _values.Where(value => !string.IsNullOrEmpty(value));
            PoContext _context;

            public string MessageId { get; private set; }
            public string MessageContext { get; private set; }

            public IEnumerable<string> Values => _values;

            public bool IsValid => !string.IsNullOrEmpty(MessageId) && ValidValues.Any();
            public bool ShouldFlushRecord => IsValid && _context == PoContext.Translation;

            public DictionaryRecordBuilder()
            {
                _values = new List<string>();
            }

            public void Set(PoContext context, string text)
            {
                switch (context)
                {
                    case PoContext.MessageId:
                        {
                            // If the MessageId has been set to an empty string and now gets set again
                            // before flushing the values should be reset.
                            if (string.IsNullOrEmpty(MessageId))
                            {
                                _values.Clear();
                            }

                            MessageId = text;
                            break;
                        }
                    case PoContext.MessageContext: MessageContext = text; break;
                    case PoContext.Translation: _values.Add(text); break;
                    case PoContext.Text: AppendText(text); return; // we don't want to set context to Text
                }

                _context = context;
            }

            void AppendText(string text)
            {
                switch (_context)
                {
                    case PoContext.MessageId: MessageId += text; break;
                    case PoContext.MessageContext: MessageContext += text; break;
                    case PoContext.Translation:
                        if (_values.Count > 0)
                        {
                            _values[^1] += text;
                        }
                        break;
                }
            }

            public CultureDictionaryRecord BuildRecordAndReset()
            {
                if (!IsValid)
                {
                    return null;
                }

                var result = new CultureDictionaryRecord(MessageId, MessageContext, ValidValues.ToArray());

                MessageId = null;
                MessageContext = null;
                _values.Clear();

                return result;
            }
        }

        enum PoContext
        {
            MessageId,
            MessageContext,
            Translation,
            Text,
            Other
        }
    }
}
