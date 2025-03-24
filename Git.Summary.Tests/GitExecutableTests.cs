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
    public void B_TestRawSummary(string testCase)
    {
        GitQueries queries = new GitQueries();
        var summary1 = queries.GetBranchCommits(GetTestGitLocalRepoFolder(), null);
        Console.WriteLine($"Total Commits for default branch: {summary1.Count}");
#if DEBUG
        Console.WriteLine(summary1.ToJsonString());
#endif
    }

    [Test]
    public void TestFullReport()
    {
        GitQueries queries = new GitQueries();
        var summaryFull = queries.BuildFullReport(GetTestGitLocalRepoFolder());
        var totalCommits = summaryFull.Branches?.SelectMany(x => x.Commits);
        var commitsCount = totalCommits?.Count();
        var uniqueCommitsCount = totalCommits?.Select(x => x.FullHash).Distinct().Count();
        Console.WriteLine($"Total Commits: {commitsCount}");
        Console.WriteLine($"Total Unique Commits: {commitsCount}");
        Console.WriteLine($"Memory Usage: {Process.GetCurrentProcess().WorkingSet64 / 1024:n0} KB");

#if DEBUG
        Console.WriteLine(summaryFull.ToJsonString());
#endif
    }

    private static string GetTestGitLocalRepoFolder()
    {
        var raw = Environment.GetEnvironmentVariable("TEST_GIT_LOCAL_REPO_FOLDER");
        return raw ?? "W:\\Temp\\efcore\\";
    }


}