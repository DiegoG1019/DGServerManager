# AttachProcess Command 
`attach-process (path-to-file [--existing=pid]) handler-name([handlerargs]) ...processargs `

## Explanation
Starts and Attaches a new Process for the Daemon to watch.
If you wish to attach an existing process, use `--existing` with the respective pid
Handler name is string leading to a LuaScript file or preced it by a `__` to use a default handler

## Arguments
### `handler-name`
The path leading to the Lua script file, you may also preceed it with a double `'_' ('__')` to use a default handler.
### `processargs`
Any extra arguments after all other arguments have been made are passed to the process, unless `--existing=pid` is passed.


## Options 
### `--existing=pid`
Replaces path-to-file and instructs the process watcher to add an existing process to the watchlist. Must be EITHER this option or a given path-to-file. replace `pid` with a Proccess Id
### `(handlerargs)`
#### example: `doSomething(/path/to/something someotherthing)`
Right after handler-name, without spacing, add a `()` (function call notation) and place the arguments you may wish to pass. If you don't wish to pass any, don't use the notation. Any given handler should not require these arguments, and this rule is enforced internally. Please refer to a specific extension's documentation in case of an exception. All arguments within the parentheses are taken just as any other regular arguments, but are passed directly to the handler.
## Lua Handlers
A script containing a function called 'Handle' as a Lua function, it's not mandatory for it to accept arguments, but it will otherwise be unable to do anything. They will also be fitted with extra context from the application within a table named 'Daemon'
This context includes: 
* A reference to `DaemonStatistics`
* `EnqueueCommandCall(stringarguments...)` A method to enqueue a command call, not expected to return anything
* `Command` A method that immediately runs a command and returns its result
* `CommandAsync(string id, params string[] args)` A method that enqueues a command call, but is expected to eventually return something.
`id` is the name of the task, used by the script to know where to look for the result once ready. The script can check if it's ready by doing
`if Context.AsyncResults[id] then` to check if a result exists. A given key will be created and emptied (set to `nil`) automatically, so, unless modified by the script, can be accessed safely
* `RegisterTask(LuaFunction o)` A method that receives a LuaFunction and adds the task to the task list. The LuaFunction in question can, optionally, take a `CommandArguments` object as an argument. This object will represent the Arguments the application was launched with.
* `UnregisterTask(LuaFunction o)` A method that receives a previously registered LuaFunction and removes it from the task list. This method is called automatically for every registered task when the script reaches its end.
* `MessageBoard` A reference to the global MessageBoard and the Subscribers/Boards list

As well as some other utility functionality in a table named 'Script' including:
* no functions yet

Lua handlers should have both a `init()` function and a `handle()` function; all code outside a function is run immediately, in the current thread, and thus it's not recommended to put much work there if at all, as it could slow down the system. `init()` should contain code pertaining any and actual initialization of the script; while `handle()` as covered previously, is meant to handle the process in question, and is called repeatedly.