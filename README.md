## Introduction

This is my Distributed Operating System Principle projects

## Group members :
- Jiajing Liao, 01469951
- Jingzhou Hu, 11319238

## Environment and Version:
- F#
- Akka
    - Akka 1.2.3
    - Akka.FSharp 1.2.3
    - FsPickler 3.4.0
- ASP.Net core - 3.1.403

## IDE and Machine
- JetBrains Rider 2020.2.2
- MacBook Pro 2.3 GHz Dual-Core Intel Core i5
    - OS Name:     Mac OS X
    - OS Version:  10.15
    - OS Platform: Darwin

# DOSP project 1

## Usage:
```
dotnet fsi proj1.fsx 3 2
dotnet fsi proj1.fsx 40 24
dotnet fsi proj1.fsx 1000000 4
dotnet fsi proj1.fsx 10000000 24
```

## 1. As table below, the best size of work unit is: 10000
|     n           |     k     |    *workSize    |     # of Threads    |     realTime    |     CPUTime    |     timeRatio     |
|-----------------|-----------|-----------------|---------------------|-----------------|----------------|-------------------|
|     10000000    |     24    |     10000000    |     1               |     8.07        |     8.151      |     0.99006257    |
|     10000000    |     24    |     1000000     |     10              |     6.53        |     13.275     |     0.49190207    |
|     10000000    |     24    |     100000      |     100             |     6.683       |     13.514     |     0.4945242     |
|     10000000    |     24    |    *10000       |     1000            |     6.207       |     13.083     |    *0.47443247    |
|     10000000    |     24    |     1000        |     10000           |     7.154       |     13.49      |     0.53031875    |
|     10000000    |     24    |     100         |     100000          |     10.458      |     17.536     |     0.59637318    |
|     10000000    |     24    |     10          |     1000000         |     15.286      |     23.454     |     0.65174384    |
|     10000000    |     24    |     1           |     10000000        |     01:26.5     |     01:44.9    |     0.82463696    |



## 2.
					
dotnet fsi proj1.fsx 1000000 4
OUTPUT:  Nothing

## 3.
real Time:  0.232s 
CPU Time: 0.442s
Ratio = 0.52
 
					



## 4. 
```
The largest problem you managed to solve:    10^8
dotnet fsi proj1.fsx 100000000 24
Results: 
1 9 20 25 44 76 121 197 304 353 540 856 1301 2053 3112 3597 5448 8576 12981 20425 30908 35709 54032 84996 128601 202289 306060 353585 534964 841476 1273121 2002557 3029784 3500233 5295700 8329856 12602701 19823373 29991872 34648837 52422128 82457176
Time: Real: 00:01:08.188, CPU: 00:02:18.363	
```
		 	 		
					

# project2

## Introduction:
In this project, we implement the 2 algorithms:
- gossip
- push-sum

## How to run the code:
```
// topo= "full", topo= "2D", topo= "line", topo= "imp2D" 
// algorithm = "gossip" or "push-sum"
dotnet fsi --langversion:preview proj2.fsx 10 full gossip
dotnet fsi --langversion:preview proj2.fsx 10 imp2D push-sum
```

## Results
![](project2/pictures/Picture1.png)
![](project2/pictures/Picture1.png)

# project3

## Team
- Jiajing Liao, 01469951
- Jingzhou Hu, 11319238

## What is working:

- We diversely generate *numNodes* actors
- At each second, every node will send a message to a random address
- After *numRequests* seconds, the program will report the result and stop

### simplification statement
In this project, we implement the [Pastry](http://rowstron.azurewebsites.net/PAST/pastry.pdf), 
we made below change to make problem simple:
- Our ID range is from [0, 1000000000), this is more straightforward for human understanding.

- For example, when n=10, our code will generate Actor as: 000000000 100000000 200000000 300000000 400000000 500000000 600000000 700000000 800000000 900000000
- Our Code will generate Akka Actor in decimal, which means there is only 0, 1, 2, ..., 9.
- Above simplification has no impact to the final result. 
- we use a Trie to store the routing table, each time the node will compare it's own address with destination address, and then send the message to the first closest node in the Trie
- we use a modified decimal md5 as hash function, truncate leading 10 digits
## How to run the code:
```java
// dotnet fsi --langversion:preview project3.fsx numNodes numRequests
dotnet fsi --langversion:preview project3.fsx 10000 1
```
## What is the largest network you managed to deal with
the largest problem is as below:
```
numNodes is 100000 nodes
numRequests is 1 requests
Running time is: 157751.663500 ms
max hop is 5 hops
min hop is 0 hops
avg hop is 4.579850 hops
```

## Analysis
out data shows that the hops are O(log n)
which is reasonable because each time, the range will shrink by 2^b times.

When `numNodes` is small, numRequests will have impact to final result. However, when it is big, there is no much impact.

## Developing:
- F#
- Akka
    - Akka 1.2.3
    - Akka.FSharp 1.2.3
    - FsPickler 3.4.0
- ASP.Net core - 3.1.403
- JetBrains Rider 2020.2.2
- MacBook Pro 2.3 GHz Dual-Core Intel Core i5
    - OS Name:     Mac OS X
    - OS Version:  10.15
    - OS Platform: Darwin

## Graph Report
![](project3/picture1.png)

## Reference
- [Pastry](http://rowstron.azurewebsites.net/PAST/pastry.pdf)
- [Trie](https://blog.martindoms.com/2016/05/23/prefix-tree-trie-f-sharp)
- [MD5](http://www.fssnip.net/3D/title/MD5-hash)