# Project4 Part I

## Team
- Jiajing Liao, 01469951
- Jingzhou Hu, 11319238

## Description
We finish **all** functionality of Project4 Part I requirements.

#### Running options 1
There 3 files we provide in ./fsx
- `AkkaServer.fsx` is the server
- `AkkaClientOne.fsx` is the client
- `AkkaClient.fsx` is for performance test

#### Running options 2
you can also run .fs files in ./fs folder, this requires a Visual Studio or JetBrains Rider IDE

## Implementation
#### Client
We use Akka.actor heavily, the major responsibility of client is to send the message string to the server, and disply the result string to the screen
#### Server
We build Twitter Server basically by 3 Class: Tweet, User, and Twitter. 
- Tweet is the instance of tweet, identified by System time stamp
- User is the instance of User, identified by username
- Twitter is a Singleton instance, which is consist of many HashMap, in order to speed up the search for "#" and "@"

#### for "#" and "@", HashMap to speed up
Whenever a new Tweet is published, it will be parsed. If it contains "#" or "@", it will then be add to the HashMap.

#### Connecting and disconnecting
We use a `Long Pulling` techniques, every 1 seconds, the actor will try to query the tweets it subscribes, and if there is a difference to the previous result, it will be displayed to the screen

## How to use

#### Command Explanation
Our Client Command is consist of 9 String, seperated by comma ","

9 command format: 
**Very Important**
```
opt+","+POST+","+username+","+password+","+target_username+","+tweet_content+","+queryhashtag+","+at+","+register
```

In order to understand the means of below command, please look at below example 1


#### Example 1: Functionality
open 1st terminal as Server
```
dotnet fsi --langversion:preview AkkaServer.fsx
```

open 2nd terminal as Client
```
dotnet fsi --langversion:preview AkkaClientOne.fsx
```

Then Input Below Command line by line into CLient Terminal
```
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
```

![](pictures/3.png)

#### Example 2: retweet
Input Below Command line by line into CLient Terminal
```
retweet, ,user2,123456, ,tweet3 @Biden #Trump , , , 
querying, ,user2,123456, , , , ,
```

![](pictures/4.png)

#### Example 3: connect and disconnect
For this example, you can open `user1` as a terminal, open `user2` as another terminal. Then, since `user1` subscribes `user2`, the screen of `user1` will automatically refresh with the latest tweets of `user2`

After disconnecting, it will no longer refresh automatically.

```
connect, ,user1,123456, , , , ,
send, ,user2,123456, ,tweet4, , ,
send, ,user2,123456, ,tweet5, , ,
send, ,user2,123456, ,tweet6, , ,
disconnect, ,user1,123456, , , , ,
send, ,user2,123456, ,tweet7, , ,
```

![](pictures/5.png)

#### Example 4: Performance Test
- Firstly, open a Server as mentioned above.
- Then, run below command, You can change the 125 to any integer, it represents the number of users
```
dotnet fsi --langversion:preview AkkaClient.fsx 125
```
`result10.txt` is Performance test result under 10 users

## Test Measure
All test result obtained from `.fs` file in Jetbrains Rider IDE.
We test time cost of 3 measures, below N is the number of all users:
- register N users
- send 10 tweets for all users, and simulate a Zipf number of subscribes, and query tweets for every users
- Zipf subscribe N users
- query N users
- query N hasgtag
- query N mention
- N random operations

## Result

#### time cost in 3 different test scales
All test result obtained from `.fs` file in Jetbrains Rider IDE.
(milliseconds)

- For N<400, we sent a tweet "tweet_content+useri_jth @user$ #topic$ " ($ means random)
- For N>=400, we sent a tweet "t"

| N    | Register  | N users each send 10 tweets | Zipf Subscribe | query N users |  query N hasgtag  |  query N mention | N Randon Ops |
|------|-----------|-----------------------------|----------------|---------------|-------------------|------------------|--------------|
| 5    | 599.6734  | 184.5998                    | 21.8749        | 51.1026       | 29.262            | 24.1199          | 51.5178      |
| 10   | 659.8578  | 387.1673                    | 50.5391        | 103.9618      | 47.1174           | 56.1817          | 53.2401      |
| 25   | 807.435   | 1191.5766                   | 128.7027       | 246.3215      | 128.7414          | 123.4499         | 171.5757     |
| 50   | 769.0788  | 1691.3756                   | 467.9807       | 764.2445      | 493.4429          | 338.9445         | 219.0232     |
| 100  | 1341.3728 | 3748.3057                   | 446.3389       | 930.0312      | 433.1106          | 449.2736         | 947.7343     |
| 200  | 1232.6446 | 6565.2218                   | 865.9617       | 2279.6697     | 1400.8518         | 1336.9309        | 867.6679     |
| 400  | 1486.0643 | 8704.6874                   | 1675.8635      | 1553.9948     | 1303.3077         | 741.304          | 1330.1301    |
| 800  | 2510.7779 | 18426.727                   | 3333.8554      | 2343.8812     | 1452.3169         | 1437.4424        | 2010.4858    |
| 1600 | 4214.8476 | 35609.2485                  | 7195.5841      | 4874.3492     | 2860.3245         | 2820.996         | 4934.7314    |
| 3200 | 7642.8433 | 111190.343                  | 15425.6988     | 14089.3911    | 6775.7368         | 6146.9854        | 10698.6298   |

![](pictures/2.png)

#### number of subscribes, simulate a Zipf distribution
![](pictures/1.png)



## What is the largest network you managed to deal with
the biggest number of users we tested is 3200

We change some cinfiguration under this scenario, tweet content is only a single character "t".


Result is as below
```
The time of register 3200 users is 7642.843300
The time of send 10 tweets is 111190.342900
The time of Zipf subscribe 3200 users is 15425.698800
The time of query 3200 users is 14089.391100
The time of query 3200 hasgtag is 6775.736800
The time of query 3200 mention is 6146.985400
The time of 3200 random operations is 10698.629800
```

## Analysis
Send Tweet cost the most of time, since server will have to parse "@user" and "#topic", this will cost tons of server time. As a result, it's the most time-consuming operations

Zipf Subscribe cost 2nd most time, since it requires total 2*N subscribes operations.

Query cost the 3nd most time, since it consists of tons of string transmission. This will cost a lot of time.

For other operations, it just need N operations, so they don't cost as much time as previous 3 operations.





## Environment

```
- FSharp.Core 3.0.2
- Akka.FSharp 1.2.0
- Akka.Remote 1.2.0
- FsPickler 1.2.21
```