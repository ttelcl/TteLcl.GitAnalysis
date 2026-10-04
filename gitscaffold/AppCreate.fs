module AppCreate

open System

open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private CommitSource =
  | CommitHash of string
  | TagName of string
  | BranchName of string

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  CommitSources: CommitSource list
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
      rest |> parseMore {o with CommitSources = CommitSource.CommitHash(commit) :: o.CommitSources}
    // TODO: -t and -b, unless -c magically happens to support them
    | [] ->
      if o.CommitSources |> List.isEmpty then
        cp "\foNo commits specified \f0(no \fg-c\f0, \fg-t\f0, \fg-b\f0 options given)"
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
    Usage.usage "create"
    1
  | Some o ->
    o |> runApp

