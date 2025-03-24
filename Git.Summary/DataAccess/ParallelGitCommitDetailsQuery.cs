using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public static void Populate(this IEnumerable<GitCommitSummary> commits, string gitLocalRepoFolder, List<string> errors)
        {
            object syncErrors = new object();
            var trySomething = (Action action) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    var errorMessage = ex.GetExceptionDigest();
                    lock (syncErrors) errors.Add(errorMessage);
                }
            };

            // QueuedTaskScheduler scheduler = new QueuedTaskScheduler(Environment.ProcessorCount + 2);
            LimitedConcurrencyLevelTaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount + 2);
            TaskFactory taskFactory = new TaskFactory(scheduler);
            List<Task> subTasks = new List<Task>();
            Stopwatch startAt = Stopwatch.StartNew();
            int index = 0;
            foreach (GitCommitSummary gitCommitSummary in commits)
            {
                void PopulateBranch()
                {
                    // git branch --no-color --no-column --format "%(refname:lstrip=2)" --contains f5a9e6b011312007e37441beab43e533dfc3f48f
                    var args = $"branch --no-color --no-column --format \"%(refname:lstrip=2)\" --contains {gitCommitSummary.FullHash}";
                    var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
                    result.DemandGenericSuccess($"Query branch for commit {gitCommitSummary.FullHash} of repo '{gitLocalRepoFolder}'");
                    var branchName = result.OutputText.Trim('\r', '\n');
                    gitCommitSummary.BranchName = branchName;
                    var debugLogMessage = $"{startAt.Elapsed} {Interlocked.Increment(ref index)} of {commits.Count()} | Commit {gitCommitSummary.FullHash} Branch='{branchName}'";

                    if (GitTraceFiles.GitTraceFolder != null)
                    {
                        var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Populate.log");
                        TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                        File.AppendAllText(traceFile, $"{debugLogMessage}{Environment.NewLine}");
                    }
                }

                void PopulateInfo()
                {
                    // git show d43f2c485192294338917ce1f9897c73a6c91d06 --stat=99999,99999,99999 > ..\show2.txt
                    var args = $"show --no-color --stat=99999,99999,99999 {gitCommitSummary.FullHash}";
                    var result = ExecProcessHelper.HiddenExec(GitExecutable.GetGitExecutable(), args, gitLocalRepoFolder);
                    result.DemandGenericSuccess($"Query branch for commit {gitCommitSummary.FullHash} of repo '{gitLocalRepoFolder}'");
                    var commitInfo = result.OutputText;
                    gitCommitSummary.Info = commitInfo;
                    if (GitTraceFiles.GitTraceFolder != null)
                    {
                        var traceFile = Path.Combine(GitTraceFiles.GitTraceFolder, Path.GetFileName(gitLocalRepoFolder), "Show Branch", $"{gitCommitSummary.CommitDate:yyyy-MM-dd HH-mm-ss} {gitCommitSummary.AuthorName}.Commit.Txt");
                        TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(traceFile)));
                        lock(string.Intern(traceFile))
                            File.AppendAllText(traceFile, $"{commitInfo}");
                    }

                }

                Action populate = () =>
                {
                    trySomething(() => PopulateBranch());
                    trySomething(() => PopulateInfo());
                };
                var task = taskFactory.StartNew(populate);
                subTasks.Add(task);
            }

            subTasks.ForEach(task => task.Wait());
        }

    }
}
