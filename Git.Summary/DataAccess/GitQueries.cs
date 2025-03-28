﻿using System;
using System.Collections.Concurrent;
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
                // TOTAL BRANCHES: Parents
                ConcurrentDictionary<string,GitBranchesManagement.HashParents> totalParents = new ConcurrentDictionary<string, GitBranchesManagement.HashParents>(StringComparer.InvariantCultureIgnoreCase);
                void PopulateBranchCommitsAndParents(GitBranchModel branchModel)
                {
                    BuildErrorsHolder.Try(ret.Errors, () => branchModel.Commits = this.GetBranchCommits(branchModel.BranchName));
                    BuildErrorsHolder.Try(ret.Errors, () =>
                    {
                        var localParents = new GitBranchesManagement(gitLocalRepoFolder).GetAllParentsForBranch(branchModel.BranchName);
                        foreach (var localParent in localParents) totalParents[localParent.Hash] = localParent;
                    });
                }

                Parallel.ForEach(
                    ret.Branches,
                    new ParallelLinqOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    branch => PopulateBranchCommitsAndParents(branch)
                );

                // Copy Parents from totalParents to ret.Branches
                foreach (var gitBranchModel in ret.Branches)
                foreach (var gitCommitSummary in gitBranchModel.Commits)
                {
                    if (totalParents.TryGetValue(gitCommitSummary.FullHash, out var parents))
                        gitCommitSummary.Parents = parents.Parents;
                }

                // Populating GitCommitSummary.BranchNames
                Dictionary<string, GitCommitSummary> uniqueCommitHashes = new Dictionary<string, GitCommitSummary>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var gitBranchModel in ret.Branches)
                foreach (var gitCommitSummary in gitBranchModel.Commits)
                {
                    if (!uniqueCommitHashes.TryGetValue(gitCommitSummary.FullHash, out var uniqueCommit))
                        uniqueCommitHashes[gitCommitSummary.FullHash] = uniqueCommit = new GitCommitSummary();

                    uniqueCommit.BranchNames = uniqueCommit.BranchNames == null ? new List<string>() : uniqueCommit.BranchNames;
                    uniqueCommit.BranchNames.Add(gitBranchModel.BranchName);
                }

                foreach (var gitBranchModel in ret.Branches)
                    foreach (GitCommitSummary gitCommitSummary in gitBranchModel.Commits)
                        if (uniqueCommitHashes.TryGetValue(gitCommitSummary.FullHash, out var uniqueCommit))
                            gitCommitSummary.BranchNames = uniqueCommit.BranchNames;
                // Done: Populating GitCommitSummary.BranchNames

                // Populating GitCommitSummary.BranchName
                // If multiple .BranchNames than select oldest branch
                foreach (var gitBranchModel in ret.Branches)
                foreach (GitCommitSummary gitCommitSummary in gitBranchModel.Commits)
                {
                    var branchNamesCount = (gitCommitSummary.BranchNames?.Count).GetValueOrDefault();
                    if (branchNamesCount >= 2)
                    {
                        var branch = gitCommitSummary.BranchNames
                            .Select(name => ret.Branches.FirstOrDefault(b => b.BranchName == name))
                            .Where(b => b?.OldestCommitDate.HasValue == true)
                            .MinBy(b => b.OldestCommitDate);

                        if (branch != null) gitCommitSummary.BranchName = branch.BranchName;
                    }
                    else if (branchNamesCount == 1)
                    {
                        gitCommitSummary.BranchName = gitCommitSummary.BranchNames[0];
                    }
                }

                ret.Branches = ret.Branches
                    .OrderByDescending(x => x.OldestCommitDate)
                    .ThenByDescending(x => (x.Commits?.Count).GetValueOrDefault())
                    .ToList();

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
                                ParseCommitInfo(gitCommitSummary);
                            }

                            if (found.BranchName != null) gitCommitSummary.BranchName = found.BranchName;
                            if (found.BranchNames != null) gitCommitSummary.BranchNames = found.BranchNames;
                        }
                    }

                    var tail = gitBranchModel.Commits
                        .Skip(1)
                        .Where(commit => commit.BranchName == gitBranchModel.BranchName || string.IsNullOrEmpty(commit.BranchName));

                    // Can't Remove commits that belongs to parent branch
                    // gitBranchModel.Commits = gitBranchModel.Commits.Take(1).Concat(tail).ToList();
                }
            }


            // Return value is not necessary in traces
            if (false && GitTraceFiles.GitTraceFolder != null)
            {
                var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Full Report.json");
                BuildErrorsHolder.TryTitled(ret.Errors, $"Store full report as {traceFile}", () => {
                    TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                    JsonExtensions.ToJsonFile(traceFile, ret, false);
                });
            }
            return ret;
        }

        private void ParseCommitInfo(GitCommitSummary gitCommitSummary)
        {
            if (string.IsNullOrEmpty(gitCommitSummary.Info)) return;
            StringReader rdr = new StringReader(gitCommitSummary.Info);
            string line = null;
            string summaryChanges = null;
            string merge = null;
            int countEmptyLines = 0;
            while (true)
            {
                line = rdr.ReadLine();
                if (line == null) break;
                if (line.Length == 0) countEmptyLines++;
                if (line.Length > 0 && countEmptyLines >= 2) summaryChanges = line;
                if (line.StartsWith("Merge: ") && line.Length > 7) merge = line.Substring(7);
            }

            gitCommitSummary.Merge = merge;
            gitCommitSummary.SummaryChanges = summaryChanges?.Trim();
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
            var branchNames = man.GetStructuredRemoteBranches();
            List<GitBranchModel> ret = branchNames.Select(x => new GitBranchModel() { BranchName = x.Name, Remote = x.Remote}).ToList();
            return ret;
        }

    }
}
