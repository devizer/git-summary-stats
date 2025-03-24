﻿using System;
using System.Collections.Generic;

namespace Universe;

public static class ExceptionExtensions
{
    public static string GetExceptionDigest(this Exception exception)
    {
        List<string> ret = new List<string>();
        // while (ex != null)
        foreach (var ex in AsPlainExceptionList(exception))
        {
            ret.Add("[" + ex.GetType().Name + "] " + ex.Message);
        }

        return string.Join(" → ", ret.ToArray());
    }


    public static IEnumerable<Exception> AsPlainExceptionList(this Exception ex)
    {
        while (ex != null)
        {
            if (ex is AggregateException ae)
            {
                foreach (var subException in ae.Flatten().InnerExceptions)
                {
                    yield return subException;
                }

                yield break;
            }

            yield return ex;
            ex = ex.InnerException;
        }
    }


}