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
        GitTraceFiles.GitTraceFolder = "W:\\Temp\\Git Traces";
        GitQueries queries = new GitQueries();
        var summary1 = queries.GetSummary("W:\\Temp\\efcore\\");
        Console.WriteLine(summary1.ToJsonString());
    }

    [Test]
    public void TestFullReport()
    {
        GitTraceFiles.GitTraceFolder = "W:\\Temp\\Git Traces";
        GitQueries queries = new GitQueries();
        var summaryFull = queries.BuildFullReport("W:\\Temp\\efcore\\");
        Console.WriteLine(summaryFull.ToJsonString());
    }
}