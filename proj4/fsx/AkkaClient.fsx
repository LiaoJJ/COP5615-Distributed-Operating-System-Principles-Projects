#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 

open System
open System.Threading
open Akka.Actor
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
type MessagePack_processor = MessagePack8 of  string  * string * string* string* string * string* string* string * string

// number of user
let args : string array = fsi.CommandLineArgs |> Array.tail
let N= args.[0] |> int
let M = N
let mutable i = 0
let mutable ii = 0
let obj = new Object()
let addIIByOne() =
    Monitor.Enter obj
    ii<- ii+1
    Monitor.Exit obj
    
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8123
                    hostname = localhost
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)

let echoServer = system.ActorSelection(
                            "akka.tcp://RemoteFSharp@localhost:8777/user/EchoServer")

let rand = System.Random(1)
let actor_user_register (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        let idx = message
        let mutable opt = "reg"           
        let mutable POST = " "
        let mutable username = "user"+(string idx)
        let mutable password = "password" + (string idx)
        let mutable target_username = " "
        let mutable queryhashtag = " "
        let mutable at = " "
        let mutable tweet_content = " "
        let mutable register = " "
        let cmd = opt+","+POST+","+username+","+password+","+target_username+","+tweet_content+","+queryhashtag+","+at+","+register
        let task = echoServer <? cmd
        let response = Async.RunSynchronously (task, 1000)
        printfn "[command]%s" cmd
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
        addIIByOne()
        return! loop()
    }
    loop ()
let actor_client_simulator (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        let idx = message
        match box message with
        | :? string   ->
            let mutable rand_num = Random( ).Next() % 7
            let mutable opt = "reg"           
            let mutable POST = "POST"
            let mutable username = "user"+(string idx)
            let mutable password = "password" + (string idx)
            let mutable target_username = "user"+rand.Next(N) .ToString()
            let mutable queryhashtag = "#user"+rand.Next(N) .ToString()
            let mutable at = "@user"+rand.Next(N) .ToString()
            let mutable tweet_content = "tweet"+rand.Next(N) .ToString()+"... " + queryhashtag + "..." + at + " " 
            let mutable register = "register"
            if rand_num=0 then  opt <-"reg"
            if rand_num=1 then  opt <-"send"
            if rand_num=2 then  opt <-"subscribe"
            if rand_num=3 then  opt <-"retweet"
            if rand_num=4 then  opt <-"querying"
            if rand_num=5 then  opt <-"#"
            if rand_num=6 then  opt <-"@" 
            // msg can be anything like "start"
            let cmd = opt+","+POST+","+username+","+password+","+target_username+","+tweet_content+","+queryhashtag+","+at+","+register
            let task = echoServer <? cmd
            let response = Async.RunSynchronously (task, 3000)
            printfn "[command]%s" cmd
            printfn "[Reply]%s" (string(response))
            printfn "%s" ""
            addIIByOne()
        return! loop()     
    }
    loop ()

let client_user_register = spawn system "client_user_register" actor_user_register    
let client_simulator = spawn system "client_simulator" actor_client_simulator



printfn "------------------------------------------------- \n " 
printfn "-------------------------------------------------   " 
printfn "Register Account...   " 
printfn "-------------------------------------------------   "

let stopWatch = System.Diagnostics.Stopwatch.StartNew()
i<-0
ii<-0
while i<N do
    client_user_register <! string i |>ignore
    i<-i+1
while ii<N-1 do
    Thread.Sleep(50)
stopWatch.Stop()
printfn "The time of register %d users is %f" N stopWatch.Elapsed.TotalMilliseconds
Thread.Sleep(5000)

printfn "------------------------------------------------- \n " 
printfn "-------------------------------------------------   " 
printfn "Zipf Subscribe and send tweet...   " 
printfn "-------------------------------------------------   "
stopWatch = System.Diagnostics.Stopwatch.StartNew()
let mutable step = 1
for i in 0..N-1 do
    for j in 0..10 do
        let cmd = "send, ,user"+(string i)+",password"+(string i)+", ,tweet+user"+(string i)+"_"+(string j)+"th , , , "
        let task = echoServer <? cmd
        let response = Async.RunSynchronously (task, 3000)
        printfn "[command]%s" cmd
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
for i in 0..N-1 do
    for j in 0..step..N-1 do
        if not (j=i) then
            let cmd = "subscribe, ,user"+(string j)+",password"+(string j)+",user"+(string i)+", , , , "
            let task = echoServer <? cmd
            let response = Async.RunSynchronously (task, 3000)
            printfn "[command]%s" cmd
            printfn "[Reply]%s" (string(response))
            printfn "%s" ""
            
    let cmd = "querying, ,user"+(string i)+",password"+(string i)+", , , , , "
    let task = echoServer <? cmd
    let response = Async.RunSynchronously (task, 3000)
    printfn "[command]%s" cmd
    printfn "[Reply]%s" (string(response))
    printfn "%s" ""
    step <- step+1
stopWatch.Stop()
printfn "The time of Zipf subscribe and send tweets %d users is %f" N stopWatch.Elapsed.TotalMilliseconds
Thread.Sleep(5000)

printfn "------------------------------------------------- \n " 
printfn "-------------------------------------------------   " 
printfn " %d Randon Ops and send tweet...   " M 
printfn "-------------------------------------------------   "
stopWatch = System.Diagnostics.Stopwatch.StartNew()
i<-0
ii<-0
while i<M do
    client_simulator<! string (rand.Next(N)) |>ignore
    i <- i+1
while ii<M-1 do
    Thread.Sleep(50)
stopWatch.Stop()
printfn "The time of %d random operations is %f" M stopWatch.Elapsed.TotalMilliseconds

system.Terminate() |> ignore
0 // return an integer exit code

