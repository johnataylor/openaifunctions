# openaifunctions
Exploring the poitential for organizing client code around OpenAI functions 

The function Resolver.Run will repeatedly call functions until the user's question is addressed.

The example illustrates how the model can "join" and "filter" across function results.

Naturally, if we are talking about "joins" we must be thinking in terms of sets of data. Here we are also showing how important it can be to factor your functions correctly. Specifically, providing the model with a function that deals with a single
entity might just cause it to call that function multiple times as part of working towards an answer for the user. However, changing that function to be able to deal with a collection and the model will realize only a single call is needed.

It is satisfying that both arrangements work, however, the difference in cost, both performance and dollar cost, might be significant.

Note the model appears to be able to create single element collections without any trouble if it sees they are needed.

Also note the schema for collections needs to be fully specified to include the type of the elements.

A chat bot is basically another loop around this loop.