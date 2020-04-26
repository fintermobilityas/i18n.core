using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace i18n.Core.Abstractions.Domain.Helpers
{
    internal sealed class FileEnumerator
    {
        readonly IEnumerable<string> _blackList;

        public FileEnumerator(IReadOnlyCollection<string> blackList)
        {
            _blackList = blackList;
            foreach (var str in blackList)
            {
                DebugHelpers.WriteLine(str);
            }
        }

        public IEnumerable<string> GetFiles(string path)
        {
            var queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                try
                {
                    foreach (var path1 in Directory.EnumerateDirectories(path))
                    {
                        if (!IsBlackListed(path1))
                        {
                            queue.Enqueue(path1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelpers.WriteLine(ex.ToString());
                }
                
                IEnumerable<string> files = null;
                try
                {
                    files = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    DebugHelpers.WriteLine(ex.ToString());
                }

                if (files == null)
                {
                    continue;
                }

                foreach (var path1 in files)
                {
                    if (!IsBlackListed(path1))
                    {
                        yield return path1;
                    }
                }
            }
        }

        bool IsBlackListed(string path)
        {
            return _blackList.Any(x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase));
        }
    }
}
