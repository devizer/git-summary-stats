namespace Git.Summary.GitModels;

public class GitSummaryReport
{
    public string LocalRepoFolder;
    public string GitVersion;
    public List<string> Errors;
    public List<GitBranchModel> Branches;
}