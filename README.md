# Demo Intensive cache miss
Sample code attached to [Intensive Cache Miss](http://mrmatrix.net/?p=209) article on my blog.

This code tries to show correct path of implementation for data retrieval process. 
The goal is to do not duplicate logic multiple times, but 

**NOTE** SingleKeyResolver disposable pattern implementation have to be added. The problem, to resolve for now is race condition between taking results and disposing tasks.