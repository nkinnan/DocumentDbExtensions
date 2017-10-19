# DocumentDbExtensions (now CosmosDB)

This library provides:
1) Automatic retry logic with overloads taking both syncronous and asyncronous delegates compatible with all DocumentDB client methods.
2) A "query interceptor" that A) both allows use of the aforementioned retry logic with Linq IQueryables against DocumentDB (this supports paging as well as stream-out via "yield")
3) A "query interceptor" that B) allows direct use of DateTime/Offset within the clauses.  No need to record these types of properties in the form of ticks or unix epoch values and then convert manually in your clauses anymore.  Simply decorate any DateTime/Offset properties with a special attribute and it "just works" while serializing to the database in ISO 8601 human-readable format.

Additional benefits:
Using this library allows you to simply return a DocumentDB IQueryable to the Microsoft OData libraries, allowing those libraries to convert any OData query in the URL into a DocumentDB query with no extra translation logic needed.  Again, it "just works".  An example along with tests demonstrating this functionality is available at https://github.com/nkinnan/DocumentDbExtensions