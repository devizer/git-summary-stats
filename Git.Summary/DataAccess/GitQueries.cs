﻿using System;
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
        public GitSummaryReport BuildFullReport(string gitLocalRepoFolder)
        {
            gitLocalRepoFolder = gitLocalRepoFolder.TrimEnd(Path.DirectorySeparatorChar);
            GitSummaryReport ret = new GitSummaryReport()
            {
                LocalRepoFolder = gitLocalRepoFolder,
                Errors = new List<string>(),
            };
            var trySomething = (Action action) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    ret.Errors.Add(ex.GetExceptionDigest());
                }
            };

            trySomething(() => ret.GitVersion = GitExecutable.GetGitVersion());
            trySomething(() => ret.Branches = GetBranches(gitLocalRepoFolder));
            if (ret.Branches != null)
            {
                void PopulateBranchCommits(GitBranchModel branchModel)
                {
                    trySomething(() => branchModel.Commits = this.GetBranchCommits(gitLocalRepoFolder, branchModel.BranchName));
                }
                
                Parallel.ForEach(
                    ret.Branches,
                    new ParallelLinqOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount + 3 },
                    branch => PopulateBranchCommits(branch)
                );
            }

            /*
            if (ret.Commits != null)
                ParallelGitCommitDetailsQuery.Populate(ret.Commits, gitLocalRepoFolder, ret.Errors);
                */

            if (GitTraceFiles.GitTraceFolder != null)
            {
                var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Full Report.json");
                TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                File.WriteAllText(traceFile, ret.ToJsonString());
            }
            return ret;
        }

        public List<GitCommitSummary> GetBranchCommits(string gitLocalRepoFolder, string branchName)
        {
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

        public List<GitBranchModel> GetBranches(string gitLocalRepoFolder)
        {
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

            List<GitBranchModel> ret = branchNames.Select(x => new GitBranchModel() { BranchName = x }).ToList();
            return ret;
        }

    }
}
