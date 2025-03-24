using Git.Summary.DataAccess;
using Git.Summary.Shared;
using Universe;

namespace Git.Summary.Tests;

public class GitExecutableTests
{
    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void TestGitVersion(string testCase)
    {
        Console.WriteLine($"Git Executable: '{GitExecutable.GetGitExecutable()}'");
        Console.WriteLine($"Git Version: '{GitExecutable.GetGitVersion()}'");
    }

    [Test]
    [TestCase("First")]
    [TestCase("Next")]
    public void TestRawSummary(string testCase)
    {
        GitQueries queries = new GitQueries();
        var summary1 = queries.GetSummary(GetTestGitLocalRepoFolder());
        Console.WriteLine(summary1.ToJsonString());
    }

    [Test]
    public void TestFullReport()
    {
        GitQueries queries = new GitQueries();
        var summaryFull = queries.BuildFullReport(GetTestGitLocalRepoFolder());
        Console.WriteLine(summaryFull.ToJsonString());
    }

    private static string GetTestGitLocalRepoFolder()
    {
        var raw = Environment.GetEnvironmentVariable("TEST_GIT_LOCAL_REPO_FOLDER");
        return raw ?? "W:\\Temp\\efcore\\";
    }


}