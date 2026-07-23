module AppReferences

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
  DoList: bool
  DoDump: bool
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
    | "-list" :: rest ->
      rest |> parseMore {o with DoList = true}
    | "-dump" :: rest ->
      rest |> parseMore {o with DoDump = true}
    | [] ->
      o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Witness = Environment.CurrentDirectory
    DoList = false
    DoDump = false
  }

let private runReferences o =
  use repo = new GitRepo(o.Witness)
  cp $"Using repository \fy{repo.Label}\f0 (\fk{repo.GitDbFolder}\f0)"
  let refs = new ReferenceMap(repo)
  cp $"Found \fb{refs.References.Count}\f0 references"
  let sortedRefs =
    refs.References
    |> Seq.sortBy (fun kvp -> kvp.Key)
    |> Seq.map (fun kvp -> kvp.Value)
    |> Seq.toArray
  if o.DoList then
    for r in sortedRefs do
      let name = r.CanonicalName
      let id = r.ResolveToDirectReference().TargetIdentifier
      cp $"\fc{id}\f0  {name}\f0."
  else
    cp "(\fkno \fG-list\fk requested\f0)"
  if o.DoDump then
    let fileName = $"{repo.Label}.references.csv"
    do
      use csv = fileName |> startFile
      csv.WriteLine("prefix,suffix,stamp,commit")
      for r in sortedRefs do
        let dr = r.ResolveToDirectReference()
        let id = dr.TargetIdentifier
        let commitOption =
          match dr.Target with
          | :? Commit as commit ->
            commit |> Some
          | :? TagAnnotation as ta ->
            match ta.Target with
            | :? Commit as commit ->
              commit |> Some
            | _ ->
              // give up
              cp $"\foIgnoring unrecognized reference annotation \fy{r.CanonicalName}\fo annotating type \fr{ta.Target.GetType()}\f0. "
              None
          | _ ->
            // no idea what this is
            cp $"\foIgnoring unrecognized reference \fy{r.CanonicalName}\fo type \fr{dr.Target.GetType()}\f0. "
            None
        match commitOption with
        | Some commit ->
          let nameparts = r.CanonicalName.Split('/')
          let id = commit.Id.Sha
          let prefix, suffix =
            if nameparts[1] = "remotes" then
              String.Join('/', nameparts[0..2])+"/", String.Join('/', nameparts[3..])
            else
              String.Join('/', nameparts[0..1])+"/", String.Join('/', nameparts[2..])
          let stamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          csv.WriteLine($"{prefix},{suffix},{stamp},{id}")
        | None ->
          // unrecognized: skip
          ()
    fileName |> finishFile
  0

let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    Usage.usage "references"
    1
  | Some o ->
    o |> runReferences

