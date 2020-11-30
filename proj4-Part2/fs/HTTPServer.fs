open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
//open Akka.TestKit

open WebSharper
open WebSharper.Sitelets
open global.Suave
open Suave.Web
open WebSharper.Suave
//open WebSharper.UI.Html
//open WebSharper.UI.Server

type EndPoint2 =
    | [<EndPoint "POST /twitter"; Json "body">] Register of body: CMD
and CMD = { command: string}

//let configuration = 
//    ConfigurationFactory.ParseString(
//        @"akka {
//            log-config-on-start : on
//            stdout-loglevel : DEBUG
//            loglevel : ERROR
//            actor {
//                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
//                debug : {
//                    receive : on
//                    autoreceive : on
//                    lifecycle : on
//                    event-stream : on
//                    unhandled : on
//                }
//            }
//            remote {
//                helios.tcp {
//                    port = 8777
//                    hostname = localhost
//                }
//            }
//        }")
//let system = ActorSystem.Create("RemoteFSharp", configuration)
let system = System.create "RemoteFSharp" (Configuration.defaultConfig())


type Tweet(tweet_id:string, text:string, is_re_tweet:bool) =
    member this.tweet_id = tweet_id
    member this.text = text
    member this.is_re_tweet = is_re_tweet
//    member this.time = System.DateTime.Now.ToFileTimeUtc() |> string
    override this.ToString() =
      let mutable res = ""
      if is_re_tweet then
        res <- sprintf "[retweet][%s]%s" this.tweet_id this.text
      else
        res <- sprintf "[%s]%s" this.tweet_id this.text
//        res <- sprintf "%s" this.text
      res

//let tweet1 = new Tweet("1", "tweet1", false)
//let tweet2 = new Tweet("2", "tweet2", false)
//let tweet3 = new Tweet("3", "tweet3", false)
//printfn "%A" tweet1

type User(user_name:string, password:string) =
    let mutable subscribes = List.empty: User list
    let mutable tweets = List.empty: Tweet list
    member this.user_name = user_name
    member this.password = password
    member this.addSubscribe x =
        subscribes <- List.append subscribes [x]
    member this.getSubscribes() =
        subscribes
    member this.addTweet x =
        tweets <- List.append tweets [x]
    member this.getTweets() =
        tweets
    override this.ToString() = 
       this.user_name
       

//let user1 = new User("user1", "111")
//let user2 = new User("user2", "222")
//let user3 = new User("user3", "333")
//printfn "%A" (user1.user_name, user1.password, user1.getSubscribes(), user1.getTweets())
//user1.addSubscribe(user1)
//user1.addSubscribe(user3)
//user1.addTweet(tweet1)
//user1.addTweet(tweet3)
//printfn "%A" (user1.user_name, user1.password, user1.getSubscribes(), user1.getTweets())

type Twitter() =
    let mutable tweets = new Map<string,Tweet>([])
    let mutable users = new Map<string,User>([])
    let mutable hashtags = new Map<string, Tweet list>([])
    let mutable mentions = new Map<string, Tweet list>([])
    member this.AddTweet (tweet:Tweet) =
        tweets <- tweets.Add(tweet.tweet_id,tweet)
    member this.AddUser (user:User) =
        users <- users.Add(user.user_name, user)
    member this.AddToHashTag hashtag tweet =
        let key = hashtag
        let mutable map = hashtags
        if map.ContainsKey(key)=false
        then
            let l = List.empty: Tweet list
            map <- map.Add(key, l)
        let value = map.[key]
        map <- map.Add(key, List.append value [tweet])
        hashtags <- map
    member this.AddToMention mention tweet = 
        let key = mention
        let mutable map = mentions
        if map.ContainsKey(key)=false
        then
            let l = List.empty: Tweet list
            map <- map.Add(key, l)
        let value = map.[key]
        map <- map.Add(key, List.append value [tweet])
        mentions <- map
    member this.register username password =
        let mutable res = ""
        if users.ContainsKey(username) then
            res <- "error, username already exist"
        else
            let user = new User(username, password)
            this.AddUser user
            user.addSubscribe user
            res <- "Register success username: " + username + "  password: " + password
        res
    member this.SendTweet username password text is_re_tweet =
        let mutable res = ""
        if not (this.authentication username password) then
            res <- "error, authentication fail"
        else
            if users.ContainsKey(username)=false then
                res <-  "error, no this username"
            else
                let tweet = new Tweet(System.DateTime.Now.ToFileTimeUtc() |> string, text, is_re_tweet)
                let user = users.[username]
                user.addTweet tweet
                this.AddTweet tweet
                let idx1 = text.IndexOf("#")
                if not (idx1 = -1) then
                    let idx2 = text.IndexOf(" ",idx1)
                    let hashtag = text.[idx1..idx2-1]
                    this.AddToHashTag hashtag tweet
                let idx1 = text.IndexOf("@")
                if not (idx1 = -1) then
                    let idx2 = text.IndexOf(" ",idx1)
                    let mention = text.[idx1..idx2-1]
                    this.AddToMention mention tweet
                res <-  "[success] sent twitter: " + tweet.ToString()
        res
    member this.authentication username password =
            let mutable res = false
            if not (users.ContainsKey(username)) then
                printfn "%A" "error, no this username"
            else
                let user = users.[username]
                if user.password = password then
                    res <- true
            res
    member this.getUser username = 
        let mutable res = new User("","")
        if not (users.ContainsKey(username)) then
            printfn "%A" "error, no this username"
        else
            res <- users.[username]
        res
    member this.subscribe username1 password username2 =
        let mutable res = ""
        if not (this.authentication username1 password) then
            res <- "error, authentication fail"
        else
            let user1 = this.getUser username1
            let user2 = this.getUser username2
            user1.addSubscribe user2
            res <- "[success] " + username1 + " subscribe " + username2
        res
    member this.reTweet username password text =
        let res = "[retweet]" + (this.SendTweet username password text true)
        res
    member this.queryTweetsSubscribed username password =
        let mutable res = ""
        if not (this.authentication username password) then
            res <- "error, authentication fail"
        else
            let user = this.getUser username
            let res1 = user.getSubscribes() |> List.map(fun x-> x.getTweets()) |> List.concat |> List.map(fun x->x.ToString()) |> String.concat "\n"
            res <- "[success] queryTweetsSubscribed" + "\n" + res1
        res
    member this.queryHashTag hashtag =
        let mutable res = ""
        if not (hashtags.ContainsKey(hashtag)) then
            res <- "error, no this hashtag"
        else
            let res1 = hashtags.[hashtag] |>  List.map(fun x->x.ToString()) |> String.concat "\n"
            res <- "[success] queryHashTag" + "\n" + res1
        res
    member this.queryMention mention =
        let mutable res = ""
        if not (mentions.ContainsKey(mention)) then
            res <- "error, no this mention"
        else
            let res1 = mentions.[mention] |>  List.map(fun x->x.ToString()) |> String.concat "\n"
            res <-  "[success] queryMention" + "\n" + res1
        res
    override this.ToString() =
        "print the entire Twitter"+ "\n" + tweets.ToString() + "\n" + users.ToString() + "\n" + hashtags.ToString() + "\n" + mentions.ToString()
        
    
let twitter = new Twitter()
//printfn "%A" twitter
// test 1
//twitter.AddUser user1
//twitter.AddUser user3
//twitter.AddTweet tweet1
//twitter.AddTweet tweet3
//twitter.AddToMention "@Biden" tweet1
//twitter.AddToMention "@Biden" tweet3
//twitter.AddToMention "@Trump" tweet2
//twitter.AddToHashTag "#Singapore" tweet1
//twitter.AddToHashTag "#Singapore" tweet2
//twitter.AddToHashTag "#France" tweet3

//// test 2
//twitter.register "user1" "123456"
////printfn ""
//twitter.register "user2" "123456"
//printfn ""
//twitter.SendTweet "user1" "123456" "tweet1 @Biden " false
//printfn ""
//twitter.SendTweet "user2" "123456" "tweet2 #Trump " false
//printfn ""
//twitter.SendTweet "user2" "123456" "tweet3 @Biden #Trump " false
//printfn ""
//twitter.reTweet "username" "password" "im retwittering..."
//printfn ""
//twitter.subscribe "user1" "123456" "user2"
//printfn ""
//twitter.queryTweetsSubscribed "user1" "123456"
//printfn ""
//twitter.queryTweetsSubscribed "user2" "123456"
//printfn ""
//twitter.queryHashTag "#Trump"
//printfn ""
//twitter.queryMention "@Biden"
//printfn ""
//printfn "%A" twitter

///////   The actor and message were defined below     /////////////

// Define message pack for different actor
// actor reg:  "POST", "/register", "username", "password"
type MessagePack_reg = MessagePack1 of  string  * string  * string* string
// actor send:  "POST", "username", "password", "tweet_content", false
type MessagePack_send = MessagePack2 of  string  * string  * string* string* bool
// actor subscribe:  "POST", "username", "password","target_username" 
type MessagePack_subscribe = MessagePack3 of  string  * string  * string* string 
// actor retweet:  "POST", "username", "password", "tweet_content" 
type MessagePack_retweets = MessagePack4 of  string  * string  * string * string
//twitter.queryTweetsSubscribed "user1" "123456"
// actor queryTweetsSubscribed:  "POST", "username", "password"  
type MessagePack_queryTweetsSubscribed = MessagePack5 of  string  * string  * string 
// actor #, queryHashTag:  "POST", "queryHashTag" 
type MessagePack_hashtag = MessagePack6 of  string  * string   
// actor @, queryHashTag:  "POST", "at" 
type MessagePack_at = MessagePack7 of  string  * string  

// The 1st actor is for reg
let actor_reg (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
//        printfn "%A" message
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack1(a,register,username,password) ->
            let res = twitter.register username password
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 2nd actor is for send
let actor_send (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        let sender_path = mailbox.Sender().Path.ToStringWithAddress()
        match message with
        |   MessagePack2(a,username,password,tweet_content,false) -> 
            let res = twitter.SendTweet username password tweet_content false
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 3rd actor is for subscribe
let actor_subscribe (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack3(a,username,password,target_username) -> 
            let res = twitter.subscribe username password target_username
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 4th actor is for retweet
let actor_retweet (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack4(a,username,password,tweet_content) -> 
            let res = twitter.reTweet  username password tweet_content
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 5th actor is for query Tweets Subscribed
let actor_querying (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack5(a,username,password ) -> 
            let res = twitter.queryTweetsSubscribed  username password
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 6th actor is for queryHashTag
let actor_queryHashTag (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack6(a,queryhashtag) -> 
            let res = twitter.queryHashTag  queryhashtag
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()
// The 7th actor is for @ (at)
let actor_at (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message  with
        |   MessagePack7(a,at) -> 
            let res = twitter.queryMention  at
            sender <? res |> ignore
        | _ ->  failwith "unknown message"
        return! loop()     
    }
    loop ()

////////////////////////////////////////
// opt is the operation we will use
// opt= "reg", "send", "subscribe", "retweet", "querying", "#" , "@"
let mutable opt= "reg" 
//let mutable POST="POST"
let mutable username="user2"
let mutable password="123456"
//let mutable register="register"
let mutable target_username="user1"
let mutable tweet_content="Today is a good day!"
let mutable queryhashtag="#Trump"
let mutable at="@Biden"
// MessagePack between processor defined below:
//( opt,POST,username,password,target_username,tweet_content,queryhashtag,at,register)
type MessagePack_processor = MessagePack8 of  string  * string * string* string* string * string* string* string * string
//MessagePack8( opt,POST,username,password,target_username,tweet_content,queryhashtag,at,register)
// dedined the message received actor


let actor_REG = spawn system "processor1" actor_reg
let actor_SEND = spawn system "processor2" actor_send
let actor_SUBSCRIBE = spawn system "processor3" actor_subscribe
let actor_RETWEET = spawn system "processor4" actor_retweet
let actor_QUERYING = spawn system "processor5" actor_querying 
let actor_QUERHASHTAG = spawn system "processor6" actor_queryHashTag
let actor_AT = spawn system "processor7" actor_at

// for the server, received string and dispatch
let actor_msgreceived (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        printfn "bbbbbbb %s" message
        match box message with
        | :? string   ->
            if message="" then
                return! loop() 
            //(opt,POST,username,password,target_username,tweet_content,queryhashtag,at,register)
            printfn "%s" ""
            printfn "[message received] %s" message
            let result = message.Split ','
            let mutable opt= result.[0]
            let mutable POST=result.[1]
            let mutable username=result.[2]
            let mutable password=result.[3]
            let mutable target_username=result.[4]
            let mutable tweet_content=result.[5]
            let mutable queryhashtag=result.[6]
            let mutable at=result.[7]
            let mutable register=result.[8]
            let mutable task = actor_REG <? MessagePack1("","","","")
            // For function reg
            if opt= "reg" then
                printfn "[Register] username:%s password: %s" username password
                task <- actor_REG <? MessagePack1(POST,register,username,password)
            // For function send
            if opt= "send" then
                printfn "[send] username:%s password: %s tweet_content: %s" username password tweet_content
                task <- actor_SEND <? MessagePack2(POST,username,password,tweet_content,false)
            // For function subscribe
            if opt= "subscribe" then
                printfn "[subscribe] username:%s password: %s subscribes username: %s" username password target_username
                task <- actor_SUBSCRIBE <? MessagePack3(POST,username,password,target_username )
            // For function retweet
            if opt= "retweet" then
                printfn "[retweet] username:%s password: %s tweet_content: %s" username password tweet_content
                task <- actor_RETWEET <? MessagePack4(POST,username,password,tweet_content)
            // For function retweet
            if opt= "querying" then
                printfn "[querying] username:%s password: %s" username password
                task <- actor_QUERYING <? MessagePack5(POST,username,password )
            // For function retweet queryhashtag
            if opt= "#" then
                printfn "[#Hashtag] %s: " queryhashtag
                task <- actor_QUERHASHTAG <? MessagePack6(POST,queryhashtag )
            // For function @
            if opt= "@" then
                printfn "[@mention] %s" at
                task <- actor_AT <? MessagePack7(POST,at )
            let response = Async.RunSynchronously (task, 1000)
            sender <? response |> ignore
            printfn "[Result]: %s" response
        return! loop()     
    }
    loop ()
let actor_MSGreceived = spawn system "EchoServer" actor_msgreceived


[<Website>]
let Main =

    let mainWebsite = Application.MultiPage (fun context action ->
        match action with
        | EndPoint2.Register body ->            
            let task = actor_MSGreceived <? body.command
            let response = Async.RunSynchronously (task, 1000)
            Content.Text response
    )

    Sitelet.Sum [ mainWebsite ]

//actor_MSGreceived <? "" |> ignore



[<EntryPoint>] 
let main argv =
    printfn "%A" argv
    // once we received a set of string, dispatch to different functional actor
    // dispatch was based on the opt.
    
//    actor_MSGreceived <? "" |> ignore
    printfn "------------------------------------------------- \n " 
    printfn "-------------------------------------------------   " 
    printfn "Twitter Server is running...   " 
    printfn "-------------------------------------------------   "
    
    // TODO Very important: This sentence must be inside the Main, otherwise it will not work
    startWebServer defaultConfig
        (WebSharperAdapter.ToWebPart(Main, RootDirectory="../.."))
    
    // For function reg
    Console.ReadLine() |> ignore
   
    printfn "-----------------------------------------------------------\n" 
    0