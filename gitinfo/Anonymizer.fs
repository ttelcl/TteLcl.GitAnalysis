module Anonymizer

open System
open System.Text

type Dictionary<'k,'v> = System.Collections.Generic.Dictionary<'k,'v>

let memoize f =
    let dict = Dictionary<_, _>()
    fun c ->
        let exist, value = dict.TryGetValue c
        match exist with
        | true -> value
        | _ -> 
            let value = f c
            dict.Add(c, value)
            value

/// <summary>
/// Returns a hash-based letter sequence derived from the given text.
/// </summary>
/// <param name="uppercase">If true, the returned string is all uppercase. If false it is all lowercase.</param>
/// <param name="letterCount">The number of characters in the returned string (range 1-52)</param>
/// <param name="text">The input text to hash</param>
let letterHash uppercase letterCount (text: string) =
  if letterCount < 1 || letterCount > 52 then
    invalidArg (nameof letterCount) "Letter count must be between 1 and 52"
  let baseLetter = if uppercase then 'A' else 'a'
  let utf8 = Encoding.UTF8.GetBytes(text)
  let hash = System.Security.Cryptography.SHA256.HashData(utf8)
  // split into 4 UInt64 values (each sourcing 13 letters; there is information for 13.616 letters in each)
  let ulongs = [|
    BitConverter.ToUInt64(hash, 0)
    BitConverter.ToUInt64(hash, 8)
    BitConverter.ToUInt64(hash, 16)
    BitConverter.ToUInt64(hash, 24)
  |]
  let u64letters (u64: UInt64) =
    Seq.unfold (fun u -> Some (int(u % 26UL), u / 26UL)) u64
    |> Seq.map (fun i -> char(int(baseLetter) + i))
    |> Seq.take 13
  let letterSeq =
    ulongs
    |> Seq.map u64letters
    |> Seq.concat
  let sb =
    letterSeq 
    |> Seq.take letterCount
    |> Seq.fold (fun (sb: StringBuilder) l -> sb.Append(l)) (new StringBuilder())
  sb.ToString()

/// <summary>
/// Returns a memoized funtion that creates a hash-based letter sequence derived from its argument text
/// </summary>
/// <param name="uppercase">If true, the returned string is all uppercase. If false it is all lowercase.</param>
/// <param name="letterCount">The number of characters in the returned string (range 1-52)</param>
let anonymizer uppercase letterCount =
  memoize (letterHash uppercase letterCount)
