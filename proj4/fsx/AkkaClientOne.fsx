#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Actor
open Akka.Configuration
open Akka.Dispatch.SysMsg
open Akka.FSharp
open System.Threading
type MessagePack_processor = MessagePack8 of  string  * string * string* string* string * string* string* string * string

// number of user
let mutable N = 1000


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
                    port = "+string (System.Random( ).Next(10000,20000))+" 
                    hostname = localhost
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)

let echoServer = system.ActorSelection(
                            "akka.tcp://RemoteFSharp@localhost:8777/user/EchoServer")


let mutable prev_query = ""
let mutable auto = false
let actor_user_connect (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | username,password ->
            while not auto do
                Thread.Sleep(500)
            let task = echoServer <?  "querying, ," + username + "," + password + ", , , , , "
            let response = Async.RunSynchronously (task, 1000) |> string
            if not (response = prev_query) then
                prev_query <- response
                printfn "[AutoQuery]%s" response
                printfn "%s" ""
            Thread.Sleep(1000)
            mailbox.Self <? (username, password) |> ignore
            return! loop() 
    }
    loop ()
    
let client_user_connect = spawn system "client_user_connect" actor_user_connect

let actor_user (mailbox: Actor<_>) = 
    let rec loop () = actor {        
//        let! message = mailbox.Receive ()
        let cmd = Console.ReadLine()
        let result = cmd.Split ','
        let opt = result.[0]
        if opt="connect" then
            let username=result.[2]
            let password=result.[3]
            auto <- true
            client_user_connect <? (username, password) |> ignore
            return! loop() 
        else if opt="disconnect" then
            auto <- false
            return! loop() 
        let task = echoServer <? cmd
        let response = Async.RunSynchronously (task, 1000)
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
//        mailbox.Self <? "go"
        return! loop()     
    }
    loop ()

let client_user = spawn system "client_user" actor_user


printfn "------------------------------------------------- \n " 
printfn "-------------------------------------------------   " 
printfn "Please enter the function you want to use...   " 
printfn "-------------------------------------------------   "
client_user <? "go" |>ignore
Thread.Sleep(1000000)
system.Terminate() |> ignore

0 // return an integer exit code

(*
9 command format: opt+","+POST+","+username+","+password+","+target_username+","+tweet_content+","+queryhashtag+","+at+","+register

A full Example:
reg, ,user1,123456, , , , ,
reg, ,user2,123456, , , , ,
send, ,user1,123456, ,tweet1 @Biden , , ,
send, ,user2,123456, ,tweet2 #Trump , , ,
send, ,user2,123456, ,tweet3 @Biden #Trump , , ,
subscribe, ,user1,123456,user2, , , ,
querying, ,user1,123456, , , , ,
querying, ,user2,123456, , , , ,
#, , , , , ,#Trump, ,
@, , , , , , ,@Biden,

Retweet Example:
retweet, ,user2,123456, ,tweet3 @Biden #Trump , , , 
querying, ,user2,123456, , , , ,

Connect Example:
connect, ,user1,123456, , , , ,
send, ,user2,123456, ,tweet4, , ,
send, ,user2,123456, ,tweet5, , ,
disconnect, ,user1,123456, , , , ,

*)