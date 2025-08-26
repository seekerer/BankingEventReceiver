What things did you considered of during the implementation?

I've considered following things during implementation:

a) concurrency handling: we might have several running instances updating same account. Initially I planned to use RowVersion field and let EFCore handle optimistic conscurrency, but then switched to transactions.

b) multiple executions of the same data. This should never happen (and in my opinion upstream component is best to check that) but I've added logic to make sure we haven't processed same TXN twice.

c) Unit testing. It should be far more extended but due to constraints I've added at least basic ones... Which actually helped me even today.

d) Existing infrastructure. For this task, especially checking if TXN already processed - I'd probably go with in-memory storage. But given MSSQL db is already available - used it.

e) Dependency injection - i've tried my best to have components separated and depend on interfaces and not implementations. This way we can have better maintenance.

What could have been done better:
 
 - far more extended validation - like negative balance, long arithmetics, rounding, etc.
 - better deployment instructions
 - better components separation.

Anything was unclear?
