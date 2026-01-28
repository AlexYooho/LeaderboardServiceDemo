# LeaderboardService system architecture and business process implementation design specifications

## 1. Background and Objectives(A stress test report is attached at the bottom.)

### 1.1 Backgroud
* Develop a small HTTP-based back-end service which stores andprovides scores and ranks for customers.
* All customers whose score is greater than zero participate in a competition. Each customer is associated with an unique rank in the leaderboard, determined by their scores.
* When customer’s score changes, its rank in leaderboard is affected in realtime.

### 1.2 Objectives
* Implemented using only **.NET Core**, data does not require persistent storage and is only stored in memory.
* Implement a **high-concurrency**, **low-latency**, and **stable** points leaderboard service.

## 2. Overall architecture design scheme
In terms of overall design, this service is based on the **MVCC** model of MySQL, which achieves read-write separation through **multi-version data + atomic switching**, but there are some differences in the specific implementation details.

### 2.1 Core design concept
| MySQL MVCC | Leaderboard          |
| ------------- | ------------------------- |
| Undo Log      | `_customerScoreDic`       |
| Read View     | `_leaderboardDataSources` |
| Version       | `_needRefresh` + build  |
| Commit        | Replace references once after build    |

**Key points: Read and write are completely isolated; queries always read only one "stable version"**.


## 3.Core data structure design

### 3.1 Write Model

```csharp
ConcurrentDictionary<ulong, long> _customerScoreDic;
```
* **Unique write source**
* Key：CustomerId
* Value：Enlarged integer fractions（score * 100）
* Features：

  * Thread safe
  * Support AddOrUpdate
  * No sorting or ranking is performed.

*Similar to **the latest version of the current row** in MySQL

### 3.2 Reading Model

```csharp
CustomerLeaderboardInfoModel[] _leaderboardDataSources;
Dictionary<ulong, int> _customerRankDic;
```

* **Completely read-only**
* The sorting and ranking have been completed.
* Use `volatile` + reference replacement to guarantee visibility.
* **No lock contention** occurred during the query

*Similar to **Read View** in MVCC.


### 3.3 Unranked user table

```csharp
ConcurrentDictionary<ulong, bool> _customerWithoutRankDic;
```

* Record users with a score <= 0
* Used in the `GetCustomerById` scenario
* Avoid full table scan


## 4. Write process（ModifyCustomerScore）

### 4.1 Write the flowchart

```text
Client
  │
  │ ModifyCustomerScore
  ▼
ConcurrentDictionary.AddOrUpdate
  │
  │ 标记 needRefresh = 1
  ▼
Return results immediately (without waiting for sorting).
```

### 4.2 Core design points

```csharp
var newScore = _customerScoreDic.AddOrUpdate(
    customerId,
    initScore,
    (_, old) => newScoreLogic
);

Interlocked.Exchange(ref _needRefresh, 1);
```

* **Write O(1)**
* Do not trigger sorting
* Non-blocking query
* Notify background threads by `_needRefresh`

* This corresponds to **writes do not block reads** in MVCC.

---

## 5. Backend build process

### 5.1 Refresh Thread Model

```csharp
Task.Factory.StartNew(
    RefreshLeaderboardProcess,
    TaskCreationOptions.LongRunning
)
```

* Single-threaded construction
* Permanently located in the background
* Merging multiple writes

---

### 5.2 Build process

```text
check needRefresh
   │
   ├─=0 → sleep 5ms
   │
   └─=1
       │
       ▼
copy scoreDic → List
       │
       ▼
sort（Score DESC, CustomerId ASC）
       │
       ▼
calculate Ranking
       │
       ▼
Generate new leaderboard data source
       │
       ▼
Atomic substitution reference
```

### 5.3 Key code

```csharp
_customerRankDic = customerRankDic;
_leaderboardDataSources = list.ToArray();
```

* Reference replacement = **version switching**
* The old version can still be read concurrently.
* No lock required

* Equivalent to **Commit** in MVCC.

---

## 6. Query process design

### 6.1 Search by ranking（GetCustomerByRank）

```text
read leaderboardDataSources
  │
  ├─ Parameter validation
  │
  └─ Array.Copy
```

* pure memory array
* O(n) copy，n ≤ 100
* Zero lockout, zero wait

---

### 6.2 Search by user（GetCustomerById）

#### 1️⃣ No user ranking

```text
customerWithoutRankDic Hit
  │
  └─ Return to the bottom of the list + yourself
```

#### 2️⃣ Users are ranked.

```text
customerRankDic.TryGetValue
  │
  └─ Based on rank before and after slicing
```

* Do not scan the entire list
* position O(1)

---

## 7. Consistency Model Explanation

| Dimension  | explanation         |
| --- | ---------- |
| real time | soft real-time      |
| Reading and writing  | Read-write separation       |
| concurrent  | Lock-free read, low-lock write    |
| ranking  | Monotonic and consistent (will not be out of order) |

* The query may display old lists.
* But dirty data or intermediate states will not occur.

*  Consistent with MVCC behavior under MySQL **RC/RR**.

---

## 8. Performance Characteristics Analysis

### 8.1 Time complexity

| operate | Complexity            |
| -- | -------------- |
| Write | O(1)           |
| Sort | O(N log N) |
| Query | O(K)（K ≤ 100）  |

### 8.2 Concurrency advantages

* Writes do not trigger sorting
* Queries do not depend on write locks
* Multiple writes merged into a single refresh

---

## 9. Scalable directions

* Segmented leaderboards, buckets（Shard by Score）
* Persistent data source（Dump / Load）

---
## 10. Stress test report

### 10.1 Stress testing environment and resource conditions
* Server configuration: 6C16G
* Samples:1000000(1000t,1000times)

### 10.2 Single interface stress test results
* POST /customer/{customerid}/score/{score}


* GET /leaderboard?start={start}&end={end}


* GET /leaderboard/{customerid}?high={high}&low={low}


### 10.3 Mixed pressure test results



---
