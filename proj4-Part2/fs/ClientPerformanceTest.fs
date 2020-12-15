﻿open System
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open System.IO
open System
open System.Threading
open Akka.Actor
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
type MessagePack_processor = MessagePack8 of  string  * string * string* string* string * string* string* string * string

// number of user
let N = 10
let M = N

















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

let aes2 = new AesEncryption("cbc", 256)

let encrypt (data2:string) = 
    let Ciphertext = aes2.Encrypt(data2, " ")
    let Cipherstring =Encoding.UTF8.GetString Ciphertext
    Cipherstring
let sendCmd cmd =
    let encrypted_message = encrypt cmd
    let json = " {\"command\":\"" + encrypted_message + "\"} "
    let response = Http.Request(
        "http://127.0.0.1:8080/twitter",
        httpMethod = "POST",
        headers = [ ContentType HttpContentTypes.Json ],
        body = TextRequest json
        )
    let response = string response.Body
    response

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
        let response = sendCmd cmd
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
            let mutable queryhashtag = "#topic"+rand.Next(N) .ToString()
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
            let response = sendCmd cmd
            printfn "[command]%s" cmd
            printfn "[Reply]%s" (string(response))
            printfn "%s" ""
            addIIByOne()
        return! loop()     
    }
    loop ()

let client_user_register = spawn system "client_user_register" actor_user_register    
let client_simulator = spawn system "client_simulator" actor_client_simulator


[<EntryPoint>]
let main (argv: string []): int = 
    printfn "%A" argv
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
    let time_register = stopWatch.Elapsed.TotalMilliseconds
//    Thread.Sleep(5000)
    
    
    
    
    printfn "------------------------------------------------- \n " 
    printfn "-------------------------------------------------   " 
    printfn "send tweet...   " 
    printfn "-------------------------------------------------   "
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    for i in 0..N-1 do
        for j in 0..10 do
            let cmd = "send, ,user"+(string i)+",password"+(string i)+", ,tweet+user"+(string i)+"_"+(string j)+"th @user"+(string (rand.Next(N)))+" #topic"+(string (rand.Next(N)))+" , , , "
//            let cmd = "send, ,user"+(string i)+",password"+(string i)+", ,@user"+(string (rand.Next(N)))+" #topic"+(string (rand.Next(N)))+" , , , "
//            let cmd = "send, ,user"+(string i)+",password"+(string i)+", ,t, , , "
            let response = sendCmd cmd
            printfn "[command]%s" cmd
            printfn "[Reply]%s" (string(response))
            printfn "%s" ""
    stopWatch.Stop()
    let time_send = stopWatch.Elapsed.TotalMilliseconds
    
    
    
    
    
    let mutable step = 1
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    printfn "Zipf Subscribe ----------------------------------"  
    for i in 0..N-1 do
        for j in 0..step..N-1 do
            if not (j=i) then
                let cmd = "subscribe, ,user"+(string j)+",password"+(string j)+",user"+(string i)+", , , , "
                let response = sendCmd cmd
                printfn "[command]%s" cmd
                printfn "[Reply]%s" (string(response))
                printfn "%s" ""
            step <- step+1
    stopWatch.Stop()
    let time_zipf_subscribe = stopWatch.Elapsed.TotalMilliseconds
        
    
    
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    for i in 0..N-1 do
        let cmd = "querying, ,user"+(string i)+",password"+(string i)+", , , , , "
        let response = sendCmd cmd
        printfn "[command]%s" cmd
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
    stopWatch.Stop()
    let time_query = stopWatch.Elapsed.TotalMilliseconds
    
    
    
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    for i in 0..N-1 do
        let cmd = "#, , , , , ,#topic"+(string (rand.Next(N)))+", ,"
        let response = sendCmd cmd
        printfn "[command]%s" cmd
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
    stopWatch.Stop()
    let time_hashtag = stopWatch.Elapsed.TotalMilliseconds
    
    
    
    
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    for i in 0..N-1 do
        let cmd = "@, , , , , , ,@user"+(string (rand.Next(N)))+","
        let response = sendCmd cmd
        printfn "[command]%s" cmd
        printfn "[Reply]%s" (string(response))
        printfn "%s" ""
    stopWatch.Stop()
    let time_mention = stopWatch.Elapsed.TotalMilliseconds
    
    
    

    printfn "------------------------------------------------- \n " 
    printfn "-------------------------------------------------   " 
    printfn " %d Randon Ops and send tweet...   " M 
    printfn "-------------------------------------------------   "
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    i<-0
    ii<-0
    while i<M do
        client_simulator<! string (rand.Next(N)) |>ignore
        i <- i+1
    while ii<M-1 do
        Thread.Sleep(50)
    stopWatch.Stop()
    let time_random = stopWatch.Elapsed.TotalMilliseconds
    
    
    printfn "The time of register %d users is %f" N time_register
    printfn "The time of send 10 tweets is %f" time_send
    printfn "The time of Zipf subscribe %d users is %f" N time_zipf_subscribe
    printfn "The time of query %d users is %f" N time_query
    printfn "The time of query %d hasgtag is %f" N time_hashtag
    printfn "The time of query %d mention is %f" N time_mention
    printfn "The time of %d random operations is %f" M time_random
    
    printfn "Total Result: %f %f %f %f %f %f %f" time_register time_send time_zipf_subscribe time_query time_hashtag time_mention time_random

    
    system.Terminate() |> ignore
    0 // return an integer exit code

