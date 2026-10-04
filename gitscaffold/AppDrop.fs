module AppDrop

open System

open LibGit2Sharp

open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

open Scaffold

type RefRange =
  | Ungrouped
  | Grouped of string
  | All

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  Range: RefRange
  Commits: string list
  Force: bool
  Dry: bool
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
    | "-g" :: group :: rest ->
      if group |> isValidScaffoldGroup |> not then
        cp $"\fo'\fy{group}\fo' is not a valid scaffold group name\f0."
        None
      else
        match o.Range with
        | RefRange.All ->
          cp "\fo'\fy-g\fo' and '\fy-all\fo' are mutually exclusive"
          None
        | _ ->
          rest |> parseMore {o with Range = group |> RefRange.Grouped}
    | "-all" :: rest ->
      match o.Range with
      | RefRange.Grouped(_) ->
        cp "\fo'\fy-g\fo' and '\fy-all\fo' are mutually exclusive"
        None
      | _ ->
        rest |> parseMore {o with Range = RefRange.All}
    | "-c" :: commit :: rest ->
      rest |> parseMore {o with Commits = commit :: o.Commits}
    | "-F" :: rest | "-force" :: rest ->
      rest |> parseMore {o with Force = true}
    | "-dry" :: rest | "-whatif" :: rest ->
      rest |> parseMore {o with Dry = true}
    | [] ->
      if o.Commits |> List.isEmpty then
        cp "\foNo commits to drop specified\f0."
        None
      elif o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      else
        {o with Commits = o.Commits |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    Range = RefRange.Ungrouped
    Commits = []
    Force = false
    Dry = false
  }

// The actual command execution, taking the parsed Options as argument
let private runApp o =
  use gitrepo = new GitRepo(o.RepoWitness)
  let repo = gitrepo.Repo
  cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
  let commits =
    let filter = new CommitFilter();
    let includes =
      [ "refs/*" ]
      |> Seq.map (fun glob -> repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.IncludeReachableFrom <- includes
    repo.Commits.QueryBy(filter)
    |> Seq.toArray
  cp $"Found \fb{commits.Length}\f0 commits in the repository."
  let graph = new CommitStubGraph(commits)
  let refs = new ReferenceMap(gitrepo)
  let commitRefMap = new CommitReferenceMap(refs.References.Values)

  (* Something not working right. Using git's own repo as sample
  > gitscaffold list -repo k:\src\github\git -byhash  
    2005-12-27 21:13:01 -08:00 2dcfb57d0b15390f9e270a7466338fdc1f74aec4 refs/scaffold/g/2005-12-29/2dcfb57d0b.
    2005-12-27 21:13:01 -08:00 2dcfb57d0b15390f9e270a7466338fdc1f74aec4 refs/scaffold/g/2006-01-01/2dcfb57d0b.
  > gitscaffold drop -repo k:\src\github\git -dry -c 2dcfb57d -all -v
    KeyNotFoundException: The given key 'refs/tags/junio-gpg-pub' was not present in the dictionary..
  *)
  
  cp $"Found {refs.References.Count} references"
  cp $"  == {commitRefMap.CommitsByReference.Count} references"
  cp $"  {commitRefMap.ReferencesByCommit.Count} distinct commits"

  let refsToDropForCommit (commit: Commit) =
    let tag = commit |> shatag
    let allRefs = commitRefMap.ReferencesForCommit(commit.Sha)
    match o.Range with
    | RefRange.Ungrouped ->
      let exactName = $"refs/scaffold/c/{tag}"
      allRefs
      |> Seq.where (fun r -> r = exactName) // one or zero matches
      |> Seq.toArray
    | RefRange.Grouped(group) ->
      let exactName = $"refs/scaffold/g/{group}/{tag}"
      allRefs
      |> Seq.where (fun r -> r = exactName) // one or zero matches
      |> Seq.toArray
    | RefRange.All ->
      let prefix = "refs/scaffold/"
      let suffix = $"/{tag}"
      allRefs
      |> Seq.where (fun r -> r.StartsWith(prefix) && r.EndsWith(suffix))
      |> Seq.toArray
  
  let refsToDropFor comittish =
    let resolution = comittish |> tryResolveCommit repo
    match resolution with
    | CommitResolution.Success(commit) ->
      commit |> refsToDropForCommit
    | CommitResolution.NotFound ->
      cp $"\foNo matching commits for '\fy{comittish}\fo' \fw-> ignoring\f0."
      [||]
    | CommitResolution.Ambiguous(_) ->
      cp $"\foAmbiguous pattern matching multiple commits '\fy{comittish}\fo' \fw-> ignoring\f0."
      [||]

  let dropCandidateSet =
    o.Commits
    |> Seq.map refsToDropFor
    |> Seq.concat
    //|> Seq.distinct
    //|> Seq.map (fun name -> repo.Refs[name])
    |> Set.ofSeq

  let dropCommits =
    dropCandidateSet
    |> Seq.map (fun refname -> commitRefMap.CommitsByReference[refname])
    |> Seq.toArray
  
  let allRefSet = 
    refs.References.Keys
    |> Set.ofSeq

  let refForRefName name = repo.Refs[name]

  let commitForRefName name = commitRefMap.CommitsByReference[name]
  
  let remainingRefNames = Set.difference allRefSet dropCandidateSet
  let remainingPairs =
    remainingRefNames
    |> Seq.map (fun refname -> (refname |> refForRefName, refname |> commitForRefName))
    |> Seq.toArray
    
  let refCommitPairReachableFrom commit =
    let descendents = graph.Descendants(commit, true)
    remainingPairs
    |> Seq.where (fun (r,c) -> descendents.Contains(c.Sha))
  
  cp "\fmDebug\f0."
  cp "Candidate refs to drop:"
  for candidateName in dropCandidateSet do
    let commit = commitRefMap.CommitsByReference[candidateName]
    let reachableFrom = commit |> refCommitPairReachableFrom |> Seq.toArray
    cp $"  \fo{candidateName}\f0 reachable from \fb{reachableFrom.Length}\f0 tips."
    for (r,c) in reachableFrom do
      cp $"    \fc{c.Sha}  \fy{r.CanonicalName}\f0."
  
  cp "\frNot Yet Implented\f0."
  1

// The entry point of this subcommand. Return 0 on success, or 1 on failure.
// "args" is a list of strings
let run args =
  // This example subcommand demonstrates one approach for command line parsing
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    // Something was wrong with the arguments. Give a detailed help message for this command
    Usage.usage "drop"
    1
  | Some o ->
    o |> runApp

