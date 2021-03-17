# RemoveUnused
## Removes unused code, given a ReSharper-generated list of code issues

This isn't a nicely-finished CL app because I only needed to use the code once, but:
1. Open your solution in Rider or Visual Studio with ReSharper
2. Make sure your solution is checked into version control in case it all goes wrong
3. Use the [find code issues](https://chrisseroka.wordpress.com/2013/10/28/find-unused-private-and-public-methods-with-resharper/) tool to list unused methods
4. If there are more methods than you want to remove by hand, then this is the tool for you.
5. Export ReSharper's list of issues as an XML file
6. Check out and build this repo
7. Mess with [ForReal.cs](https://github.com/samblackburn/RemoveUnused/blob/main/RemoveUnused/ForReal.cs#L25) so it points at your code and xml file
8. Run the explicit "test" to nuke your unused code
9. Try to build your solution, revert any specific edits that broke compilation

There were a few incorrectly removed methods when I tried this on a large codebase, but it still saved a lot of time compared to mashing alt+enter.
