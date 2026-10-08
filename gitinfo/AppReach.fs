module AppReach

open System

open LibGit2Sharp

open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools
open GitUtils

type IReadOnlySet<'a> = System.Collections.Generic.IReadOnlySet<'a>
type HashSet<'a> = System.Collections.Generic.HashSet<'a>

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  DoTips: bool
  DoRoots: bool
  DoDiff: bool
  Commits: string list
}

let private parseArgs args =
  // A recursive argument parser, peeling off one argument each time.
  // Takes an Options and the list of remaining arguments and returns an
  // "Options option". If None, that indicates an argument parsing error
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-repo" :: repo :: rest ->
      rest |> parseMore {o with RepoWitness = repo}
    | "-roots" :: rest ->
      rest |> parseMore {o with DoRoots = true}
    | "-tips" :: rest ->
      rest |> parseMore {o with DoTips = true}
    | "-diff" :: rest ->
      rest |> parseMore {o with DoDiff = true}
    | "-c" :: committish :: rest ->
      rest |> parseMore {o with Commits = committish :: o.Commits}
    | [] ->
      // The recursion terminator. You probably want to reverse any lists in the Options
      // argument. Also a great place for last minute validation
      if o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      elif o.DoDiff && o.Commits.Length <> 2 then
        cp "\fo'\fg-diff\fo' requires exactly two '\fg-c\fo' options\f0."
        None
      elif o.Commits |> List.isEmpty then
        cp "\foNo commits specified\f0."
        None
      else
        {o with Commits = o.Commits |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    DoTips = false
    DoRoots = false
    DoDiff = false
    Commits = []
  }

type private CommitReach = {
  Sha: string
  Tips: IReadOnlySet<string>
  Domain: IReadOnlySet<string>
  Reach: IReadOnlySet<string>
  Roots: IReadOnlySet<string>
}

// The actual command execution, taking the parsed Options as argument
let private runApp o =
  use gitrepo = new GitRepo(o.RepoWitness)
  cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
  let repo = gitrepo.Repo
  let fullgraph = repo |> CommitStubGraph.FromRepo
  let refs = new ReferenceMap(gitrepo)
  let commitRefMap = new CommitReferenceMap(refs.References.Values)
  let referencesTo commitSha =
    commitSha |> commitRefMap.ReferencesForCommit |> Seq.sort |> Seq.toArray
  let getReach commit =
    let domain = fullgraph.Descendants(commit, true)
    let reach = fullgraph.Ancestors(commit, true)
    {
      Sha = commit.Sha
      Tips = domain |> fullgraph.TipsOf
      Domain = domain
      Reach = reach
      Roots = reach |> fullgraph.RootsOf
    }
  let partSets (set1: #IReadOnlySet<'a>) (set2: #IReadOnlySet<'a>) =
    let only1 = new HashSet<'a>()
    only1.UnionWith(set1)
    only1.ExceptWith(set2)
    let common = new HashSet<'a>()
    common.UnionWith(set1)
    common.IntersectWith(set2)
    let only2 = new HashSet<'a>()
    only2.UnionWith(set2)
    only2.ExceptWith(set1)
    only1, common, only2
  let showParts (label: string) set1 set2 =
    let only1, common, only2 = partSets set1 set2
    cp $"{label, 10}: \fb{only1.Count,5}\f0 only first, \fg{common.Count,5}\f0 in common, \fb{only2.Count,5}\f0 only second"
  let rec resolveCommits results commitList =
    match results, commitList with
    | None, _ -> None
    | Some(tail), (committish :: rest) ->
      match committish |> tryResolveCommit repo with
      | CommitResolution.NotFound ->
        cp $"\foUnknown commit '\fy{committish}\fo'\f0."
        None
      | CommitResolution.Ambiguous(message) ->
        cp $"\foCommit '\fy{committish}\fo' is ambiguous - specify more characters\f0."
        None
      | CommitResolution.Success(commit) ->
        rest |> resolveCommits ((commit :: tail) |> Some)
    | Some(tail), [] ->
      tail |> List.rev |> Some
  match o.Commits |> resolveCommits ([] |> Some) with
  | None ->
    1
  | Some(commits) ->
    let reaches = commits |> Seq.map getReach |> Seq.toArray
    for reach in reaches do
      cp $"\fo{reach.Sha}\f0: \fg{reach.Tips.Count}\f0 tips,  \fc{reach.Domain.Count}\f0 in domain,  \fc{reach.Reach.Count}\f0 reachable,  \fb{reach.Roots.Count}\f0 roots"
      if o.DoTips then
        for sha in reach.Tips do
          let commit = sha |> fullgraph.FindCommit
          if commit = null then
            cp $"\frMissing: \fo{sha}\f0." // should never happen
          else
            let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let references = commit.Sha |> referencesTo
            let referenceText =
              if references.Length = 0 then
                "(\fkno references\f0)"
              elif references.Length = 1 then
                $"[\fo{references[0]}\f0]"
              else
                $"[\fo{references[0]}\f0] and \fb{references.Length-1}\f0 more."
            cp $"   tip: \fc{commit.Sha}\f0  (\fg{stamp}\f0)  {referenceText}"
      if o.DoRoots then
        for sha in reach.Roots do
          let commit = sha |> fullgraph.FindCommit
          if commit = null then
            cp $"\frMissing: \fo{sha}\f0." // should never happen
          else
            let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let references = commit.Sha |> referencesTo
            let referenceText =
              if references.Length = 0 then
                "(\fkno references\f0)"
              elif references.Length = 1 then
                $"[\fo{references[0]}\f0]"
              else
                $"[\fo{references[0]}\f0] and \fb{references.Length-1}\f0 more."
            cp $"  root: \fb{commit.Sha}\f0  (\fy{stamp}\f0)  {referenceText}"
    if o.DoDiff && reaches.Length = 2 then
      let r1 = reaches[0]
      let r2 = reaches[1]
      cp ""
      cp $"Differences and shared commits between \fy{r1.Sha}\f0 and \fy{r2.Sha}\f0."
      showParts "Tips" r1.Tips r2.Tips
      showParts "domain" r1.Domain r2.Domain
      showParts "reach" r1.Reach r2.Reach
      showParts "roots" r1.Roots r2.Roots
    0

// The entry point of this subcommand. Return 0 on success, or 1 on failure.
// "args" is a list of strings
let run args =
  // This example subcommand demonstrates one approach for command line parsing
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    // Something was wrong with the arguments. Give a detailed help message for this command
    Usage.usage "reach"
    1
  | Some o ->
    o |> runApp
