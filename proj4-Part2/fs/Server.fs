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
open System.Security.Cryptography
open System.Text
open System.IO
open System
open System.Text.RegularExpressions











/// <summary>
/// Encrypts data and files using AES CBC/CFB - 128/192/256 bits.
/// 
/// The encryption and authentication keys 
/// are derived from the supplied key/password using HKDF/PBKDF2.
/// The key can be set either with `SetMasterKey` or with `RandomKeyGen`.
/// Encrypted data format: salt[16] + iv[16] + ciphertext[n] + mac[32].
/// Ciphertext authenticity is verified with HMAC SHA256.
/// 
/// CFB is not supported in .NET Core.
/// </summary>
/// <param name="mode">Optional, the AES mode (CBC or CFB)</param>
/// <param name="size">Optional, the key size (128, 192, 256)</param>
/// <exception cref="ArgumentException">
/// Thrown when mode is not supported or size is invalid.
/// </exception>
type AesEncryption(?mode:string, ?size:int) = 
    let modes = Map.empty.Add("CBC", CipherMode.CBC).Add("CFB", CipherMode.CFB)
    let sizes = [ 128; 192; 256 ]
    let saltLen = 16
    let ivLen = 16
    let macLen = 32
    let macKeyLen = 32

    let mode = (defaultArg mode "CBC").ToUpper()
    let keyLen = (defaultArg size 128) / 8
    let size = defaultArg size 128
    let mutable masterKey:byte[] = null

    do
        if not (List.exists ((=) size) sizes) then
            raise (ArgumentException "Invalid key size!")
        if not (modes.ContainsKey mode) then
            raise (ArgumentException (mode + " is not supported!"))

    /// The number of PBKDF2 iterations (applies to password based keys).
    member val keyIterations = 20000 with get, set
    /// Accepts ans returns base64 encoded data.
    member val base64 = true with get, set

    /// <summary>
    /// Encrypts data using a master key or the supplied password.
    /// 
    /// The password is not required if a master key has been set 
    /// (either with `RandomKeyGgen` or with `SetMasterKey`). 
    /// If a password is supplied, it will be used to create a key with PBKDF2.
    /// </summary>
    /// <param name="data">The plaintext.</param>
    /// <param name="password">Optional, the password.</param>
    /// <returns>Encrypted data (salt + iv + ciphertext + mac).</returns>
    member this.Encrypt(data:byte[], ?password:string):byte[] = 
        let iv = this.RandomBytes ivLen
        let salt = this.RandomBytes saltLen
        try
            let aesKey, macKey = this.Keys(salt, (defaultArg password null))

            use cipher = this.Cipher(aesKey, iv)
            use ict = cipher.CreateEncryptor()
            let ciphertext = ict.TransformFinalBlock(data, 0, data.Length)

            let iv_ct = Array.append iv ciphertext
            let mac = this.Sign(iv_ct, macKey)
            let encrypted = Array.append (Array.append salt iv_ct) mac

            if this.base64 then
                Encoding.ASCII.GetBytes (Convert.ToBase64String encrypted)
            else
                encrypted
        with 
            | :? ArgumentException as e -> this.ErrorHandler e; null
            | :? CryptographicException as e -> this.ErrorHandler e; null
    
    /// <summary>Encrypts data using a master key or the supplied password.</summary>
    /// <param name="data">The plaintext.</param>
    /// <param name="password">Optional, the password.</param>
    /// <returns>Encrypted data (salt + iv + ciphertext + mac).</returns>
    member this.Encrypt(data:string, ?password:string):byte[] = 
        this.Encrypt (Encoding.UTF8.GetBytes(data), (defaultArg password null))
    
    /// <summary>
    /// Decrypts data using a master key or the supplied password.
    /// 
    /// The password is not required if a master key has been set 
    /// (either with `RandomKeyGgen` or with `SetMasterKey`). 
    /// If a password is supplied, it will be used to create a key with PBKDF2.
    /// </summary>
    /// <param name="data">The ciphertext (raw of base46-encoded bytes).</param>
    /// <param name="password">Optional, the pasword.</param>
    member this.Decrypt(data:byte[], ?password:string):byte[] = 
        let mutable data = data
        try
            if this.base64 then 
                data <- Convert.FromBase64String(Encoding.ASCII.GetString data)
            
            let salt = data.[0..saltLen - 1]
            let iv = data.[saltLen..saltLen + ivLen - 1]
            let ciphertext = data.[saltLen + ivLen..data.Length - macLen - 1]
            let mac = data.[data.Length - macLen..data.Length - 1]

            let aesKey, macKey = this.Keys(salt, (defaultArg password null))
            this.Verify((Array.append iv ciphertext), mac, macKey)

            use cipher = this.Cipher(aesKey, iv)
            use ict = cipher.CreateDecryptor()
            let plaintext = ict.TransformFinalBlock(ciphertext, 0, ciphertext.Length)
            plaintext
        with 
            | :? ArgumentException as e -> this.ErrorHandler e; null
            | :? CryptographicException as e -> this.ErrorHandler e; null
            | :? FormatException as e -> this.ErrorHandler e; null
            | :? IndexOutOfRangeException as e -> this.ErrorHandler e; null
    
    /// <summary>Decrypts data using a master key or the supplied password.</summary>
    /// <param name="data">The ciphertext (raw of base46-encoded bytes).</param>
    /// <param name="password">Optional, the pasword.</param>
    member this.Decrypt(data:string, ?password:string):byte[] = 
        this.Decrypt (Encoding.UTF8.GetBytes (data), (defaultArg password null))
    

    /// <summary>
    /// Encrypts files using a master key or the supplied password.
    /// 
    /// The password is not required if a master key has been set 
    /// (either with `RandomKeyGgen` or with `SetMasterKey`). 
    /// If a password is supplied, it will be used to create a key with PBKDF2.
    /// The original file is not modified; a new encrypted file is created.   
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">Optional, the pasword.</param>
    member this.EncryptFile(path:string, ?password:string):string = 
        let iv = this.RandomBytes ivLen
        let salt = this.RandomBytes saltLen
        try
            let newPath = path + ".enc"
            use fs = new FileStream(newPath, FileMode.Create, FileAccess.Write) 
            fs.Write(salt, 0, saltLen)
            fs.Write(iv, 0, ivLen)

            let aesKey, macKey = this.Keys(salt, (defaultArg password null))
            use cipher = this.Cipher(aesKey, iv)
            use ict = cipher.CreateEncryptor()
            use hmac = new HMACSHA256(macKey)
            hmac.TransformBlock(iv, 0, iv.Length, null, 0) |> ignore

            for data, fend in this.FileChunks(path) do
                let mutable ciphertext = Array.create data.Length 0uy

                if fend then
                    ciphertext <- ict.TransformFinalBlock(data, 0, data.Length)
                    hmac.TransformFinalBlock(ciphertext, 0, ciphertext.Length) |> ignore
                else
                    ict.TransformBlock(data, 0, data.Length, ciphertext, 0) |> ignore
                    hmac.TransformBlock(ciphertext, 0, ciphertext.Length, null, 0) |> ignore
                fs.Write(ciphertext, 0, ciphertext.Length)
            
            let mac = hmac.Hash
            fs.Write(mac, 0, mac.Length)
            newPath
        with 
            | :? ArgumentException as e -> this.ErrorHandler e; null
            | :? CryptographicException as e -> this.ErrorHandler e; null
            | :? UnauthorizedAccessException as e -> this.ErrorHandler e; null
            | :? FileNotFoundException as e -> this.ErrorHandler e; null
    
    /// <summary>
    /// Decrypts files using a master key or the supplied password.
    /// 
    /// The password is not required if a master key has been set 
    /// (either with `RandomKeyGgen` or with `SetMasterKey`). 
    /// If a password is supplied, it will be used to create a key with PBKDF2.
    /// The original file is not modified; a new decrypted file is created.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">Optional, the pasword.</param>
    member this.DecryptFile(path:string, ?password:string):string = 
        let salt = Array.create saltLen 0uy
        let iv = Array.create ivLen 0uy
        let mac = Array.create macLen 0uy

        try
            let newPath = Regex.Replace(path, ".enc$", ".dec")
            let fileSize = (int)(new FileInfo(path)).Length
            use fs = new FileStream(path, FileMode.Open, FileAccess.Read)

            fs.Read(salt, 0, saltLen) |> ignore
            fs.Read(iv, 0, ivLen) |> ignore
            fs.Seek((int64)(fileSize - macLen), SeekOrigin.Begin) |> ignore
            fs.Read(mac, 0, macLen) |> ignore

            let aesKey, macKey = this.Keys(salt, (defaultArg password null))
            this.VerifyFile(path, mac, macKey)
        
            use fs = new FileStream(newPath, FileMode.Create, FileAccess.Write)
            use cipher = this.Cipher(aesKey, iv)
            use ict = cipher.CreateDecryptor()

            for data, fend in this.FileChunks(path, saltLen + ivLen, macLen) do
                let mutable plaintext = Array.create data.Length 0uy
                let mutable size = 0

                if fend then
                    plaintext <- ict.TransformFinalBlock(data, 0, data.Length)
                    size <- plaintext.Length
                else
                    size <- ict.TransformBlock(data, 0, data.Length, plaintext, 0)
                fs.Write(plaintext, 0, size)
            newPath
        with 
            | :? ArgumentException as e -> this.ErrorHandler e; null
            | :? CryptographicException as e -> this.ErrorHandler e; null
            | :? UnauthorizedAccessException as e -> this.ErrorHandler e; null
            | :? FileNotFoundException as e -> this.ErrorHandler e; null
    
    /// <summary>
    /// Sets a new master key.
    /// This key will be used to create the encryption and authentication keys.
    /// </summary>
    /// <param name="key">The new master key.</param>
    /// <param name="raw">Optional, expexts raw bytes, not base64-encoded.</param>
    member this.SetMasterKey(key:byte[], ?raw:bool) =
        let mutable key = key
        try
            if not (defaultArg raw false) then
                key <- Convert.FromBase64String(Encoding.ASCII.GetString key)
            masterKey <- key
        with 
            | :? FormatException as e -> this.ErrorHandler e
    
    /// <summary>
    /// Sets a new master key.
    /// This key will be used to create the encryption and authentication keys.
    /// </summary>
    /// <param name="key">The new master key.</param>
    member this.SetMasterKey(key:string) =
        this.SetMasterKey((Encoding.ASCII.GetBytes key), false);

    /// <summary>
    /// Returns the master key (or null if the key is not set).
    /// </summary>
    /// <param name="raw">Optional, returns raw bytes, not base64-encoded.</param>
    member this.GetMasterKey(?raw:bool):byte[] =
        if masterKey = null then
            this.ErrorHandler (Exception "The key is not set!")
            null
        elif not (defaultArg raw false) then
            Encoding.ASCII.GetBytes (Convert.ToBase64String masterKey)
        else
            masterKey
    
    /// <summary>
    /// Generates a new random key.
    /// This key will be used to create the encryption and authentication keys.
    /// </summary>
    /// <param name="keyLen">Optional, the key size.</param>
    /// <param name="raw">Optional, returns raw bytes, not base64-encoded.</param>
    member this.RandomKeyGen(?keyLen:int, ?raw:bool):byte[] =
        masterKey <- this.RandomBytes(defaultArg keyLen 32)
        if (defaultArg raw false) then
            masterKey
        else
            Encoding.ASCII.GetBytes (Convert.ToBase64String masterKey)
    
    /// Derives encryption and authentication keys from a key or password.
    /// If the password is not null, it will be used to create the keys.
    member private this.Keys(salt:byte[], ?password:string) = 
        let password = (defaultArg password null)
        let mutable dkey:byte[] = null

        if password <> null then
            dkey <- this.Pbkdf2Sha512(password, salt, keyLen + macKeyLen)
        elif masterKey <> null then
            dkey <- this.HkdfSha256(masterKey, salt, keyLen + macKeyLen)
        else
            raise (ArgumentException "No password or key specified!")
        dkey.[..keyLen - 1], dkey.[keyLen..]
    
    /// Creates random bytes; used for salt, IV and key generation.
    member private this.RandomBytes(size:int) =
        let rb = Array.create size 0uy
        use rng = new RNGCryptoServiceProvider()
        rng.GetBytes rb
        rb
    
    /// Creates an RijndaelManaged object; used for encryption / decryption.
    member private this.Cipher(key:byte[], iv:byte[]):RijndaelManaged =
        let rm =  new RijndaelManaged()
        rm.Mode <- modes.[mode]
        rm.Padding <- if mode = "CFB" then PaddingMode.None else PaddingMode.PKCS7
        rm.FeedbackSize <- if mode = "CFB" then 8 else 128
        rm.KeySize <- size
        rm.Key <- key
        rm.IV <- iv
        rm
    
    /// Computes the MAC of ciphertext; used for authentication.
    member private this.Sign(data:byte[], key:byte[]) = 
        use hmac = new HMACSHA256(key)
        hmac.ComputeHash data
    
    /// Computes the MAC of ciphertext; used for authentication.
    member private this.SignFile(path:string, key:byte[], ?fstart:int, ?fend:int) = 
        use hmac = new HMACSHA256(key)
        for data, _ in this.FileChunks(path, (defaultArg fstart 0), (defaultArg fend 0)) do 
            hmac.TransformBlock(data, 0, data.Length, null, 0) |> ignore
        hmac.TransformFinalBlock((Array.create 0 0uy), 0, 0) |> ignore
        hmac.Hash
    
    /// Verifies the authenticity of ciphertext.
    member private this.Verify(data, mac, key) = 
        let dataMac = this.Sign(data, key)
        if not (this.ConstantTimeComparison (mac, dataMac)) then
            raise (ArgumentException "MAC check failed!")
    
    /// Verifies the authenticity of ciphertext.
    member private this.VerifyFile(path:string, mac:byte[], key:byte[]) = 
        let fileMac = this.SignFile(path, key, saltLen, macLen)
        if not (this.ConstantTimeComparison(mac, fileMac)) then
             raise (ArgumentException "MAC check failed!")
    
    /// Handles exceptions (prints the exception message by default).  
    member private this.ErrorHandler(e:Exception) =
        printfn "%s" e.Message
    
    /// Safely compares two byte arrays, used for uthentication.
    member private this.ConstantTimeComparison(mac1:byte[], mac2:byte[]) =
        let mutable result = mac1.Length ^^^ mac2.Length
        for i in 0 .. (min mac1.Length mac2.Length) - 1 do
            result <- result ||| ((int)mac1.[i] ^^^ (int)mac2.[i])
        result = 0
     
    /// A generator that reads a file and yields chunks of data.
    /// The chunk size should be a multiple of the block size (16).
    member private this.FileChunks(path:string, ?fbeg:int, ?fend:int):seq<Tuple<byte[], bool>> = 
        let mutable size = 1024
        let fs = new FileStream(path, FileMode.Open, FileAccess.Read)
        let fbeg = defaultArg fbeg 0
        let fend = (int)fs.Length - (defaultArg fend 0)
        let mutable pos = fs.Read(Array.create fbeg 0uy, 0, fbeg)

        seq { while pos < fend do
                size <- if fend - pos > size then size else fend - pos
                let data = Array.create size 0uy
                pos <- pos + fs.Read(data, 0, size)
                yield (data, pos = fend)
        }
    
    /// A PBKDF2 algorithm implementation, with HMAC-SHA512.
    member private this.Pbkdf2Sha512(password:string, salt:byte[], dkeyLen:int):byte[] =
        let mutable dkey = Array.zeroCreate<byte> 0
        use prf = new HMACSHA512(Encoding.UTF8.GetBytes password)
        let hashLen = 64;

        for i in 0..hashLen..(dkeyLen - 1) do
            let b = Array.rev (BitConverter.GetBytes ((i / hashLen) + 1))
            let mutable u = prf.ComputeHash (Array.append salt b)
            let f = u

            for _ in 1..(this.keyIterations - 1) do
                u <- prf.ComputeHash u
                for k in 0..f.Length - 1 do
                    f.[k] <- f.[k] ^^^ u.[k]
            dkey <- Array.append dkey f
        dkey.[0..dkeyLen - 1]
    
    /// A PBKHKFDF2 algorithm implementation, with HMAC-SHA256.
    member private this.HkdfSha256(key:byte[], salt:byte[], dkeyLen:int):byte[] =
        let mutable dkey = Array.zeroCreate<byte> 0
        let mutable hkey = Array.zeroCreate<byte> 0
        let hashLen = 32;
        use prkHmac = new HMACSHA256(salt)
        let prk = prkHmac.ComputeHash key

        for i in 0..hashLen..(dkeyLen - 1) do
            hkey <- Array.append hkey [|(byte (i / hashLen + 1))|]
            use hmac = new HMACSHA256(prk)
            hkey <- hmac.ComputeHash hkey
            dkey <- Array.append dkey hkey
        dkey.[0..dkeyLen - 1]




















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
//        printfn "%s" message
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

let aes2 = new AesEncryption("cbc", 256)

let decrypt (Cipherstring:string) = 
    let Cipherstring2=Encoding.UTF8.GetBytes Cipherstring
    let Plaintext = aes2.Decrypt(Cipherstring2, " ")
    let Plainstring =Encoding.UTF8.GetString Plaintext
    Plainstring

[<Website>]
let Main =

    let mainWebsite = Application.MultiPage (fun context action ->
        match action with
        | EndPoint2.Register body ->
            // TODO de-encryption:     body.command (encrypted string) -> string(,,,,,,,)
            let decrypted_string = decrypt body.command
            printfn "[Before decrypted:]%s" body.command
            printfn "[decrypted Message:]%s" decrypted_string
            let task = actor_MSGreceived <? decrypted_string
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