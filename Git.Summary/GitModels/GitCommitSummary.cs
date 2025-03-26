using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Git.Summary.GitModels;

public class GitCommitSummary
{
    public string FullHash { get; set; }
    public DateTimeOffset CommitDate { get; set; }
    public string Merge { get; set; } // optional, contain two commits
    [JsonIgnore]
    public List<string> BranchNames { get; set; }
    [JsonProperty("BranchNames")] 
    public string BranchNamesDebug => BranchNames == null ? null : string.Join("|", BranchNames);
    public string BranchName { get; set; }
    public string AuthorName { get; set; }
    public string AuthorEmail { get; set; }
    public string Info { get; set; }
    public string SummaryChanges { get; set; } // Last line of show
}
