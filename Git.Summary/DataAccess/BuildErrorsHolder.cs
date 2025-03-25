using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Universe;

namespace Git.Summary.DataAccess;

public static class BuildErrorsHolder
{
    private static readonly object SyncErrors = new object();
    public static void Try(List<string> errors, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            var exceptionDigest = ex.GetExceptionDigest();
            lock (SyncErrors)
                errors.Add(exceptionDigest);
        }
    }
    public static void TryTitled(List<string> errors, string title, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            var exception = new InvalidOperationException($"{title} failed", ex);
            var exceptionDigest = exception.GetExceptionDigest();
            lock(SyncErrors)
                errors.Add(exceptionDigest);
        }
    }
}