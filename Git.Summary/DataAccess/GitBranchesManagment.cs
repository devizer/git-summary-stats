using Git.Summary.GitModels;
using Git.Summary.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Universe;
using static Git.Summary.DataAccess.GitBranchesManagement;

namespace Git.Summary.DataAccess
{
    public class GitBranchesManagement
    {
        public static readonly string OriginName = "origin";
        public readonly string GitLocalRepoFolder;

        public GitBranchesManagement(string gitLocalRepoFolder)
        {
            GitLocalRepoFolder = gitLocalRepoFolder;
        }

        public string GetCurrentBranch()
        {
            var args = $"branch --no-color --no-column --format \"%(refname:lstrip=2)\"";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, GitLocalRepoFolder);
            result.DemandGenericSuccess($"Query current branch");
            var branchName = result.OutputText.Trim('\r', '\n');
            return branchName;
        }

        public void CheckoutBranch(string branchName)
        {
            var args = $"checkout \"{branchName}\"";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, GitLocalRepoFolder);
            result.DemandGenericSuccess($"Checkout branch {branchName} for {GitLocalRepoFolder}");
        }

        public void FetchPullBranch(FetchPull fetchPull, bool needAll = false)
        {
            var args = $"{fetchPull.ToString().ToLower()}{(needAll ? " --all" : "")}";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, GitLocalRepoFolder);
            result.DemandGenericSuccess($"Git {fetchPull.ToString().ToLower()} for {GitLocalRepoFolder}");
        }

        public enum FetchPull
        {
            Pull,
            Fetch
        }

        public List<string> GetRemotes()
        {
            var args = $"remote";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, GitLocalRepoFolder);
            result.DemandGenericSuccess($"Git Query remotes for {GitLocalRepoFolder}");
            return result.OutputText
                .Split('\r', '\n')
                .Where(x => x.Length > 0)
                .ToList();
        }

        public List<RemoteBranchName> GetStructuresRemoteBranches()
        {
            var remotes = this.GetRemotes();
            var remoteBranchNames = this.GetRemoteBranchNames();

            RemoteBranchName Parse(string rbn)
            {
                var remote = remotes.FirstOrDefault(r => rbn.StartsWith($"{r}/"));
                if (remote != null)
                {
                    var localName = rbn.Length > remote.Length + 1 ? rbn.Substring(remote.Length + 1) : null;
                    if (localName != null) return new RemoteBranchName() { Remote = remote, Name = localName };
                }
                return null;
            }

            return remoteBranchNames.Select(x => Parse(x)).Where(x => x != null).ToList();
        }

        public List<string> GetRemoteBranchNames()
        {
            var gitLocalRepoFolder = GitLocalRepoFolder;
            gitLocalRepoFolder = gitLocalRepoFolder.TrimEnd(Path.DirectorySeparatorChar);
            // git branch --no-color --no-column --format "%(refname:lstrip=2)" -r
            var args = "branch --no-color --no-column --format \"%(refname:lstrip=2)\" -r";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
            if (GitTraceFiles.GitTraceFolder != null)
            {
                var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Branches.txt");
                TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                File.WriteAllText(traceFile, result.OutputText);
            }

            result.DemandGenericSuccess($"Query git branches for {gitLocalRepoFolder}");

            var isNotHead = (string raw) =>
            {
                var arr = raw.Split('/');
                return !(arr.Length == 2 && arr[1] == "HEAD");
            };

            var branchNames = result.OutputText.Split('\r', '\n')
                .Where(x => x.Length != 0)
                .Where(x => isNotHead(x));

            return branchNames.ToList();
        }

    }
}
