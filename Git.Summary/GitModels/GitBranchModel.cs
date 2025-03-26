namespace Git.Summary.GitModels;

public class GitBranchModel
{
    public string Remote { get; set; }
    public string BranchName { get; set; }
    public DateTimeOffset? OldestCommitDate => Commits?.LastOrDefault()?.CommitDate;
    public int CommitsCount => (Commits?.Count).GetValueOrDefault();
    public string ParentsSummary => GetParentsSummary();
    public List<GitCommitSummary> Commits { get; set; }

    string GetParentsSummary()
    {
        var list = Commits?.Select(x => x.Parents?.Count).Distinct().OrderBy(x => x).Select(x => x == null ? "null" : $"{x}");
        return string.Join(";", list);
    }





}