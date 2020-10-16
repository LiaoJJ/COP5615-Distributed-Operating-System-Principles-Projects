## step 1
add all the package on the top of file. If there is a version, it will be better.
```
#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
```


## step2
delete `[<EntryPoint>]`, delete the return value, delete indentation

## step3
support the terminal input
```
let args : string array = fsi.CommandLineArgs |> Array.tail
let  N= args.[0] |> int
```

## run the with command
```
dotnet fsi --langversion:preview proj2.fsx 10 line gossip
```