using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Summary.GitModels;

public class GitSummaryReport
{
    public string LocalRepoFolder;
    public string GitVersion;
    public List<GitCommitSummary> Commits;
    public List<string> Errors;
}

public class GitCommitSummary
{
    public string FullHash { get; set; }
    public DateTimeOffset CommitDate { get; set; }
    public string BranchName { get; set; }
    public string AuthorName { get; set; }
    public string AuthorEmail { get; set; }
}
