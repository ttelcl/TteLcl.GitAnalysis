module AppStamp

open System
open System.IO
open System.Text

open LibGit2Sharp

open TteLcl.GitModel

open ColorPrint
open CommonTools

type private StampSource =
  | CommitStamp
  | AuthorStamp

type private TouchTarget =
  | AlwaysTouch of string
  | NewerTouch of string

type private Options = {
  RepoWitness: string
  StampType: StampSource
  Reference: string
  Touches: TouchTarget list
}

let private runStamp o =
  let witness =
    if o.RepoWitness |> String.IsNullOrEmpty then
      Environment.CurrentDirectory
    else
      o.RepoWitness |> Path.GetFullPath
  let repositoryPath = witness |> Repository.Discover
  if repositoryPath |> String.IsNullOrEmpty then
    cp $"\frNo repository found for \fo{witness}\f0."
    1
  else
    use repository = new Repository(repositoryPath)
    let commit = repository.Lookup<Commit>(o.Reference)
    if commit = null then
      cp $"\frNo such commit or reference: \fo{o.Reference}\f0."
      1
    else
      let stamp, label =
        match o.StampType with
        | CommitStamp ->
          commit.Committer.When, "committed"
        | AuthorStamp ->
          commit.Author.When, "authored"
      let commitId = commit.Id.Sha.Substring(0, 8)
      let stampText = stamp.ToString("yyyy-MM-dd HH:mm:ss K")
      cp $"Commit \fb{commitId}\f0 in \fc{repositoryPath}\f0 was \fy{label}\f0 on \fg{stampText}\f0."
      let utcStamp = stamp.UtcDateTime
      for touchTarget in o.Touches do
        match touchTarget with
        | AlwaysTouch touchFile ->
          if touchFile |> File.Exists then
            cp $"  Updating time stamp of \fg{touchFile}\f0."
            File.SetLastWriteTimeUtc(touchFile, utcStamp)
          else
            cp $"  Creating \fg{touchFile}\f0 and updating its time stamp."
            File.Create(touchFile).Dispose()
            File.SetLastWriteTimeUtc(touchFile, utcStamp)
        | NewerTouch touchFile ->
          if touchFile |> File.Exists then
            let existingStamp = File.GetLastWriteTimeUtc(touchFile)
            if existingStamp >= utcStamp then
              cp $"  \foExisting time stamp of \fg{touchFile}\fo is already newer.\f0 Not updating."
            else
              cp $"  Updating time stamp of \fg{touchFile}\f0."
              File.SetLastWriteTimeUtc(touchFile, utcStamp)
          else
            cp $"  Creating \fg{touchFile}\f0 and updating its time stamp."
            File.Create(touchFile).Dispose()
            File.SetLastWriteTimeUtc(touchFile, utcStamp)
      0

let run args =
  let rec parseMore o args =
    match args with
    | "-v":: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-repo" :: witness :: rest ->
      rest |> parseMore {o with RepoWitness = witness}
    | "-ref" :: reference :: rest ->
      if reference |> String.IsNullOrEmpty then
        rest |> parseMore {o with Reference = "HEAD"}
      else
        rest |> parseMore {o with Reference = reference}
    | "-a" :: rest ->
      rest |> parseMore {o with StampType = AuthorStamp}
    | "-t" :: file :: rest ->
      rest |> parseMore {o with Touches = AlwaysTouch(file) :: o.Touches}
    | "-T" :: file :: rest ->
      rest |> parseMore {o with Touches = NewerTouch(file) :: o.Touches}
    | [] ->
      {o with Touches = o.Touches |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    RepoWitness = null
    StampType = CommitStamp
    Reference = "HEAD"
    Touches = []
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "info"
    1
  | Some o ->
    o |> runStamp

