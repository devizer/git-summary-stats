using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Summary.DataAccess
{
    public class GitTraceFiles
    {
        public static string GitTraceFolder { get; set; }

        static GitTraceFiles()
        {
            var raw = Environment.GetEnvironmentVariable("GIT_TRACE_FOLDER")?.Trim();
            if (!string.IsNullOrEmpty(raw))
                GitTraceFolder = Path.GetFullPath(raw);
        }
    }
}
