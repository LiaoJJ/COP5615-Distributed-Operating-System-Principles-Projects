module project3.Program

open System
open Akka.Actor
open Akka.FSharp
open System.Threading
open System.Security.Cryptography
open System.Text

let system = System.create "system" (Configuration.defaultConfig())
let stopWatch = System.Diagnostics.Stopwatch.StartNew()

let N = 1000
let numRequests = 5
let K = 1000000000 / N

let mutable global_hops = 0
let mutable global_max_hops = 0
let mutable global_min_hops = 10000
let mutable global_count = 0

let rand = System.Random()
let monitor = new Object()
let update_global_hops hops =
    Monitor.Enter monitor
    global_hops <- global_hops + hops
    global_max_hops <- max global_max_hops hops
    global_min_hops <- min global_min_hops hops
    global_count <- global_count + 1
    Monitor.Exit monitor
    0

let md5 (data : byte array) : string =
    use md5 = MD5.Create()
    (StringBuilder(), md5.ComputeHash(data))
    ||> Array.fold (fun sb b -> sb.Append(b.ToString("d")))
    |> string

let int2String (x: int) = string x
let string2int (x: string) = int x
let hash (str:string) =
    let bytes = System.Text.Encoding.ASCII.GetBytes str
    let temp = md5 bytes
    temp.Substring(0, 9)

type Trie(c:Option<char>, words:string seq) =
    let childMap = words
                   |> Seq.filter(fun w -> w.Length > 0)
                   |> Seq.groupBy(fun word -> word.[0])
                   |> Seq.map(fun (ch, w) -> 
                       (ch, new Trie(
                           Option.Some(ch), 
                           w |> Seq.map (fun word -> word.Substring(1)))))
                   |> Map.ofSeq

    new(words:string seq) = Trie(Option.None, words)
    member this.value = c
    member this.eow = this.value.IsSome && words |> Seq.exists (fun word -> word.Length = 0)
    member this.children = childMap

    // follows a given prefix down the tree and returns the node at the end
    member private this.getPrefixTrie prefix =
        let rec getTrie (curr:Trie, currVal:string) =
            if currVal.Length = 0 then Option.Some(curr)
            else if not(curr.children.ContainsKey(currVal.[0])) then Option.None
            else getTrie(curr.children.Item(currVal.[0]), currVal.Substring(1))
    
        getTrie(this, prefix)

    member this.withWord word =
        if this.value.IsSome then invalidOp "Cannot add a word to a non-root Trie node"
        else
            Trie(Option.None, word :: List.ofSeq words)

    member this.getWords() =
        let rec getWordsInternal(trie:Trie, substring) : string seq =
            seq {
                if trie.children.Count = 0 then yield substring
                else
                    if (trie.eow) then yield substring
                    yield! trie.children |> Map.toSeq |> Seq.collect(fun (c,t) -> getWordsInternal(t, substring+c.ToString()))
            }

        getWordsInternal(this, "")

    member this.isPrefix(prefix:string) =
        let endTrie = this.getPrefixTrie(prefix)
        Option.isSome(endTrie)
    
    member this.getWordsPrefixedBy(value:string) =
        let t = this.getPrefixTrie(value)
        match t with
        | Option.None -> Seq.empty
        | _ -> t.Value.getWords() |> Seq.map (fun w -> value + w)

    member this.contains(word:string) =
        let endTrie = this.getPrefixTrie(word);
        let result = endTrie.IsSome && endTrie.Value.eow
        result




let shl (str1 : string) (str2 : string) =
    let mutable res = 0
    let mutable flag = false
    for i in 0..(str1.Length-1) do
        if not flag && str1.[i] = str2.[i] then
                res <- res + 1
        else
            flag <- true
    res
        

let actorOfStatefulSink (myAddr : string, trie: Trie) (mailbox : Actor<'a>) =
  let rec loop (myAddr : string, trie: Trie) =
    actor {
      if global_count = numRequests*N then
          stopWatch.Stop()
          printfn "Node %s has Stopped" myAddr
      else
          let! msg = mailbox.Receive()
          let (dest : string, hops) = msg
          Monitor.Enter monitor
          printfn "curNode: %s, destNode: %s,  curHops: %i" myAddr dest hops
          Monitor.Exit monitor
          let same = shl myAddr dest
          if same = dest.Length then
              // arrive destination
              printfn "Arrive destination"
              update_global_hops hops+1 |> ignore
          else
              let prefix_plus = dest.Substring(0, same + 1)
              if trie.isPrefix(prefix_plus) then
                  // transmit to next node
                  let seq_sub = trie.getWordsPrefixedBy(prefix_plus)
                  let next_addr = seq_sub |> Seq.head 
                  let next_actor = system.ActorSelection("akka://system/user/" + next_addr)
                  next_actor <! (dest, hops+1)
              else
                  // there is no more next node, send to the cloest Node
                  let prefix = dest.Substring(0, same)
                  let seq_sub = trie.getWordsPrefixedBy(prefix)
                  let mutable min = 1000000000
                  let mutable cand = seq_sub |> Seq.head |> int
                  let dest_int = dest |> int
//                  printfn "%A" cand
                  let close x:string=
//                      printfn "%A" x
                      let num = string2int x
                      if abs (num - dest_int) < min then
                          min <- abs (num - dest_int)
                          cand <- num
                      x
                  seq_sub |> Seq.iter(fun x -> close x |> ignore)
                  let next_addr = sprintf "%09i" cand
                  if next_addr = myAddr then
                      // arrive destination
                      printfn "Arrive destination"
                      update_global_hops hops+1 |> ignore
                  else
                      // send to the next one
                      let next_actor = system.ActorSelection("akka://system/user/" + next_addr)
                      next_actor <! (dest, hops+1)
          return! loop (myAddr, trie)
    }
  loop (myAddr, trie)



[<EntryPoint>]
let main argv =
    
    let createActor (myAddr, trie) =
        actorOfStatefulSink (myAddr, trie)
        |> spawn system myAddr
        |> ignore
      
    
    let seq1 = seq {0 .. N-1}
    let seq2 = seq1 |> Seq.map (fun x -> x*K) |> Seq.map( fun x -> sprintf "%09i" x)
    
    let trie = Trie(seq2);
    printfn "=======Init========"
    printfn "Node names"
    seq2 |> Seq.iter( fun x -> printf "%s " x) |> ignore
    printfn " "
    seq2 |> Seq.iter( fun x -> createActor (x, trie)) |> ignore
    
    printfn "=======Send Request========"
    let getRandDest _ =
        let randInt = rand.Next()
        let randDest = hash (int2String randInt)
        randDest
    let route actor_addr =
        let next = system.ActorSelection("akka://system/user/" + actor_addr)
        next <! (getRandDest(), 0)
    for i in 1..numRequests do
        seq2 |> Seq.iter( fun x -> route x) |> ignore
        Thread.Sleep(1000)
    
    let System_wait=0
    while global_count < numRequests*N do
        System_wait |> ignore
        printfn "global_counts is %i nodes" global_count
        Thread.Sleep(100)
    printfn "=======Result========"
    printfn "numNodes is %i nodes" N
    printfn "numRequests is %i requests" numRequests
    printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds
    printfn "max hop is %i hops" global_max_hops
    printfn "min hop is %i hops" global_min_hops
    printfn "avg hop is %f hops" ((global_hops |> float) / (global_count |> float))

//    system.WhenTerminated.Wait ()
    0 // return an integer exit code
