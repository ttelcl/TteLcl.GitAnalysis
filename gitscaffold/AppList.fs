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
    | [] ->
      if o.RepoWitness |> String.IsNullOrEmpty then
        cp "\foMissing \fy-repo\fo argument. \f0(Use \fg-repo \fc.\f0 to use the repo of the current directory)"
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = String.Empty
    SortOrder = RefSort.NoSort
  }

// The actual command execution, taking the parsed Options as argument
let private runList o =
  if o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
    cp $"\frError!\fo Not part of any GIT repository: \f0'\fy{o.RepoWitness}\f0'."
    1
  else
    use gitrepo = new GitRepo(o.RepoWitness)
    cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
    let refs = new ReferenceMap(gitrepo, "refs/scaffold/")
    let commitRefMap = new CommitReferenceMap(refs.References.Values)
    let pairs =
      commitRefMap.CommitsByReference
      |> Seq.map (fun kvp -> (kvp.Key, kvp.Value))
    let sortedPairs =
      match o.SortOrder with
      | RefSort.ByRefName ->
        pairs |> Seq.sortBy (fun (k,_) -> k)
      | RefSort.ByCommitStamp ->
        pairs |> Seq.sortBy (fun (_,v) -> v.Committer.When)
      | RefSort.ByCommitHash ->
        pairs |> Seq.sortBy (fun (_,v) -> v.Sha)
      | RefSort.NoSort ->
        pairs
    let sortedPairs = sortedPairs |> Seq.toArray
    cp $"Found \fb{sortedPairs.Length}\f0 scaffold refs."
    for (refname, commit) in sortedPairs do
      let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss +K")
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

