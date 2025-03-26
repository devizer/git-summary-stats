using System.Diagnostics;
using Git.Summary.DataAccess;
using Git.Summary.Shared;
using Universe;

namespace Git.Summary.Tests;

public class GitExecutableTests
{
    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void A_TestGitVersion(string testCase)
    {
        Console.WriteLine($"Git Executable: '{GitExecutable.GetGitExecutable()}'");
        Console.WriteLine($"Git Version: '{GitExecutable.GetGitVersion()}'");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void B_ShowCurrentBranch(string testCase)
    {
        GitBranchesManagement man = new GitBranchesManagement(GetTestGitLocalRepoFolder());
        Console.WriteLine($"Current Branch: '{man.GetCurrentBranch()}'");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void C1_ShowRemoteBranches(string testCase)
    {
        GitBranchesManagement man = new GitBranchesManagement(GetTestGitLocalRepoFolder());
        var branches = man.GetRemoteBranchNames();
        Console.WriteLine($"Remote Branches:{Environment.NewLine}{string.Join(Environment.NewLine, branches.OrderBy(x => x))}");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void C2_ShowStructuredRemoteBranches(string testCase)
    {
        GitBranchesManagement man = new GitBranchesManagement(GetTestGitLocalRepoFolder());
        var branches = man.GetStructuresRemoteBranches();
        Console.WriteLine($"Remote Branches:{Environment.NewLine}{string.Join(Environment.NewLine, branches.OrderBy(x => x.Name))}");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void D_FetchRemoteBranches(string testCase)
    {
        GitBranchesManagement man = new GitBranchesManagement(GetTestGitLocalRepoFolder());
        var baseBranch = man.GetCurrentBranch();
        var branches = man.GetStructuresRemoteBranches();
        try
        {
            foreach (var branch in branches)
            {
                Console.WriteLine($"Checkout Branch {branch}");
                Stopwatch sw = Stopwatch.StartNew();
                man.CheckoutBranch(branch.Name);
                Console.WriteLine($"OK: Checkout Branch {branch} in {sw.Elapsed.TotalSeconds:n1} seconds");

                Console.WriteLine($"Fetch Branch {branch}");
                sw.Restart();
                man.FetchPullBranch(GitBranchesManagement.FetchPull.Pull, false);
                Console.WriteLine($"OK: Fetch Branch {branch} in {sw.Elapsed.TotalSeconds:n1} seconds");
            }
        }
        finally
        {
            man.CheckoutBranch(baseBranch);
        }

        Console.WriteLine($"Remote Branches:{Environment.NewLine}{string.Join(Environment.NewLine, branches)}");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void E_TestRawSummary(string testCase)
    {
        GitQueries queries = new GitQueries(GetTestGitLocalRepoFolder());
        var summary1 = queries.GetBranchCommits(null);
        Console.WriteLine($"Total Commits for default branch: {summary1.Count}");
#if DEBUG
        Console.WriteLine(summary1.ToJsonString());
#endif
    }

    [Test]
    public void E_TestFullReport()
    {
        GitQueries queries = new GitQueries(GetTestGitLocalRepoFolder());
        var summaryFull = queries.BuildFullReport();
        var totalCommits = summaryFull.Branches?.SelectMany(x => x.Commits);
        var commitsCount = totalCommits?.Count();
        var uniqueCommitsCount = totalCommits?.Select(x => x.FullHash).Distinct().Count();
        Console.WriteLine($"Total Commits: {commitsCount}");
        Console.WriteLine($"Total Unique Commits: {uniqueCommitsCount}");
        Console.WriteLine($"Memory Usage: {Process.GetCurrentProcess().WorkingSet64 / 1024:n0} KB");


        Console.WriteLine($"{Environment.NewLine}BRANCHES {summaryFull.Branches?.Count}");
        foreach (var branch in summaryFull.Branches)
        {
            Console.WriteLine($"{branch.Commits?.Count,-12:n0} {(branch.Commits?.FirstOrDefault()?.CommitDate),-30} {branch.BranchName}");
        }

        Console.WriteLine($"{Environment.NewLine}ERRORS {summaryFull.Errors.Count}");
        Console.WriteLine(string.Join(Environment.NewLine, summaryFull.Errors));

        var jsonReportFile = Path.GetFullPath(Path.Combine("bin", "Report.json"));
        Console.WriteLine($"Full Report: '{jsonReportFile}'");
        TryAndForget.Execute(() => Directory.CreateDirectory(Path.GetDirectoryName(jsonReportFile)));
        JsonExtensions.ToJsonFile(jsonReportFile, summaryFull);
    }

    private static string GetTestGitLocalRepoFolder()
    {
        var raw = Environment.GetEnvironmentVariable("TEST_GIT_LOCAL_REPO_FOLDER");
        return raw ?? "W:\\Temp\\efcore\\";
    }


}