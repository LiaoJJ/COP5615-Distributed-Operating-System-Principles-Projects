#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.FSharp

let args : string array = fsi.CommandLineArgs |> Array.tail


let system = System.create "processor" <| Configuration.load ()
let stopWatch = System.Diagnostics.Stopwatch.StartNew()
// N is the number of total actor
let  N= args.[0] |> int
let stop_count=5
// Topologies
// topo= "full", topo= "2D", topo= "line", topo= "imp2D" 
let topo= args.[1]

let sqrtN= Convert.ToInt32( round (sqrt (float N)))
let new_N_small=(sqrtN)*(sqrtN)
let new_N_large=(sqrtN+1)*(sqrtN+1)
let mutable new_N =(sqrtN)*(sqrtN)
let n=Convert.ToInt32( round (sqrt (float new_N)))
// upper floor to turn up N into next square number
if N <= new_N_small
    then 
        new_N<-new_N_small
        //printfn "Your N is:  %i, N sqrt is: %i, turned up N is: %i" N sqrtN new_N
    else
        new_N<-new_N_large

let  mutable array_terminate = [| for i in 1.. N -> 0 |]
let mutable array_terminate_sum = array_terminate|> Array.sum

let  mutable array_terminate_new_N = [| for i in 1.. new_N -> 0 |]
let mutable array_terminate_sum_new_N = array_terminate_new_N|> Array.sum

//type ProcessorMessage = ProcessJob of string * int * string
//"Rumor" * target num * "topo type" = string * int * string
type ProcessorMessage = ProcessJob of  string * int * string
let rand = System.Random()

// processor1 is for "Full_Network"
let processor1 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count) = actor {
        array_terminate_sum <- array_terminate|> Array.sum
        //let array_terminate_sum = array_terminate|> Array.sum
        if array_terminate_sum >=N-1
            then
                stopWatch.Stop()
                else
                    let! message = mailbox.Receive ()
                    let sender = mailbox.Sender()
                    let sender_path = mailbox.Sender().Path.ToStringWithAddress()
                    match message  with
                    |   ProcessJob(x,num,topo) ->
                        //num is current actor's id in list 
                        printfn "Received gossip from（%s）. I am actor (%i) My current count: %i count" sender_path num (count+1)
                        // find a rand_num that is not current actor and it's not terminated
                        let mutable rand_num = Random( ).Next() % N
                        while array_terminate.[rand_num]=1  do
                              rand_num <- Random( ).Next() % N
                              // make sure the target is not terminated
                              while rand_num = num  do
                                rand_num <- Random( ).Next() % N

                        if (count+1) >= stop_count 
                            then  
                                // once terminated, set the flag to 1
                                array_terminate.[num]<-1
                                printfn "The actor (%i) was Ternimated" num 
                                //return! loop(count+1)
                                if array_terminate_sum <N
                                    then
                                        printfn "Still have actor not Ternimated, redirecting gossip to： (%i)"  rand_num
                                        let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                        gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                            else 
                                printfn "Telling gossip to （%i）" (rand_num)
                                // spread and may need respond since it may be closed
                                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                                //return! loop(count+1)
                    //| _ ->  failwith "unknown message"
                    return! loop(count+1)     
    }
    loop 0

// processor2 is for "2D_Grid"
let processor2 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count) = actor {
        array_terminate_sum_new_N  <- array_terminate_new_N |> Array.sum
        //let array_terminate_sum = array_terminate|> Array.sum
        if array_terminate_sum_new_N  >=new_N-1
            then
                stopWatch.Stop()
                else
                    let! message = mailbox.Receive ()
                    let sender = mailbox.Sender()
                    let sender_path = mailbox.Sender().Path.ToStringWithAddress()
                    match message  with
                    |   ProcessJob(x,num,topo) ->
                        //num is current actor's id in list 
                      ////printfn "Received gossip from（%s）. I am actor (%i) My current count: %i count" sender_path num (count+1)
                        // find a rand_num that is not current actor and it's not terminated
                        let mutable direction=Random( ).Next() % 4
                        let mutable rand_num = num+1
                        
                        direction<-Random( ).Next() % 4 
                        if rand_num%n=1 then direction<-1
                        elif rand_num%n=n then  direction<-3

                        if rand_num <=n then direction<-2
                        elif rand_num >= new_N-n+1 then direction<-0

                        if direction=0 then rand_num<- num-n
                        if direction=1 then rand_num<- num+1
                        if direction=2 then rand_num<- num+n
                        if direction=3 then rand_num<- num-1
  
                        if (count+1) >= stop_count 
                            then  
                                // once terminated, set the flag to 1
                                array_terminate_new_N .[num]<-1
                             ////printfn "The actor (%i) was Ternimated" num 
                                //return! loop(count+1)
                                if array_terminate_sum_new_N <new_N
                                    then
                                    ////printfn "Still have actor not Ternimated, redirecting gossip to： (%i)"  rand_num
                                        let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                        gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                            else 
                            ////printfn "Telling gossip to （%i）" (rand_num)
                                // spread and may need respond since it may be closed
                                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                                //return! loop(count+1)
                    //| _ ->  failwith "unknown message"
                    return! loop(count+1)     
    }
    loop 0

// processor3 is for "Imperfect 2D Grid:"
let processor3 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count) = actor {
        array_terminate_sum_new_N  <- array_terminate_new_N |> Array.sum
        //let array_terminate_sum = array_terminate|> Array.sum
        if array_terminate_sum_new_N  >=new_N-1
            then
                stopWatch.Stop()
                else
                    let! message = mailbox.Receive ()
                    let sender = mailbox.Sender()
                    let sender_path = mailbox.Sender().Path.ToStringWithAddress()
                    match message  with
                    |   ProcessJob(x,num,topo) ->
                        //num is current actor's id in list 
                      ////printfn "Received gossip from（%s）. I am actor (%i) My current count: %i count" sender_path num (count+1)
                        // find a rand_num that is not current actor and it's not terminated
                        let mutable direction=Random( ).Next() % 4
                        let mutable rand_num = num+1
                        
                        direction<-Random( ).Next() % 4 
                        if rand_num%n=1 then direction<-1
                        elif rand_num%n=n then  direction<-3

                        if rand_num <=n then direction<-2
                        elif rand_num >= new_N-n+1 then direction<-0

                        if direction=0 then rand_num<- num-n
                        if direction=1 then rand_num<- num+1
                        if direction=2 then rand_num<- num+n
                        if direction=3 then rand_num<- num-1

                        direction<-Random( ).Next() % 5 
                        // 1 extra random
                        if direction=4 then rand_num<-Random( ).Next() % new_N 
  
                        if (count+1) >= stop_count 
                            then  
                                // once terminated, set the flag to 1
                                array_terminate_new_N .[num]<-1
                             ////printfn "The actor (%i) was Ternimated" num 
                                //return! loop(count+1)
                                if array_terminate_sum_new_N <new_N
                                    then
                                    ////printfn "Still have actor not Ternimated, redirecting gossip to： (%i)"  rand_num
                                        let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                        gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                            else 
                            ////printfn "Telling gossip to （%i）" (rand_num)
                                // spread and may need respond since it may be closed
                                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                                //return! loop(count+1)
                    //| _ ->  failwith "unknown message"
                    return! loop(count+1)     
    }
    loop 0

// processor4 is for "line"
let processor4 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count) = actor {
        array_terminate_sum <- array_terminate|> Array.sum
        //let array_terminate_sum = array_terminate|> Array.sum
        if array_terminate_sum >= N-1
            then
                stopWatch.Stop()
                else
                    let! message = mailbox.Receive ()
                    let sender = mailbox.Sender()
                    let sender_path = mailbox.Sender().Path.ToStringWithAddress()
                    match message  with
                    |   ProcessJob(x,num,topo) ->
                        //num is current actor's id in list 
                        printfn "Received gossip from（%s）. I am actor (%i) My current count: %i count" sender_path num (count+1)
                        // find a rand_num that is not current actor and it's not terminated
                        //let mutable rand_num = Random( ).Next() % N
                        
                        let mutable direction=0
                        let mutable rand_num = num
                        direction<-Random( ).Next() % 2 
                        if direction=1 then rand_num<- num+1
                        if direction=0 then rand_num<- num-1
                        
                        
                        if array_terminate_sum>=N-sqrtN then
                         //   while array_terminate.[rand_num-1]=1 do
                            printfn "Close to termination, start locating..."
                            rand_num<-Random( ).Next() % N   
                            
                        if rand_num=1 then rand_num<-num+1
                        if rand_num=N then rand_num<-num-1
                            

                        if (count+1) >= stop_count 
                            then  
                                // once terminated, set the flag to 1
                                array_terminate.[num]<-1
                                printfn "The actor (%i) was Ternimated" num 
                                //return! loop(count+1)
                                if array_terminate_sum <N
                                    then
                                        printfn "Still have actor not Ternimated, redirecting gossip to： (%i)"  rand_num
                                        let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                        gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                            else 
                                printfn "Telling gossip to （%i）" (rand_num)
                                // spread and may need respond since it may be closed
                                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                                gossip_spread <? ProcessJob( "Roumor",rand_num,topo)|>ignore
                                //return! loop(count+1)
                    //| _ ->  failwith "unknown message"
                    return! loop(count+1)     
    }
    loop 0

//let actorArray = Array.create N (spawn system "processor" processor)
//{0..N-1} |> Seq.iter (fun a ->
//    actorArray.[a] <- spawn system (string a) processor
//)
//{0..5} |> Seq.iter(fun a ->
 //   actorArray.[a] <! ProcessJob( "Roumor")
  //  ()
//)

    
if topo="full" 
    then
        let actorArray = Array.create N (spawn system "processor1" processor1)
        {0..N-1} |> Seq.iter (fun a ->
            actorArray.[a] <- spawn system (string a) processor1
        )
        actorArray.[1] <? ProcessJob("Roumor",2,topo)|>ignore
        let System_wait=0
        //let mutable array_terminate_sum = array_terminate|> Array.sum
        //let mutable sum=0
        // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
        while array_terminate_sum < N-1 do
            //printfn "%A" array_terminate
            //printfn "Current terminated number: %i" array_terminate_sum
            System_wait|>ignore
        //array_terminate_sum <- array_terminate|> Array.sum
        printfn "-----------------------------------------------------------------------------\n" 
        printf "Only one actor alive so it can't gossip to itself. \n"
        printf "No more spreading available. Algorithm terminating... \n" 
        printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
        printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds


if topo= "2D"
    then
        printfn "Your N is:  %i, turned up N is: %i" N  new_N
        //printf "For 2D topo, the N you enter will turn up into next square"
        let actorArray = Array.create new_N (spawn system "processor2" processor2)
        {0..new_N-1} |> Seq.iter (fun a ->
            actorArray.[a] <- spawn system (string a) processor2
        )
        actorArray.[1] <? ProcessJob("Roumor",2,topo)|>ignore
        let System_wait=0
        while array_terminate_sum_new_N < new_N-2 do
            //printfn "%A" array_terminate
            //printfn "Current terminated number: %i" array_terminate_sum
            printf "Current terminated: %i \n"  array_terminate_sum_new_N
            System_wait|>ignore
        //array_terminate_sum <- array_terminate|> Array.sum
        printfn "-----------------------------------------------------------------------------\n" 
        //printf "Current terminated: %i \n"  array_terminate_sum_new_N
        printf "Only two actors alive which will cost too much time to find random path. \n"
        printf "No more spreading available. Algorithm terminating... \n" 
        printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum_new_N) new_N
        printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds

if topo= "imp2D"
then
    printfn "Your N is:  %i, turned up N is: %i" N  new_N
    //printf "For 2D topo, the N you enter will turn up into next square"
    let actorArray = Array.create new_N (spawn system "processor3" processor3)
    {0..new_N-1} |> Seq.iter (fun a ->
        actorArray.[a] <- spawn system (string a) processor3
    )
    actorArray.[1] <? ProcessJob("Roumor",2,topo)|>ignore
    let System_wait=0
    while array_terminate_sum_new_N < new_N-2 do
        //printfn "%A" array_terminate
        //printfn "Current terminated number: %i" array_terminate_sum
        printf "Current terminated: %i \n"  array_terminate_sum_new_N
        System_wait|>ignore
    //array_terminate_sum <- array_terminate|> Array.sum
    printfn "-----------------------------------------------------------------------------\n" 
    //printf "Current terminated: %i \n"  array_terminate_sum_new_N
    printf "Only two actors alive which will cost too much time to find random path. \n"
    printf "No more spreading available. Algorithm terminating... \n" 
    printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum_new_N) new_N
    printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds
        

if topo="line" then
    let actorArray = Array.create N (spawn system "processor4" processor4)
    {0..N-1} |> Seq.iter (fun a ->
        actorArray.[a] <- spawn system (string a) processor4
    )
    actorArray.[1] <? ProcessJob("Roumor",2,topo)|>ignore
    let System_wait=0
    //let mutable array_terminate_sum = array_terminate|> Array.sum
    //let mutable sum=0
    // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
    while array_terminate_sum < N-1 do
        printfn "%A" array_terminate
        printfn "Current terminated number: %i" array_terminate_sum
        System_wait|>ignore
    //array_terminate_sum <- array_terminate|> Array.sum
    printfn "-----------------------------------------------------------------------------\n" 
    printf "Only two actors alive which will cost too much time to find random path. \n"
    //printf "Only one actor alive so it can't gossip to itself. \n"
    printf "No more spreading available. Algorithm terminating... \n" 
    printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
    printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds
   
             
   


    

    

