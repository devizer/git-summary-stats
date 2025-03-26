namespace Git.Summary.GitModels;

public class GitBranchModel
{
    public string Remote { get; set; }
    public string BranchName { get; set; }
    public List<GitCommitSummary> Commits { get; set; }

}