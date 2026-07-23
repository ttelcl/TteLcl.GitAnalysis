module AppCommits

open System
open System.IO
open System.Text

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private Options = {
  Witness: string
  ListCount: int
  DoDump: bool
  IncludeGlobs: string list
  ExcludeGlobs: string list
}

let private parseArgs args =
  let rec parseMore o args =
    match args with
    | "-v":: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-repo" :: witness :: rest ->
      rest |> parseMore {o with Witness = witness}
    | "-list" :: ntxt :: rest ->
      let ok, n = ntxt |> Int32.TryParse
      if ok && n >= 0 then
        rest |> parseMore {o with ListCount = n}
      else
        cp $"\fo-list\fr: cannot parse '{ntxt}' as non-negative number\f0."
        None
    | "-dump" :: rest ->
      rest |> parseMore {o with DoDump = true}
    | "-i" :: includeGlob :: rest ->
      rest |> parseMore {o with IncludeGlobs = includeGlob :: o.IncludeGlobs}
    | "-x" :: excludeGlob :: rest ->
      rest |> parseMore {o with ExcludeGlobs = excludeGlob :: o.ExcludeGlobs}
    | [] ->
      {o with IncludeGlobs = o.IncludeGlobs |> List.rev; ExcludeGlobs = o.ExcludeGlobs |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Witness = Environment.CurrentDirectory
    ListCount = 0
    DoDump = false
    IncludeGlobs = []
    ExcludeGlobs = []
  }

let private runCommits o =
  use repo = new GitRepo(o.Witness)
  let filter = new CommitFilter();
  if o.IncludeGlobs |> List.isEmpty |> not then
    let includes =
      o.IncludeGlobs
      |> Seq.map (fun glob -> repo.Repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.IncludeReachableFrom <- includes
  if o.ExcludeGlobs |> List.isEmpty |> not then
    let excludes =
      o.ExcludeGlobs
      |> Seq.map (fun glob -> repo.Repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.ExcludeReachableFrom <- excludes
  let commitSequence = repo.Repo.Commits.QueryBy(filter)
  let commits = commitSequence |> Seq.toArray
  cp $"Found \fb{commits.Length}\f0 commits"

  cp "\frNYI\f0!"
  1

let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    Usage.usage "commits"
    1
  | Some o ->
    o |> runCommits

