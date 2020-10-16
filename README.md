# DOSP project 1

## Group members :
- Jiajing Liao
- Jingzhou Hu

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
 



## Environment:
language: F#

MacBook Pro 
2.3 GHz Dual-Core Intel Core i5

 Version:   3.1.402
 Commit:    9b5de826fd

Runtime Environment:
 OS Name:     Mac OS X
 OS Version:  10.15
 OS Platform: Darwin

Test time: 2020.9.18
		 	 		
					

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

## Developing:
- F#
- Akka

## Results
![](project2/pictures/Picture1.png)
![](project2/pictures/Picture1.png)