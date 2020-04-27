using System;
using System.Threading;

namespace i18n.Core.Helpers
{
    internal static class I18NUtility
    {
        public static void Retry(this Action block, int retries = 2, int delayInMilliseconds = 250, bool throwException = true)
        {
            Func<object> thunk = () => {
                block();
                return null;
            };

            thunk.Retry(retries, delayInMilliseconds, throwException);
        }

        public static T Retry<T>(this Func<T> block, int retries = 2, int delayInMilliseconds = 250, bool throwException = true)
        {
            while (true)
            {
                try
                {
                    var ret = block();
                    return ret;
                }
                catch (Exception)
                {
                    if (retries == 0)
                    {
                        if (throwException)
                        {
                            throw;
                        }

                        return default;
                    }

                    retries--;
                    if (delayInMilliseconds > 0)
                    {
                        Thread.Sleep(delayInMilliseconds);
                    }
                }
            }
        }
    }
}