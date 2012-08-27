CommandRunner
=============

A C# wrapper that allows regex inspection and modification of subcommand output.

Build
-----

Builds under Mono and VS2010.  See build-mono.bat and build-vs.bat.

For either, edit the bat file to point gmcs or csc to your local installation of those files.

If gmcs is a bat file, don't forget to prefix it with "call", like the example.

Libraries and License
---------------------

CommandRunner uses the following libraries:

* JSON.NET - MIT License - http://json.codeplex.com
* NDesk.Options - MIT License - http://www.ndesk.org/Options

CommandRunner License: MIT (see LICENSE) - more information can be found at http://opensource.org/licenses/mit-license.php

Usage
-----

Run 'cr --help' for usage and options.

Generally, you will pass one or more commands and define rules to test against the output of those commands.  

A rule:

* defines a pattern regex and can parse STDOUT, STDERR, or both
* can define a short pattern regex, a faster pattern that must first match before attempting complex, slow pattern regexes

On a match, a rule can optionally:

* pass the original line through
* print formatted regex matches of the line on STDOUT, STDERR, or both
* print a separate report string at the end of all command output
* run search and replace rules on the output (e.g., to escape characters)
* continue checking more rules, or stop
* set an error flag

CommandRunner can also do some high-level operations on the commands, such as:

* force the return of all sub-commands, allowing success (0) returns on commands that error
* sum the returns of all sub-commnds
* print the time 

Passing multiple commands will execute them in order.  Passing multiple rules will execute them in order, as well.  (This is important if you have rules that stop rule execution.)

Flags can be used to create a single rule at the commandline.  This rule will execute before all other rule files.

Rule and Search/Replace files are written in JSON.  There are flags that will print examples for basing new files.

Examples
--------

* Time Run

Adding a -t flag will print the execution time of all commands run.

```
C:\Projects\CommandRunner>cr -t dir
 Volume in drive C is OSDisk
 Volume Serial Number is 4223-7618
 Directory of C:\Projects\CommandRunner
08/26/2012  03:11 PM    <DIR>          .
08/26/2012  03:11 PM    <DIR>          ..
<snip/>
			  18 File(s)        565,398 bytes
			   2 Dir(s)  20,343,488,512 bytes free
Time elapsed: 00:00:00.2830000
```

* Parse Build Ouput and Report Errors

Given a GCC output with errors and warnings (like the following, reduced to just the errors/warnings):

```
d:/projects/ExampleProject/inc/thing.hpp:75: warning: `class Thing' has virtual functions but non-virtual destructor
d:/projects/ExampleProject/src/thing.cpp:279: error: `updateThing' undeclared (first use this function)
d:/projects/ExampleProject/src/thing.cpp:279: error: (Each undeclared identifier is reported only once for each function it appears in.)
d:/projects/ExampleProject/src/thing.cpp::495:2: warning: no newline at end of file
```

You could use a rule file with the following contents:

```
[
  {
	"title": "Compile Warning with Line and Character",
	"example": "c:/example/example.cpp:495:2: warning: no newline at end of file",
	"passOutput": false,
	"stopProcessing": true,
	"setError": false,
	"processStdErr": true,
	"processStdOut": true,
	"stdoutFormat": null,
	"stderrFormat": null,
	"reportFormat": "Warning - File: {1} Line: {2} Character: {3} - {4}",
	"shortPattern": "warning",
	"pattern": "([^\\s^]+):([0-9]+):([0-9]+): warning:[\\s]+(.*)$"
  },
  {
	"title": "Compile Warning with Line",
	"example": "c:/example/example.hpp:15: warning: `class ExampleObject' has virtual functions but non-virtual destructor",
	"passOutput": false,
	"stopProcessing": true,
	"setError": false,
	"processStdErr": true,
	"processStdOut": true,
	"stdoutFormat": null,
	"stderrFormat": null,
	"reportFormat": "Warning - File: {1} Line: {2} - {3}",
	"shortPattern": "warning",
	"pattern": "([^\\s]+):([0-9]+): warning:[\\s]+(.*)$"
  },
  {
	"title": "Compile Error with Line",
	"example": "c:/example/example.cpp:279: error: `methodName' undeclared (first use this function)",
	"passOutput": false,
	"stopProcessing": true,
	"setError": true,
	"processStdErr": true,
	"processStdOut": true,
	"stdoutFormat": null,
	"stderrFormat": null,
	"reportFormat": "Error - File: {1} Line: {2} - {3}",
	"shortPattern": "error",
	"pattern": "([^\\s^:]+):([0-9]+): error:[\\s]+(.*)$"
  },
]
```

Running CommandRunner with a -r flag will produce the following output printed at the end of that build:

```
Warning - File: d:/projects/ExampleProject/inc/thing.hpp Line: 75 - `class Thing' has virtual functions but non-virtual destructor
Error - File: /projects/ExampleProject/src/thing.cpp Line: 279 - `updateThing' undeclared (first use this function)
Error - File: /projects/ExampleProject/src/thing.cpp Line: 279 - (Each undeclared identifier is reported only once for each function it appears in.)
Warning - File: d:/projects/ExampleProject/src/thing.cpp: Line: 495 Character: 2 - no newline at end of file
```

* Profile Rules for Performance

Passing an additional -s flag will show statistics for the rule matching.  Adding the flag to the above example produces:

```
Number of Rules: 3
Title: Compile Warning with Line and Character
Example: c:/example/example.cpp:495:2: warning: no newline at end of file
Pattern: ([^\s^]+):([0-9]+):([0-9]+): warning:[\s]+(.*)$
  Hits:   1 Duration 00:00:00 (Avg: 00:00:00)
  Misses: 1 Duration 00:00:00 (Avg: 00:00:00)
  Total:  2 Duration 00:00:00 (Avg: 00:00:00)
  Short Pattern: warning
	Hits:   2 Duration 00:00:00 (Avg: 00:00:00)
	Misses: 2 Duration 00:00:00 (Avg: 00:00:00)
	Total:  4 Duration 00:00:00 (Avg: 00:00:00)
Title: Compile Warning with Line
Example: c:/example/example.hpp:15: warning: `class ExampleObject' has virtual functions but non-virtual destructor
Pattern: ([^\s]+):([0-9]+): warning:[\s]+(.*)$
  Hits:   1 Duration 00:00:00 (Avg: 00:00:00)
  Misses: 0 Duration 00:00:00 (Avg: 00:00:00)
  Total:  1 Duration 00:00:00 (Avg: 00:00:00)
  Short Pattern: warning
	Hits:   1 Duration 00:00:00 (Avg: 00:00:00)
	Misses: 2 Duration 00:00:00 (Avg: 00:00:00)
	Total:  3 Duration 00:00:00 (Avg: 00:00:00)
Title: Compile Error with Line
Example: c:/example/example.cpp:279: error: `methodName' undeclared (first use this function)
Pattern: ([^\s^:]+):([0-9]+): error:[\s]+(.*)$
  Hits:   2 Duration 00:00:00 (Avg: 00:00:00)
  Misses: 0 Duration 00:00:00 (Avg: 00:00:00)
  Total:  2 Duration 00:00:00 (Avg: 00:00:00)
  Short Pattern: error
	Hits:   2 Duration 00:00:00 (Avg: 00:00:00)
	Misses: 0 Duration 00:00:00 (Avg: 00:00:00)
	Total:  2 Duration 00:00:00 (Avg: 00:00:00)
Pattern Hits:          4 Duration 00:00:00 (Avg: 00:00:00)
Pattern Misses:        1 Duration 00:00:00 (Avg: 00:00:00)
Pattern Total:         5 Duration 00:00:00 (Avg: 00:00:00)
Short Pattern Hits:    5 Duration 00:00:00 (Avg: 00:00:00)
Short Pattern Misses:  4 Duration 00:00:00 (Avg: 00:00:00)
Short Pattern Total:   9 Duration 00:00:00 (Avg: 00:00:00)
```

Statistics help to find if rules are matching or not and how long the program is spending in their evaluation.  (Which is much more interesting on longer output and many rules than this example.)

Contact
-------

Please let me know if you have any questions or suggestions: CommandRunner@jeremymurray.org


