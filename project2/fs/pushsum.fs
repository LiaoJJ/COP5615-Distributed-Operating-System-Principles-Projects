open System
open Akka.Actor
open Akka.FSharp
// PUSH SUM for 4 kinds of topo
// Parameter: 
// N: Total actor number; 
//Topologies: topo= "full", topo= "2D", topo= "line", topo= "imp2D" 

let system = System.create "processor" <| Configuration.load ()
let stopWatch = System.Diagnostics.Stopwatch.StartNew()
// N is the number of total actor
// Topologies
// topo= "full", topo= "2D", topo= "line", topo= "imp2D" 
let N=10
let topo= "full"

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
let  mutable array_terminate_count = [| for i in 1.. N -> 0 |]
let mutable array_terminate_sum = array_terminate|> Array.sum

// initiating array s, w and r=s/w   Ratio
let  mutable array_s = [| for i in 1.. N ->(double i) |]
let  mutable array_w = [| for i in 1.. N -> (double 1) |]
let  mutable array_r = [| for i in 1.. N -> (double i) |]


let  mutable array_terminate_new_N = [| for i in 1.. new_N -> 0 |] // 0-1
let  mutable array_terminate_count_new_N = [| for i in 1.. new_N -> 0 |] //0-3
let mutable array_terminate_sum_new_N = array_terminate_new_N|> Array.sum

// initiating array s, w and r=s/w   Ratio
let  mutable array_s_new_N = [| for i in 1.. new_N ->(double i) |]
let  mutable array_w_new_N = [| for i in 1.. new_N -> (double 1) |]
let  mutable array_r_new_N = [| for i in 1.. new_N -> (double i) |]
//type ProcessorMessage = ProcessJob of string * int * string
//"Rumor" * target num * "topo type" = string * int * string
//type ProcessorMessage = ProcessJob of  string * int * string
// actor id, topo, s, w   
type ProcessorMessage = ProcessJob of   int * string * double * double  
let rand = System.Random()

// processor1 is for "Full_Network"
let processor1 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count ) = actor {
        let! message = mailbox.Receive ()
        match message  with
        |   ProcessJob(num,topo,s,w ) ->
            // the (s, w)   are sender's current number in global array
            // add it to receiver's (s, w)
            array_terminate_sum <- array_terminate|> Array.sum
            if array_terminate_sum >= N-1 then stopWatch.Stop()
            // finding next hop
            let mutable rand_num = Random( ).Next() % N
            // updating (s,w)
            array_s.[num]<-array_s.[num]+s
            array_w.[num]<-array_w.[num]+w
            // updating ratio
            let mutable new_r=double(s/w)
            let mutable e= abs(double(new_r-array_r.[num]))
            // stop criteria for error
            if e < 1e-10 then array_terminate_count.[num]<-array_terminate_count.[num]+1
            // stop criteria for actor: 3 times
            if array_terminate_count.[num] >= 3
                then 
                    //flag=1
                    array_terminate.[num]<- 1
                    // maybe need redirecting even itself is terminated
                    array_terminate_sum <- array_terminate|> Array.sum
                    if array_terminate_sum <N 
                        then
                            // redirecting, cut itself half and send another half
                            array_s.[num]<-array_s.[num]/2.0
                            array_w.[num]<-array_w.[num]/2.0
                            array_r.[num]<-array_s.[num]/array_w.[num]
                            printfn "Still have actor not Ternimated, I'm (%i), redirecting to： (%i)" num rand_num
                            let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                            gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore
                            
            else
                // actor is running
                array_s.[num]<-array_s.[num]/2.0
                array_w.[num]<-array_w.[num]/2.0
                array_r.[num]<-array_s.[num]/array_w.[num]
                printfn "I'm (%i), Sending to： (%i)" num rand_num
                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore

            // everything end
        return! loop(count+1)        
    }
    loop 0
        

// processor2 is for "2D_Grid"
let processor2 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count ) = actor {
        let! message = mailbox.Receive ()
        match message  with
        |   ProcessJob(num,topo,s,w ) ->
            // the (s, w)   are sender's current number in global array
            // add it to receiver's (s, w)
            array_terminate_sum <- array_terminate|> Array.sum
            if array_terminate_sum >= N-1 then stopWatch.Stop()
            // finding next hop
            //let mutable rand_num = Random( ).Next() % N
            let mutable direction=Random( ).Next() % 4
            let mutable rand_num = num
            direction<-Random( ).Next() % 4 
            if rand_num%n=1 then direction<-1
            elif rand_num%n=n then  direction<-3

            if rand_num <=n then direction<-2
            elif rand_num >= new_N-n+1 then direction<-0

            if direction=0 then rand_num<- num-n
            if direction=1 then rand_num<- num+1
            if direction=2 then rand_num<- num+n
            if direction=3 then rand_num<- num-1
            
            
            
            if array_terminate_sum>=N-sqrtN then
             //   while array_terminate.[rand_num-1]=1 do
                printfn "Close to termination, start locating..."
                rand_num<-Random( ).Next() % N      
            if rand_num<0 then rand_num<-num+1
            if rand_num>=N then rand_num<-num-n
            //
            // updating (s,w)
            array_s.[num]<-array_s.[num]+s
            array_w.[num]<-array_w.[num]+w
            // updating ratio
            let mutable new_r=double(s/w)
            let mutable e= abs(double(new_r-array_r.[num]))
            // stop criteria for error
            if e < 1e-10 then array_terminate_count.[num]<-array_terminate_count.[num]+1
            // stop criteria for actor: 3 times
            if array_terminate_count.[num] >= 3
                then 
                    //flag=1
                    array_terminate.[num]<- 1
                    // maybe need redirecting even itself is terminated
                    array_terminate_sum <- array_terminate|> Array.sum
                    if array_terminate_sum <N 
                        then
                            // redirecting, cut itself half and send another half
                            array_s.[num]<-array_s.[num]/2.0
                            array_w.[num]<-array_w.[num]/2.0
                            array_r.[num]<-array_s.[num]/array_w.[num]
                            printfn "Still have actor not Ternimated, I'm (%i), redirecting to： (%i)" num rand_num
                            let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                            gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore
                            
            else
                // actor is running
                array_s.[num]<-array_s.[num]/2.0
                array_w.[num]<-array_w.[num]/2.0
                array_r.[num]<-array_s.[num]/array_w.[num]
                printfn "I'm (%i), Sending to： (%i)" num rand_num
                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore

            // everything end
        return! loop(count+1)        
    }
    loop 0
// processor3 is for "Imperfect 2D Grid:"
let processor3 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count ) = actor {
        let! message = mailbox.Receive ()
        match message  with
        |   ProcessJob(num,topo,s,w ) ->
            // the (s, w)   are sender's current number in global array
            // add it to receiver's (s, w)
            array_terminate_sum <- array_terminate|> Array.sum
            if array_terminate_sum >= N-1 then stopWatch.Stop()
            // finding next hop
            //let mutable rand_num = Random( ).Next() % N
            let mutable direction=Random( ).Next() % 4
            let mutable rand_num = num
            direction<-Random( ).Next() % 4 
            if rand_num%n=1 then direction<-1
            elif rand_num%n=n then  direction<-3

            if rand_num <=n then direction<-2
            elif rand_num >= new_N-n+1 then direction<-0

            if direction=0 then rand_num<- num-n
            if direction=1 then rand_num<- num+1
            if direction=2 then rand_num<- num+n
            if direction=3 then rand_num<- num-1
            
            
            
            if array_terminate_sum>=N-sqrtN then
             //   while array_terminate.[rand_num-1]=1 do
                printfn "Close to termination, start locating..."
                rand_num<-Random( ).Next() % N 
            //  4 direction +1 random
            let mutable randomdice=Random( ).Next() % 5
            if randomdice=1 then rand_num<-Random( ).Next() % N
              
            if rand_num<0 then rand_num<-num+1
            if rand_num>=N then rand_num<-num-n
            //
            // updating (s,w)
            array_s.[num]<-array_s.[num]+s
            array_w.[num]<-array_w.[num]+w
            // updating ratio
            let mutable new_r=double(s/w)
            let mutable e= abs(double(new_r-array_r.[num]))
            // stop criteria for error
            if e < 1e-10 then array_terminate_count.[num]<-array_terminate_count.[num]+1
            // stop criteria for actor: 3 times
            if array_terminate_count.[num] >= 3
                then 
                    //flag=1
                    array_terminate.[num]<- 1
                    // maybe need redirecting even itself is terminated
                    array_terminate_sum <- array_terminate|> Array.sum
                    if array_terminate_sum <N 
                        then
                            // redirecting, cut itself half and send another half
                            array_s.[num]<-array_s.[num]/2.0
                            array_w.[num]<-array_w.[num]/2.0
                            array_r.[num]<-array_s.[num]/array_w.[num]
                            printfn "Still have actor not Ternimated, I'm (%i), redirecting to： (%i)" num rand_num
                            let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                            gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore
                            
            else
                // actor is running
                array_s.[num]<-array_s.[num]/2.0
                array_w.[num]<-array_w.[num]/2.0
                array_r.[num]<-array_s.[num]/array_w.[num]
                printfn "I'm (%i), Sending to： (%i)" num rand_num
                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore

            // everything end
        return! loop(count+1)        
    }
    loop 0

// processor4 is for "line"
let processor4 (mailbox: Actor<_>) = 
    //let mutable count = 0
    let rec loop (count ) = actor {
        let! message = mailbox.Receive ()
        match message  with
        |   ProcessJob(num,topo,s,w ) ->
            // the (s, w)   are sender's current number in global array
            // add it to receiver's (s, w)
            array_terminate_sum <- array_terminate|> Array.sum
            if array_terminate_sum >= N-1 then stopWatch.Stop()
            // deciding the next hop
            //let mutable rand_num = Random( ).Next() % N
            let mutable direction=0
            let mutable rand_num = num
            direction<-Random( ).Next() % 2 
            if direction=1 then rand_num<- num+1
            if direction=0 then rand_num<- num-1
            
            
            if array_terminate_sum>=N-sqrtN then
             //   while array_terminate.[rand_num-1]=1 do
                //printfn "Close to termination, start locating..."
                rand_num<-Random( ).Next() % N      
            if rand_num<0 then rand_num<-num+1
            if rand_num>=N then rand_num<-num-1
            // updating (s,w)
            array_s.[num]<-array_s.[num]+s
            array_w.[num]<-array_w.[num]+w
            // updating ratio
            let mutable new_r=double(s/w)
            let mutable e= abs(double(new_r-array_r.[num]))
            // stop criteria for error
            if e < 1e-10 then array_terminate_count.[num]<-array_terminate_count.[num]+1
            // stop criteria for actor: 3 times
            if array_terminate_count.[num] >= 3
                then 
                    //flag=1
                    array_terminate.[num]<- 1
                    // maybe need redirecting even itself is terminated
                    array_terminate_sum <- array_terminate|> Array.sum
                    if array_terminate_sum <N 
                        then
                            // redirecting, cut itself half and send another half
                            array_s.[num]<-array_s.[num]/2.0
                            array_w.[num]<-array_w.[num]/2.0
                            array_r.[num]<-array_s.[num]/array_w.[num]
                            //printfn "Still have actor not Ternimated, I'm (%i), redirecting to： (%i)" num rand_num
                            let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                            gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore
                            
            else
                // actor is running
                array_s.[num]<-array_s.[num]/2.0
                array_w.[num]<-array_w.[num]/2.0
                array_r.[num]<-array_s.[num]/array_w.[num]
                //printfn "I'm (%i), Sending to： (%i)" num rand_num
                let gossip_spread = system.ActorSelection("akka://processor/user/" + string rand_num)
                gossip_spread <? ProcessJob(rand_num,topo,array_s.[num],array_w.[num])|>ignore

            // everything end
        return! loop(count+1)        
    }
    loop 0
 



[<EntryPoint>]
let main argv = 
    
    if topo="full" 
        then
            let actorArray = Array.create N (spawn system "processor1" processor1)
            {0..N-1} |> Seq.iter (fun a ->
                actorArray.[a] <- spawn system (string a) processor1
            )
            // send to acot 2, actor1 update first: s/2, w/2 
            array_s.[1]<-array_s.[1]/2.0
            array_w.[1]<-array_w.[1]/2.0
            actorArray.[1] <? ProcessJob(2,topo,array_s.[1],array_w.[1] )|>ignore
            let System_wait=0
            //let mutable array_terminate_sum = array_terminate|> Array.sum
            //let mutable sum=0
            // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
            array_terminate_sum <- array_terminate|> Array.sum
            while array_terminate_sum < N do
                printfn "%A" array_terminate
                printfn "Current terminated number: %i" (array_terminate_sum)
                printfn "Current actor 0 has s:%f w:%f r:%f" array_s.[0] array_w.[0] array_r.[0] 
                printfn "Current actor 1 has s:%f w:%f r:%f" array_s.[1] array_w.[1] array_r.[1]
                printfn "Current actor 2 has s:%f w:%f r:%f" array_s.[2] array_w.[2] array_r.[2] 
                //printfn "Current actor 3 has s:%f w:%f r:%f" array_s.[3] array_w.[3] array_r.[3] 
                System_wait|>ignore
            //array_terminate_sum <- array_terminate|> Array.sum
            printfn "-----------------------------------------------------------------------------\n" 
            printfn "%A" array_terminate
            printf "No more spreading available.\n" 
            printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
            printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds

    if topo="2D" 
    then
        let actorArray = Array.create N (spawn system "processor2" processor2)
        {0..N-1} |> Seq.iter (fun a ->
            actorArray.[a] <- spawn system (string a) processor2
        )
        // send to acot 2, actor1 update first: s/2, w/2 
        array_s.[1]<-array_s.[1]/2.0
        array_w.[1]<-array_w.[1]/2.0
        actorArray.[1] <? ProcessJob(2,topo,array_s.[1],array_w.[1] )|>ignore
        let System_wait=0
        //let mutable array_terminate_sum = array_terminate|> Array.sum
        //let mutable sum=0
        // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
        array_terminate_sum <- array_terminate|> Array.sum
        while array_terminate_sum < N do
            printfn "%A" array_terminate
            printfn "Current terminated number: %i" (array_terminate_sum)
            printfn "Current actor 0 has s:%f w:%f r:%f" array_s.[0] array_w.[0] array_r.[0] 
            printfn "Current actor 1 has s:%f w:%f r:%f" array_s.[1] array_w.[1] array_r.[1]
            printfn "Current actor 2 has s:%f w:%f r:%f" array_s.[2] array_w.[2] array_r.[2] 
            //printfn "Current actor 3 has s:%f w:%f r:%f" array_s.[3] array_w.[3] array_r.[3] 
            System_wait|>ignore
        //array_terminate_sum <- array_terminate|> Array.sum
        printfn "-----------------------------------------------------------------------------\n" 
        printfn "%A" array_terminate
        printf "No more spreading available.\n" 
        printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
        printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds
    
    if topo="imp2D" 
    then
        let actorArray = Array.create N (spawn system "processor3" processor3)
        {0..N-1} |> Seq.iter (fun a ->
            actorArray.[a] <- spawn system (string a) processor3
        )
        // send to acot 2, actor1 update first: s/2, w/2 
        array_s.[1]<-array_s.[1]/2.0
        array_w.[1]<-array_w.[1]/2.0
        actorArray.[1] <? ProcessJob(2,topo,array_s.[1],array_w.[1] )|>ignore
        let System_wait=0
        //let mutable array_terminate_sum = array_terminate|> Array.sum
        //let mutable sum=0
        // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
        array_terminate_sum <- array_terminate|> Array.sum
        while array_terminate_sum < N do
            printfn "%A" array_terminate
            printfn "Current terminated number: %i" (array_terminate_sum)
            printfn "Current actor 0 has s:%f w:%f r:%f" array_s.[0] array_w.[0] array_r.[0] 
            printfn "Current actor 1 has s:%f w:%f r:%f" array_s.[1] array_w.[1] array_r.[1]
            printfn "Current actor 2 has s:%f w:%f r:%f" array_s.[2] array_w.[2] array_r.[2] 
            //printfn "Current actor 3 has s:%f w:%f r:%f" array_s.[3] array_w.[3] array_r.[3] 
            System_wait|>ignore
        //array_terminate_sum <- array_terminate|> Array.sum
        printfn "-----------------------------------------------------------------------------\n" 
        printfn "%A" array_terminate
        printf "No more spreading available.\n" 
        printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
        printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds
    
    if topo="line" 
    then
        let actorArray = Array.create N (spawn system "processor4" processor4)
        {0..N-1} |> Seq.iter (fun a ->
            actorArray.[a] <- spawn system (string a) processor4
        )
        // send to acot 2, actor1 update first: s/2, w/2 
        array_s.[1]<-array_s.[1]/2.0
        array_w.[1]<-array_w.[1]/2.0
        actorArray.[1] <? ProcessJob(2,topo,array_s.[1],array_w.[1] )|>ignore
        let System_wait=0
        //let mutable array_terminate_sum = array_terminate|> Array.sum
        //let mutable sum=0
        // N-2 is the boundary since when N-1 actors terminated, this algorithm can't running anymore
        array_terminate_sum <- array_terminate|> Array.sum
        while array_terminate_sum < N do
            //printfn "%A" array_terminate
            printfn "Current terminated number: %i" (array_terminate_sum)
            //printfn "Current actor 0 has s:%f w:%f r:%f" array_s.[0] array_w.[0] array_r.[0] 
            //printfn "Current actor 1 has s:%f w:%f r:%f" array_s.[1] array_w.[1] array_r.[1]
            //printfn "Current actor 2 has s:%f w:%f r:%f" array_s.[2] array_w.[2] array_r.[2] 
            //printfn "Current actor 3 has s:%f w:%f r:%f" array_s.[3] array_w.[3] array_r.[3] 
            System_wait|>ignore
        //array_terminate_sum <- array_terminate|> Array.sum
        printfn "-----------------------------------------------------------------------------\n" 
        printfn "%A" array_terminate
        printf "No more spreading available.\n" 
        printf "ALL STOPPED! Terminated/Total: (%i)/(%i) \n " (array_terminate_sum) N
        printfn "Running time is: %f ms" stopWatch.Elapsed.TotalMilliseconds


   
             
   


    

    
    0 // return an integer exit code      


