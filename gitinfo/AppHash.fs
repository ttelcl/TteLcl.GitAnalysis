module AppHash

open System
open System.IO
open System.Text

open TteLcl.GitModel

open ColorPrint
open CommonTools

type private ContentSource =
  | BinaryFile of string
  | TextFile of string
  | Literal of string
  | Line of string

type private Options = {
  Content: ContentSource option
  ContentType: string
}

let private convertLineBreaks (source:byte[]) =
  let mutable iin = 0
  let mutable iout = 0
  while iin < source.Length do
    if iin < source.Length - 1 && source[iin] = 13uy (* CR *) && source[iin+1] = 10uy (* LF *) then
      // do not copy and do not increment iout
      iin <- iin + 1
    else
      source[iout] <- source[iin]
      iout <- iout + 1
      iin <- iin + 1
  source |> Array.truncate iout

let private runHash o =
  let blob =
    match o.Content with
    | None ->
      // Should have been handled more gracefully before
      failwith "No content specified"
    | Some(BinaryFile(file)) ->
      file |> System.IO.File.ReadAllBytes
    | Some(TextFile(file)) ->
      file |> System.IO.File.ReadAllBytes |> convertLineBreaks
    | Some(Literal(text)) ->
      text |> Encoding.UTF8.GetBytes
    | Some(Line(text)) ->
      (text + "\n") |> Encoding.UTF8.GetBytes
  let idCache = new GitIdCache()
  let gitId = idCache.ForContent(o.ContentType, blob)
  cp $"Size in bytes:  \fb{blob.Length}\f0."
  cp $"Full hash:      \fg{gitId.FullString}\f0."
  cp $"Shortened hash: \fy{gitId.Id}\f0."
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
    | "-fb" :: file :: rest ->
      rest |> parseMore {o with Content = file |> BinaryFile |> Some}
    | "-ft" :: file :: rest ->
      rest |> parseMore {o with Content = file |> TextFile |> Some}
    | "-s" :: text :: rest ->
      rest |> parseMore {o with Content = text |> Literal |> Some}
    | "-l" :: line :: rest ->
      rest |> parseMore {o with Content = line |> Line |> Some}
    | "-t" :: tpe :: rest ->
      rest |> parseMore {o with ContentType = tpe}
    | [] ->
      if o.Content.IsNone then
        cp "\frNo content specified \f0(\fg-fb\f0, \fg-ft\f0 or \fg-s\f0)"
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    Content = None
    ContentType = "blob"
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "info"
    1
  | Some o ->
    o |> runHash
