using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using Git.Summary.GitModels;
using Git.Summary.Shared;
using Universe;

namespace Git.Summary.DataAccess
{
    public static class ParallelGitCommitDetailsQuery
    {
        
        // Returns only .Info and .BranchName
        public static IDictionary<string, GitCommitSummary> Populate(this IEnumerable<string> /* unique */ hashCommits, string gitLocalRepoFolder, List<string> errors)
        {
            IDictionary<string, GitCommitSummary> ret = new ConcurrentDictionary<string, GitCommitSummary>(StringComparer.OrdinalIgnoreCase);

            // QueuedTaskScheduler scheduler = new QueuedTaskScheduler(Environment.ProcessorCount + 2);
            LimitedConcurrencyLevelTaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount);
            TaskFactory taskFactory = new TaskFactory(scheduler);
            List<Task> subTasks = new List<Task>();
            Stopwatch startAt = Stopwatch.StartNew();
            int index = 0;
            foreach (string hash in hashCommits)
            {
                void PopulateBranch(GitCommitSummary gitCommitSummary)
                {
                    // git branch --no-color --no-column --format "%(refname:lstrip=2)" --contains f5a9e6b011312007e37441beab43e533dfc3f48f
                    var args = $"branch --no-color --no-column --format \"%(refname:lstrip=2)\" --contains {hash}";
                    var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
                    result.DemandGenericSuccess($"Query branch for commit {hash} of repo '{gitLocalRepoFolder}'");
                    var commitBranchNames = result.OutputText.Split('\r', '\n').Where(x => x.Length > 0).ToList();
                    // TODO: SKIP Commit if it belongs to main/master
                    gitCommitSummary.BranchName = commitBranchNames.FirstOrDefault();
                    gitCommitSummary.BranchNames = commitBranchNames;

                    if (GitTraceFiles.GitTraceFolder != null)
                    {
                        var debugLogMessage = $"{startAt.Elapsed} {Interlocked.Increment(ref index)} of {hashCommits.Count()} | Commit {hash} Branch='{commitBranchNames}'";
                        var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Populate.log");
                        TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                        BuildErrorsHolder.TryTitled(errors, $"Log to {traceFile}", () =>
                        {
                            lock(string.Intern(traceFile))
                                File.AppendAllText(traceFile, $"{debugLogMessage}{Environment.NewLine}");
                        });
                    }
                }

                void PopulateInfo(GitCommitSummary gitCommitSummary)
                {
                    // git show d43f2c485192294338917ce1f9897c73a6c91d06 --stat=99999,99999,99999 > ..\show2.txt
                    var args = $"show --no-color --stat=99999,99999,99999 {hash}";
                    var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
                    result.DemandGenericSuccess($"Query branch for commit {hash} of repo '{gitLocalRepoFolder}'");
                    var commitInfo = result.OutputText;
                    gitCommitSummary.Info = commitInfo;
                    if (GitTraceFiles.GitTraceFolder != null)
                    {
                        var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Commits", $"{hash}.Txt");
                        TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                        BuildErrorsHolder.TryTitled(errors, $"Store trace for commit '{hash}' as '{traceFile}'", () => {
                            lock (string.Intern(traceFile))
                                File.AppendAllText(traceFile, $"{commitInfo}");
                        });
                    }

                }

                Action populate = () =>
                {
                    GitCommitSummary gitCommitSummary = new GitCommitSummary();
                    BuildErrorsHolder.Try(errors, () => PopulateInfo(gitCommitSummary));
                    BuildErrorsHolder.Try(errors, () => PopulateBranch(gitCommitSummary));
                    ret[hash] = gitCommitSummary;
                };
                var task = taskFactory.StartNew(populate);
                subTasks.Add(task);
            }

            subTasks.ForEach(task => task.Wait());
            return ret;
        }

        

    }
}
