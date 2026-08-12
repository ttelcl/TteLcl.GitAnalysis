module AppChain

open System
open System.IO
open System.Text

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private ChainWalk = {
  CommitKey: string
  UpChildren: bool
  DownParents: bool
}

type private Options = {
  Witness: string
  Walks: ChainWalk list
}

type private Context = {
  Repo: GitRepo
  Options: Options
  CommitsById: CommitMap
  RefMap: CommitReferenceMap
  Graph: CommitStubGraph
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
    | "-from" :: key :: rest ->
      let walk = {CommitKey = key; UpChildren = true; DownParents = false}
      rest |> parseMore {o with Walks = walk :: o.Walks}
    | "-to" :: key :: rest ->
      let walk = {CommitKey = key; UpChildren = false; DownParents = true}
      rest |> parseMore {o with Walks = walk :: o.Walks}
    | "-both" :: key :: rest ->
      let walk = {CommitKey = key; UpChildren = true; DownParents = true}
      rest |> parseMore {o with Walks = walk :: o.Walks}
    | [] ->
      if o.Walks |> List.isEmpty then
        cp "\frNo \fo-from\fr, \fo-to\fr, or \fo-both\fr arguments provided\f0."
        None
      else
        {o with Walks = o.Walks |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Witness = Environment.CurrentDirectory
    Walks = []
  }

let private tryResolveReferenceToCommit (r:Reference) =
  let c = r.TryResolveToCommit()
  if c = null then None else c |> Some

let private runChainWalk ctx walk =
  let repo = ctx.Repo
  let o = ctx.Options
  let commitsById = ctx.CommitsById
  let refMap = ctx.RefMap
  let graph = ctx.Graph
  let getRefs sha =
    let refs =
      refMap.ReferencesForCommit(sha)
      |> Seq.sort
      |> Seq.toList
    String.Join(' ', refs)
  let commitOption =
    let commits =
      walk.CommitKey
      |> repo.Repo.Refs.FromGlob
      |> Seq.choose tryResolveReferenceToCommit
      |> Seq.toArray
    match commits.Length with
    | 0 ->
      let commit = repo.Repo.Lookup<Commit>(walk.CommitKey)
      if commit = null then
        cp $"\foNo commits matching '\fy{walk.CommitKey}\fo' found.\f0 Ignoring."
        None
      else
        commit |> Some
    | 1 ->
      commits[0] |> Some
    | _ ->
      cp $"\fo'\fy{walk.CommitKey}\fo' is ambiguous, matching \fb{commits.Length}\fo commits.\f0 Ignoring."
      None
  match commitOption with
  | Some commit ->
    let sha = commit.Sha
    let shortSha = sha.Substring(0, 8)
    cp $"Tracing \fg{shortSha}\f0."
    let childChain =
      if walk.UpChildren then
        graph.ChildChain(sha) 
        |> Seq.where (fun c -> c.Sha <> sha)
        |> Seq.toList
        |> List.rev
      else
        []
    let parentChain =
      if walk.DownParents then
        graph.ParentChain(commit.Sha)
        |> Seq.where (fun c -> c.Sha <> sha)
        |> Seq.toList
      else
        []
    let chain =
      childChain @ (commit :: parentChain)
    cp $"Chain size {childChain.Length} + 1 + {parentChain.Length} = \fb{chain.Length}\f0."
    let filetag =
      if chain.Length = 0 then
        "" // should never happen
      elif chain.Length = 1 then
        let headtail = chain |> List.head
        headtail.Sha.Substring(0, 8)
      else
        let head = chain |> List.head
        let tail = chain |> List.last
        $"{tail.Sha.Substring(0,8)}-{head.Sha.Substring(0,8)}"
    let fileName = $"{repo.Label}.{filetag}.chain.csv"
    do
      use csv = fileName |> startFile
      csv.WriteLine("id,committed,authored,refs")
      for commit in chain do
        let sha = commit.Sha.Substring(0, 8)
        let committed = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
        let authored = commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K")
        let refs = commit.Sha |> getRefs
        csv.WriteLine($"{sha},{committed},{authored},{refs}")
    fileName |> finishFile
  | None ->
    ()

let private runChain o =
  use repo = new GitRepo(o.Witness)
  let filter = new CommitFilter();
  filter.IncludeReachableFrom <- repo.Repo.Refs.FromGlob("refs/*") |> Seq.toArray
  cp "Loading commit graph."
  let commits = repo.Repo.Commits.QueryBy(filter) |> Seq.toArray
  let commitMap = commits |> CommitMap.FromCommits
  let commitReferenceMap = new CommitReferenceMap(repo.Repo.Refs)
  cpx $"Found \fb{commits.Length}\f0 commits,"
  cpx $" \fg{commitReferenceMap.CommitsByReference.Count}\f0 references,"
  cp $" \fc{commitReferenceMap.ReferencesByCommit.Count}\f0 distinct referenced commits."
  let graph = new CommitStubGraph(commits)
  let ctx = {
    Repo = repo
    Options = o
    CommitsById = commitMap
    RefMap = commitReferenceMap
    Graph = graph
  }
  for walk in o.Walks do
    walk |> runChainWalk ctx
  0

let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    Usage.usage "chain"
    1
  | Some o ->
    o |> runChain
