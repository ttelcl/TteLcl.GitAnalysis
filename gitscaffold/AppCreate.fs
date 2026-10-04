module AppCreate

open System

open LibGit2Sharp

open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  CommitSources: string list
  GroupName: string option
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
      if group |> Scaffold.isValidScaffoldGroup |> not then
        cp $"\fo'\fy{group}\fo' is not a valid scaffold group name\f0."
        None
      else
        rest |> parseMore {o with GroupName = group |> Some}
    | "-c" :: commit :: rest ->
      rest |> parseMore {o with CommitSources = commit :: o.CommitSources}
    | [] ->
      if o.CommitSources |> List.isEmpty then
        cp "\foNo commits specified \f0(no \fg-c\f0 options given)"
        None
      elif o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      else
        {o with CommitSources = o.CommitSources |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    CommitSources = []
    GroupName = None
  }

// The actual command execution, taking the parsed Options as argument
let private runApp o =
  use gitrepo = new GitRepo(o.RepoWitness)
  let repo = gitrepo.Repo
  cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
  let commits =
    let filter = new CommitFilter();
    let includes =
      [ "refs/*" ] // for now: include everything and exclude nothing
      |> Seq.map (fun glob -> repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.IncludeReachableFrom <- includes
    repo.Commits.QueryBy(filter)
    |> Seq.toArray
  cp $"Found \fb{commits.Length}\f0 commits in the repository."
  for committish in o.CommitSources do
    cp $"'\fg{committish}\f0'"
    match committish |> Scaffold.tryResolveCommit repo with
    | Scaffold.CommitResolution.Success(commit) ->
      let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
      cp $"  Resolved as commit \fy{commit.Sha}\f0 (\fc{stamp}\f0)"
      let result =
        match o.GroupName with
        | Some(group) -> commit |> Scaffold.createGroupedScaffold group
        | None -> commit |> Scaffold.createCommitScaffold
      match result with
      | Scaffold.RefCreation.Created(r) ->
        cp $"  \fmcreated\f0  reference \fg{r.CanonicalName}\f0"
      | Scaffold.RefCreation.Existing(r) ->
        cp $"  \fwexisting\f0 reference \fg{r.CanonicalName}\f0"
    | Scaffold.CommitResolution.NotFound ->
      cp $"  \foFailed to resolve to an existing commit. \fwSkipping\f0."
    | Scaffold.CommitResolution.Ambiguous(message) ->
      cp $"  \foFailed to resolve to a unique commit ('\fy{committish}\fo' has multiple matches). \fwSkipping\f0."
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
    Usage.usage "create"
    1
  | Some o ->
    o |> runApp

