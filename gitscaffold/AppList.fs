module AppList

open System

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private RefSort =
  | NoSort
  | ByRefName
  | ByCommitStamp
  | ByCommitHash

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  SortOrder: RefSort
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
    | "-bytime" :: rest ->
      rest |> parseMore {o with SortOrder = RefSort.ByCommitStamp}
    | "-byname" :: rest ->
      rest |> parseMore {o with SortOrder = RefSort.ByRefName}
    | "-g" :: groupname :: rest ->
      if groupname |> Scaffold.isValidScaffoldGroup then
        rest |> parseMore {o with GroupName = groupname |> Some}
      else
        cp $"\fo'\fr{groupname}\fo' is not a valid scaffold group name\f0."
        None
    | [] ->
      if o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    SortOrder = RefSort.NoSort
    GroupName = None
  }

// The actual command execution, taking the parsed Options as argument
let private runList o =
  use gitrepo = new GitRepo(o.RepoWitness)
  cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
  let filter =
    match o.GroupName with
    | None -> "refs/scaffold/"
    | Some(group) -> $"refs/scaffold/{group}/"
  let refs = new ReferenceMap(gitrepo, filter)
  let commitRefMap = new CommitReferenceMap(refs.References.Values)
  let pairs =
    commitRefMap.CommitsByReference
    |> Seq.map (fun kvp -> (kvp.Key, kvp.Value))
  let sortedPairs =
    match o.SortOrder with
    | RefSort.ByRefName ->
      pairs |> Seq.sortBy (fun (k,_) -> k)
    | RefSort.ByCommitStamp ->
      pairs |> Seq.sortByDescending (fun (_,v) -> v.Committer.When)
    | RefSort.ByCommitHash ->
      pairs |> Seq.sortBy (fun (_,v) -> v.Sha)
    | RefSort.NoSort ->
      pairs
  let sortedPairs = sortedPairs |> Seq.toArray
  cp $"Found \fb{sortedPairs.Length}\f0 scaffold refs."
  for (refname, commit) in sortedPairs do
    let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
    let name = refname
    let sha = commit.Sha
    cp $"\fc{stamp} \fy{sha} \fg{name}\f0."
  0

// The entry point of this subcommand. Return 0 on success, or 1 on failure.
// "args" is a list of strings
let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    // Something was wrong with the arguments. Give a detailed help message for this command
    Usage.usage "list"
    1
  | Some o ->
    o |> runList

