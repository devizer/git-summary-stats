using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Universe;

namespace Git.Summary.Shared
{
    public class GitExecutable
    {

        public static string GetGitExecutable()
        {
            var raw = Environment.GetEnvironmentVariable("GIT_EXECUTABLE")?.Trim();
            if (!string.IsNullOrEmpty(raw)) return raw;
            return TinyCrossInfo.IsWindows ? "git.exe" : "git";
        }

        public static string GetGitVersion()
        {
            var result = ExecProcessHelper.HiddenExec(GetGitExecutable(), "--version");
            result.DemandGenericSuccess("Get Git Version", false);
            return result.OutputText
                .Split('\r', '\n')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .FirstOrDefault();
        }
    }
}
