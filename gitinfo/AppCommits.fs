module AppCommits

open System
open System.IO
open System.Text

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type UserText =
  | NoUser
  | UserOnly
  | EmailOnly
  | UserAndEmail
  | UserHash

type private Options = {
  Witness: string
  TagLength: int
  ListCount: int
  DoDump: bool
  DoTips: bool
  DoShow: bool
  DoEdges: bool
  DoGraph: bool
  DoJson: bool
  UserStyle: UserText
  IncludeMessage: bool
  IncludeSha: bool
  IncludeGlobs: string list
  ExcludeGlobs: string list
  Label: string
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
    | "-tips" :: rest ->
      rest |> parseMore {o with DoTips = true}
    | "-show" :: rest ->
      rest |> parseMore {o with DoShow = true}
    | "-edges" :: rest
    | "-edge" :: rest ->
      rest |> parseMore {o with DoEdges = true}
    | "-graph" :: rest ->
      rest |> parseMore {o with DoGraph = true}
    | "-json" :: rest ->
      rest |> parseMore {o with DoJson = true}
    | "-name" :: rest
    | "-names" :: rest ->
      let newStyle =
        match o.UserStyle with
        | UserText.EmailOnly | UserText.UserAndEmail ->
          UserText.UserAndEmail
        | _ -> UserText.UserOnly
      rest |> parseMore {o with UserStyle = newStyle}
    | "-email" :: rest
    | "-emails" :: rest ->
      let newStyle =
        match o.UserStyle with
        | UserText.UserOnly | UserText.UserAndEmail ->
          UserText.UserAndEmail
        | _ -> UserText.EmailOnly
      rest |> parseMore {o with UserStyle = newStyle}
    | "-anon" :: rest | "-hash" :: rest | "-userhash" :: rest ->
      rest |> parseMore {o with UserStyle = UserText.UserHash}
    | "-nomessage" :: rest 
    | "-no-message" :: rest ->
      rest |> parseMore {o with IncludeMessage = false}
    | "-nosha" :: rest 
    | "-no-sha" :: rest ->
      rest |> parseMore {o with IncludeSha = false}
    | "-label" :: label :: rest ->
      rest |> parseMore {o with Label = label}
    | "-taglength" :: tagLengthText :: rest
    | "-tl" :: tagLengthText :: rest ->
      let ok, tagLength = tagLengthText |> Int32.TryParse
      if ok && tagLength > 0 && tagLength <= 40 then
        rest |> parseMore {o with TagLength = tagLength}
      else
        cp $"\foInvalid \fg-taglength\fo. Expecting a number from 1 to 40 but got \f0'\fy{tagLengthText}\f0'"
        None
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
    TagLength = 8
    ListCount = 0
    DoDump = false
    DoTips = false
    DoShow = false
    DoEdges = false
    DoGraph = false
    DoJson = false
    UserStyle = UserText.NoUser
    IncludeMessage = true
    IncludeSha = true
    IncludeGlobs = []
    ExcludeGlobs = []
    Label = null
  }

type private CommitSide =
  | Tip
  | Intern
  | Tail


type private TipTailCommit = {
  Sha: string
  Side: CommitSide
  Stamp: DateTimeOffset
  Stamp2: DateTimeOffset
}

type private ClassifiedRef =
  | Branch of string
  | Remote of string
  | Tag of string
  | Other of string

type private ClassifiedRefs = {
  Branches: string list
  Remotes: string list
  Tags: string list
  Others: string list
}

let private commitTag o (commit: Commit) =
  commit.Sha.Substring(0, o.TagLength)

let timeSeparation (t1:DateTimeOffset) (t2:DateTimeOffset) =
  if t1.Year <> t2.Year then
    "5(year)", "", t1.ToString("yyyy"), t2.ToString("yyyy")
  elif t1.Month <> t2.Month then
    "4(month)", t1.ToString("yyyy") + "-", t1.ToString("MM"), t2.ToString("MM")
  elif t1.Day <> t2.Day then
    "3(day)", t1.ToString("yyyy-MM") + "-", t1.ToString("dd"), t2.ToString("dd")
  elif t1.Hour <> t2.Hour then
    "2(hour)", t1.ToString("yyyy-MM-dd") + " ", t1.ToString("HH"), t2.ToString("HH")
  elif t1.Minute <> t2.Minute then
    "1(minute)", t1.ToString("yyyy-MM-dd HH") + ":", t1.ToString("mm"), t2.ToString("mm")
  else
    "0(instant)", t1.ToString("yyyy-MM-dd HH:mm") + ":", t1.ToString("ss"), t2.ToString("ss")

let private foldRefs refs =
  let foldRef state r =
    match r with
    | Branch b -> {state with Branches = b :: state.Branches}
    | Remote r -> {state with Remotes = r :: state.Remotes}
    | Tag t -> {state with Tags = t :: state.Tags}
    | Other o -> {state with Others = o :: state.Others}
  let folded = refs |> Seq.fold foldRef {
      Branches = []
      Remotes = []
      Tags = []
      Others = []
    }
  {
    Branches = folded.Branches |> List.rev
    Remotes = folded.Remotes |> List.rev
    Tags = folded.Tags |> List.rev
    Others = folded.Others |> List.rev
  }

let private classifyReference (refname: string) =
  if refname.StartsWith("refs/heads/") then
    refname.Substring(11) |> ClassifiedRef.Branch
  elif refname.StartsWith("refs/remotes/") then
    refname.Substring(13) |> ClassifiedRef.Remote
  elif refname.StartsWith("refs/tags/") then
    refname.Substring(10) |> ClassifiedRef.Tag
  else
    refname |> ClassifiedRef.Other

type private CommitData = {
  RepoLabel: string
  Commits: Commit array
  CmtMap: CommitMap
  RefMap: CommitReferenceMap
}

let private userNameAnon = Anonymizer.anonymizer true 3

let private emailNameAnon = Anonymizer.anonymizer false 8

let private userSignatureAnon (signature: Signature) =
  $"{signature.Name |> userNameAnon}-{signature.Email |> emailNameAnon}"

let private getUser o (signature:  Signature) : string option =
  if signature = null then
    None
  else
    match o.UserStyle with
    | UserText.NoUser -> None
    | UserText.UserOnly -> signature.Name |> Some
    | UserText.EmailOnly -> signature.Email |> Some
    | UserText.UserAndEmail -> $"{signature.Name} <{signature.Email}>" |> Some
    | UserText.UserHash -> signature |> userSignatureAnon |> Some

let private runCommitsGraph o commitData =
  let commits = commitData.Commits
  let commitmap = commitData.CmtMap
  let refmap = commitData.RefMap
  let fileName = commitData.RepoLabel + ".graph.json"
  let commitId commit = commit |> commitTag o
  let getUser (signature: Signature) = signature |> getUser o
  let commitNode (commit: Commit) =
    let node = new JObject()
    if o.IncludeMessage then
      node.Add("message", commit.MessageShort)
    if o.IncludeSha then
      node.Add("sha", commit.Sha)
    if commit.Committer <> null then
      node.Add("committed", commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K"))
      match commit.Committer |> getUser with
      | Some name -> node.Add("committer", name)
      | None -> ()
    if commit.Author <> null then
      if commit.Committer = null || commit.Committer.When <> commit.Author.When || commit.Committer.Name <> commit.Author.Name then
        node.Add("authored", commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K"))
        match commit.Author |> getUser with
        | Some name -> node.Add("author", name)
        | None -> ()
    let targets = new JObject()
    let keytags = new JObject()
    let externals = new JArray()
    for parent in commit.Parents do
      if parent.Sha |> commitmap.Contains then
        targets.Add(parent |> commitId, new JObject())
      else
        parent |> commitId |> externals.Add
    node.Add("targets", targets)
    let references = commit.Sha |> refmap.ReferencesForCommit
    if references.Count > 0 then
      let refs = new JArray()
      for reference in references do
        reference |> refs.Add
      keytags.Add("labels", refs)
    if externals.Count > 0 then
      keytags.Add("extern", externals)
    if keytags.Count > 0 then
      node.Add("keytags", keytags)
    node
  do
    use w = fileName |> startFile
    let nodes = new JObject()
    for commit in commits do
      let node = commit |> commitNode
      nodes.Add(commit |> commitId, node)
    let graph = new JObject()
    graph.Add("nodes", nodes)
    let json = JsonConvert.SerializeObject(graph, Formatting.Indented)
    json |> w.WriteLine
  fileName |> finishFile

let private runCommitsJson o commitData =
  let commits = commitData.Commits
  let commitmap = commitData.CmtMap
  let refmap = commitData.RefMap
  let fileName = commitData.RepoLabel + ".commits.json"
  let commitId commit = commit |> commitTag o
  let getUser (signature: Signature) = signature |> getUser o
  let commitNode (commit: Commit) =
    let node = new JObject()
    let id = commit |> commitId
    node.Add("key", id)
    if o.IncludeMessage then
      node.Add("message", commit.MessageShort)
    if o.IncludeSha then
      node.Add("sha", commit.Sha)
    if commit.Committer <> null then
      node.Add("committed", commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K"))
      match commit.Committer |> getUser with
      | Some name -> node.Add("committer", name)
      | None -> ()
    if commit.Author <> null then
      if commit.Committer = null || commit.Committer.When <> commit.Author.When || commit.Committer.Name <> commit.Author.Name then
        node.Add("authored", commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K"))
        match commit.Author |> getUser with
        | Some name -> node.Add("author", name)
        | None -> ()
    let targets = new JArray()
    let externals = new JArray()
    for parent in commit.Parents do
      if parent.Sha |> commitmap.Contains then
        targets.Add(parent |> commitId)
      else
        parent |> commitId |> externals.Add
    node.Add("parents", targets)
    let references = commit.Sha |> refmap.ReferencesForCommit
    if references.Count > 0 then
      let refs = new JArray()
      for reference in references do
        reference |> refs.Add
      node.Add("refs", refs)
    if externals.Count > 0 then
      node.Add("prerequisites", externals)
    node
  do
    use w = fileName |> startFile
    let nodes = new JArray()
    for commit in commits do
      let node = commit |> commitNode
      nodes.Add(node)
    let json = JsonConvert.SerializeObject(nodes, Formatting.Indented)
    json |> w.WriteLine
  fileName |> finishFile

let private runCommits o =
  use repo = new GitRepo(o.Witness)
  if o.Label |> String.IsNullOrEmpty |> not then
    repo.Label <- o.Label
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

  let tagConflicts =
    commits
    |> Array.groupBy (commitTag o)
    |> Array.where (fun (tag, commits) -> commits.Length > 1)

  if tagConflicts.Length > 0 then
    // Rather unexpected, but not impossible. For example, git's own repository needs
    // (at the time of writing) a tag length of at least 9
    cp "\frError\fo: Shortened commit ids are too short to be unique\f0."
    cp $"  Use \fg-taglength\f0 (or \fg-tl\f0) to increase the tag length above \fb{o.TagLength}\f0."
    cp $"Conflicts: (\fb{tagConflicts.Length}\f0)"
    for (tag, commits) in tagConflicts do
      cp $"Commit '\fr{tag}\f0' could be any of:"
      for commit in commits do
        let stamp =
          if commit.Committer <> null then
            commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          elif commit.Author <> null then
            commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K")
          else
            "\f0<\fkno timestamp\f0>"
        cp $"  \fy{commit.Sha}\f0 ({stamp})"
    1

  else
  
    let commitMap = commits |> CommitMap.FromCommits
    let commitReferenceMap = new CommitReferenceMap(repo.Repo.Refs)

    let commitData = {
      RepoLabel = repo.Label
      Commits = commits
      CmtMap = commitMap
      RefMap = commitReferenceMap
    }

    let tips = commitMap.TipIds()
    let tails = commitMap.TailIds()
    let inners =
      commitMap.Commits.Keys
      |> Seq.where (fun sha -> sha |> tips.Contains |> not)
      |> Seq.toArray
    cp $"Found \fg{tips.Count}\f0 tips and \fo{tails.Count}\f0 tails and \fb{inners.Length}\f0 in-betweens"

    let commitSide commitId =
      if commitId |> tips.Contains then
        CommitSide.Tip
      elif commitId |> tails.Contains then
        CommitSide.Tail
      else
        CommitSide.Intern

    let toTtc sha =
      let side = sha |> commitSide
      let commit = repo.Repo.Lookup<Commit>(sha)
      {
        Sha = sha
        Side = side
        Stamp = commit.Committer.When
        Stamp2 = commit.Author.When
      }

    let relevantCommits =
      [
        tips |> Seq.map toTtc
        tails |> Seq.map toTtc
        inners |> Seq.map toTtc
      ]
      |> Seq.concat
      |> Seq.sortBy (fun tot -> tot.Stamp)
      |> Seq.toArray
    relevantCommits |> Array.Reverse

    if o.DoShow then
      for tot in relevantCommits do
        let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
        let tag = tot.Sha.Substring(0, o.TagLength)
        let refs = tot.Sha |> commitReferenceMap.ReferencesForCommit |> Seq.sort |> Seq.toArray
        let isInner = tot.Side = CommitSide.Intern
        if (isInner |> not) || refs.Length > 0 then
          // Skip unlabeled internal nodes
          match tot.Side with
          | CommitSide.Tip ->
            cpx $"+ \fg{tag}\f0  {stamp} "
          | CommitSide.Tail ->
            cpx $"- \fo{tag}\f0  {stamp} "
          | CommitSide.Intern ->
            cpx $". \fk{tag}\f0  \fk{stamp}\f0 "
          for r in refs do
            let color, shortname =
              if r.StartsWith("refs/heads/") then
                "\fg", r.Substring(11)
              elif r.StartsWith("refs/remotes/") then
                "\fc", r.Substring(13)
              elif r.StartsWith("refs/tags/") then
                "\fy", ("#" + r.Substring(10))
              else
                "\fr", r
            let color = if isInner then color.ToUpper() else color          
            cpx $" {color}{shortname}\f0"
          cp "."

    if o.DoTips then
      let fileName = repo.Label + ".tips-tails.csv"
      do
        use csv = fileName |> startFile
        csv.WriteLine("kind,commit,stamp,authored,branches,remotes,tags,others")
        for tot in relevantCommits do
          let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
          let authored =
            if tot.Stamp = tot.Stamp2 then
              ""
            else
              tot.Stamp2.ToString("yyyy-MM-dd HH:mm:ss")
          let kind =
            match tot.Side with
            | CommitSide.Tip -> "tip"
            | CommitSide.Tail -> "tail"
            | CommitSide.Intern -> "inner"
          let refs =
            tot.Sha
            |> commitReferenceMap.ReferencesForCommit
            |> Seq.sort
            |> Seq.map classifyReference
            |> Seq.toArray
          if tot.Side <> CommitSide.Intern || refs.Length > 0 then
            // skip unlabeled internal nodes
            let foldedRefs = refs |> foldRefs
            let branches = String.Join(" ", foldedRefs.Branches)
            let remoteBranches = String.Join(" ", foldedRefs.Remotes)
            let tags = String.Join(" ", foldedRefs.Tags)
            let others = String.Join(" ", foldedRefs.Others)
            let tag = tot.Sha.Substring(0, o.TagLength)
            csv.WriteLine($"{kind},{tag},{stamp},{authored},{branches},{remoteBranches},{tags},{others}")
      fileName |> finishFile

    if o.DoEdges then
      let fileName = repo.Label + ".edges.csv"
      let mutable edgecount = 0
      let mutable externcount = 0
      do
        let cmap = commitMap.Commits
        use csv = fileName |> startFile
        csv.WriteLine("child,parent,extern,childstamp,parentstamp,separation,time-edge")
        for child in cmap.Values do
          for parent in child.Parents do
            let external = cmap.ContainsKey(parent.Sha) |> not
            let childStamp = child.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let parentStamp = parent.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let childShort = child |> commitTag o
            let parentShort = parent |> commitTag o
            let separation, commontime, pretime, postime = timeSeparation parent.Committer.When child.Committer.When
            let edgetext = $"{commontime}[{pretime}+{postime}]"
            csv.WriteLine($"{childShort},{parentShort},{external},{childStamp},{parentStamp},{separation},{edgetext}")
            edgecount <- edgecount + 1
            if external then
              externcount <- externcount + 1
        ()
      fileName |> finishFile
      cp $"Found \fb{edgecount}\f0 inter-commit edges, of which \fc{externcount}\f0 are external"

    if o.DoDump then
      let graph = new CommitStubGraph(commits)
      let fileName = repo.Label + ".commits.csv"
      do
        use csv = fileName |> startFile
        csv.WriteLine("commit,stamp,authored,interns,externs,children,sha")
        for commit in commits do
          let ok, stub = commit.Sha |> graph.StubMap.TryGetValue
          if ok then
            let id = commit |> commitTag o
            let commitStamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let authorStamp = commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K")
            let internalParentStubs =
              stub.Parents
              |> Seq.where (fun cs -> cs.Connected)
              |> Seq.toArray
            let externalParentStubs =
              stub.Parents
              |> Seq.where (fun cs -> cs.Connected |> not)
              |> Seq.toArray
            // Note that children are always 'connected', no need to prepare anything
            csv.WriteLine($"{id},{commitStamp},{authorStamp},{internalParentStubs.Length},{externalParentStubs.Length},{stub.Children.Count},{commit.Sha}")
          else
            cp $"\foCommit \fr{commit.Sha}\fo not found in graph\f0."
      fileName |> finishFile

    if o.DoGraph then
      commitData |> runCommitsGraph o

    if o.DoJson then
      commitData |> runCommitsJson o

    0

let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    Usage.usage "commits"
    1
  | Some o ->
    o |> runCommits

