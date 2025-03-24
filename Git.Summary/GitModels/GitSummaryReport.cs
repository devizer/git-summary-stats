namespace Git.Summary.GitModels;

public class GitSummaryReport
{
    public string LocalRepoFolder;
    public string GitVersion;
    public List<GitBranchModel> Branches;
    public List<GitCommitSummary> Commits;
    public List<string> Errors;
}