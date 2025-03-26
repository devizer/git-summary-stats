namespace Git.Summary.GitModels;

public class GitSummaryReport
{
    public string LocalRepoFolder;
    public string GitVersion;
    public string InitialBranch;
    public List<string> Errors;
    public List<GitBranchModel> Branches;
}