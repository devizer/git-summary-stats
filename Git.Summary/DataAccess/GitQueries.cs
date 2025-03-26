using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Git.Summary.GitModels;
using Git.Summary.Shared;
using Universe;

namespace Git.Summary.DataAccess
{
    public class GitQueries
    {
        public readonly string GitLocalRepoFolder;

        public GitQueries(string gitLocalRepoFolder)
        {
            GitLocalRepoFolder = gitLocalRepoFolder;
        }

        public GitSummaryReport BuildFullReport()
        {
            var gitLocalRepoFolder = GitLocalRepoFolder;
            gitLocalRepoFolder = gitLocalRepoFolder.TrimEnd(Path.DirectorySeparatorChar);
            GitSummaryReport ret = new GitSummaryReport()
            {
                LocalRepoFolder = gitLocalRepoFolder,
                Errors = new List<string>(),
            };
            BuildErrorsHolder.Try(ret.Errors, () => ret.GitVersion = GitExecutable.GetGitVersion());
            BuildErrorsHolder.Try(ret.Errors, () => ret.Branches = GetBranches());
            BuildErrorsHolder.Try(ret.Errors, () => ret.InitialBranch = new GitBranchesManagement(GitLocalRepoFolder).GetCurrentBranch());
            if (ret.Branches != null)
            {
                void PopulateBranchCommits(GitBranchModel branchModel)
                {
                    BuildErrorsHolder.Try(ret.Errors, () => branchModel.Commits = this.GetBranchCommits(branchModel.BranchName));
                }

                Parallel.ForEach(
                    ret.Branches,
                    new ParallelLinqOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    branch => PopulateBranchCommits(branch)
                );

                ret.Branches = ret.Branches.OrderByDescending(x => (x.Commits?.Count).GetValueOrDefault()).ToList();

                // Commit Info and Branch Name
                var uniqueCommits = ret.Branches.SelectMany(x => x.Commits).Select(x => x.FullHash).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
                var d = ParallelGitCommitDetailsQuery.Populate(uniqueCommits, gitLocalRepoFolder, ret.Errors);
                foreach (var gitBranchModel in ret.Branches) 
                {
                    foreach (var gitCommitSummary in gitBranchModel.Commits)
                    {
                        if (d.TryGetValue(gitCommitSummary.FullHash, out var found))
                        {
                            if (!string.IsNullOrEmpty(found.Info))
                            {
                                gitCommitSummary.Info = found.Info;
                                ParseInfo(gitCommitSummary);
                            }
                            gitCommitSummary.BranchName = found.BranchName;
                            gitCommitSummary.BranchNames = found.BranchNames;
                        }
                    }

                    var tail = gitBranchModel.Commits
                        .Skip(1)
                        .Where(x => $"${x.BranchName}" == gitBranchModel.BranchName || string.IsNullOrEmpty(x.BranchName));

                    // gitBranchModel.Commits = gitBranchModel.Commits.Take(1).Concat(tail).ToList();
                }
            }


            if (GitTraceFiles.GitTraceFolder != null)
            {
                var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Full Report.json");
                BuildErrorsHolder.TryTitled(ret.Errors, $"Store full report as {traceFile}", () => {
                    TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                    JsonExtensions.ToJsonFile(traceFile, ret, false);
                });
            }
            return ret;
        }

        private void ParseInfo(GitCommitSummary gitCommitSummary)
        {
            if (string.IsNullOrEmpty(gitCommitSummary.Info)) return;
            StringReader rdr = new StringReader(gitCommitSummary.Info);
            string line = null;
            string summaryChanges = null;
            string merge = null;
            while (true)
            {
                line = rdr.ReadLine();
                if (line == null) break;
                if (line.Length > 0) summaryChanges = line;
                if (line.StartsWith("Merge: ") && line.Length > 7) merge = line.Substring(7);
            }

            gitCommitSummary.Merge = merge;
            gitCommitSummary.SummaryChanges = summaryChanges;
        }

        public List<GitCommitSummary> GetBranchCommits(string branchName)
        {
            var gitLocalRepoFolder = GitLocalRepoFolder;
            gitLocalRepoFolder = gitLocalRepoFolder.TrimEnd(Path.DirectorySeparatorChar);
            var b = string.IsNullOrEmpty(branchName?.Trim()) ? "" : $"{branchName} ";
            string args = $"log {b}--date=format:\"%a %Y-%m-%d %H:%M:%S %z\" --pretty=\"format:%H │ %cd │ %aD │ %aI │ %an │ %ae \"";
            var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
            if (GitTraceFiles.GitTraceFolder != null)
            {
                var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), $"Branch {SafeFileName.Get(branchName)}.txt");
                TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                // lock(string.Intern(traceFile)
                File.WriteAllText(traceFile, $"BRANCH '{branchName}'{Environment.NewLine}{result.OutputText}");
            }

            result.DemandGenericSuccess($"Query git log for branch '{branchName}' for '{gitLocalRepoFolder}'");
            var rawRows = result.OutputText
                .Split('\r', '\n')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            List<GitCommitSummary> ret = new List<GitCommitSummary>();
            foreach (var rawRow in rawRows)
            {
                var rawItems = rawRow.Split('│').Select(x => x.Trim()).ToArray();
                if (rawItems.Length < 5) continue;
                var iso8601String = rawItems[3];
                if (!DateTimeOffset.TryParse(iso8601String, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var commitDate))
                {
                    Console.WriteLine($"WARING! Unable to parse ISO-8601 date '{iso8601String}'");
                }

                GitCommitSummary gitCommitSummary = new GitCommitSummary()
                {
                    AuthorEmail = rawItems[rawItems.Length - 1],
                    AuthorName = rawItems[rawItems.Length - 2],
                    FullHash = rawItems[0],
                    CommitDate = commitDate,
                };
                ret.Add(gitCommitSummary);
            }

            return ret;
        }

        public List<GitBranchModel> GetBranches()
        {
            GitBranchesManagement man = new GitBranchesManagement(GitLocalRepoFolder);
            var branchNames = man.GetStructuresRemoteBranches();
            List<GitBranchModel> ret = branchNames.Select(x => new GitBranchModel() { BranchName = x.Name, Remote = x.Remote}).ToList();
            return ret;
        }

    }
}
