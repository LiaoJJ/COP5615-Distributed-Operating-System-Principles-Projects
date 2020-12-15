open System
#time
let square  = fun (x:int64)->x*x

let isSquare n = (square (int64(sqrt (double n)))) = n

let isSquareByK (x:int64) (k:int64) =
//    printfn "%A %A %A" x k ([x..x+k-1L] |> List.map(fun x -> square x) |> List.sum)
    [x..x+k-1L] |> List.map(fun x -> square x) |> List.sum |> isSquare

let actor left right k =
    [left..right] |> List.filter(fun x->(isSquareByK x k)) |> List.collect(fun x->[x])
//    List.filter isSquare [left..right] |> List.collect (fun x->[x])


let asyncActor left right k = async {return (actor left right k)}
    





let args : string array = fsi.CommandLineArgs |> Array.tail
let first = args.[0]
let second = args.[1]
let num = 1000L
let left = 1L
let right = first |> int64

let kk = second |> int64
let step = (right / num)
//printfn "the size of Task is: %d" step

let min x y =
    if x<y then x else y
    
    
let getRange = fun (i:int64) -> [left+(i-1L)*step;left+i*step-1L]

let listLast =  [(left+(num-1L)*step);right]

let bossList = [1L..num-1L] |> List.map (fun x-> (getRange x)) |> List.append [listLast]

//printfn "begin Async Task"

let bossFunc() = 
    bossList
        |> List.map(fun x-> asyncActor x.[0] x.[1] kk)
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.filter (fun x->(x.Length > 0))
        |> Array.toList
        |> List.map (fun x->x)
        |> List.collect(fun x-> x)
let list = bossFunc()
list |> List.map(fun x->(int)x) |> List.sort |> Seq.iter (fun x -> printf "%d " x)

printfn "\n "
#time

//printfn "end Async Task"



