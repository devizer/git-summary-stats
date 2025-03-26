namespace Git.Summary.GitModels;

public class RemoteBranchName
{
    public string Remote { get; set; }
    public string Name { get; set; }

    public override string ToString()
    {
        return $"{nameof(Remote)}: '{Remote}', {nameof(Name)}: «{Name}»";
    }
}